using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

internal sealed class AuditBrokerProgram
{
    static int Main(string[] args)
    {
        if (args != null && args.Length > 0 && args[0] == "--console")
        {
            AuditBrokerCore.Run();
            return 0;
        }
        ServiceBase.Run(new AuditBrokerService());
        return 0;
    }
}

internal sealed class AuditBrokerService : ServiceBase
{
    Thread _thread;

    public AuditBrokerService()
    {
        ServiceName = "AhzAuditBroker";
        CanStop = true;
        CanShutdown = true;
        AutoLog = false;
    }

    protected override void OnStart(string[] args)
    {
        _thread = new Thread(AuditBrokerCore.Run);
        _thread.IsBackground = true;
        _thread.Start();
    }

    protected override void OnStop()
    {
        AuditBrokerCore.Stop();
        try { if (_thread != null) _thread.Join(5000); } catch { }
    }

    protected override void OnShutdown()
    {
        OnStop();
        base.OnShutdown();
    }
}

internal static class AuditBrokerCore
{
    const string PipeName = "ahz-scribe-audit";
    static readonly object Gate = new object();
    static readonly JavaScriptSerializer Json = new JavaScriptSerializer();
    static volatile bool _running = true;
    static string _prevHash = "";
    static byte[] _key;

    static string AuditDir
    {
        get
        {
            string dir = Environment.GetEnvironmentVariable("AHZ_AUDIT_DIR");
            if (string.IsNullOrEmpty(dir))
                dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "AhzScribe", "audit");
            return dir;
        }
    }

    static string AuditPath { get { return Path.Combine(AuditDir, "audit.jsonl"); } }
    static string KeyPath { get { return Path.Combine(AuditDir, "audit.key"); } }

    public static void Stop() { _running = false; }

    public static void Run()
    {
        Directory.CreateDirectory(AuditDir);
        LoadKey();
        _prevHash = LastHash();
        Log("broker starting pipe=" + PipeName + " audit=" + AuditPath);
        while (_running)
        {
            try
            {
                PipeSecurity sec = new PipeSecurity();
                sec.AddAccessRule(new PipeAccessRule(
                    new SecurityIdentifier(WellKnownSidType.WorldSid, null),
                    PipeAccessRights.FullControl, AccessControlType.Allow));
                sec.AddAccessRule(new PipeAccessRule(
                    new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
                    PipeAccessRights.FullControl, AccessControlType.Allow));

                using (NamedPipeServerStream pipe = new NamedPipeServerStream(
                    PipeName, PipeDirection.InOut, 10, PipeTransmissionMode.Byte,
                    PipeOptions.None, 4096, 4096, sec))
                {
                    pipe.WaitForConnection();
                    using (StreamReader r = new StreamReader(pipe, Encoding.UTF8))
                    using (StreamWriter w = new StreamWriter(pipe, new UTF8Encoding(false)) { AutoFlush = true })
                    {
                        string line = r.ReadLine();
                        string reply = Handle(line);
                        w.WriteLine(reply);
                    }
                }
            }
            catch (Exception ex)
            {
                Log("[broker] " + ex);
                Thread.Sleep(300);
            }
        }
    }

    static string Handle(string line)
    {
        if (string.IsNullOrEmpty(line)) return "{\"status\":\"error\",\"message\":\"empty\"}";
        Dictionary<string, object> req = null;
        try { req = Json.Deserialize<Dictionary<string, object>>(line); }
        catch (Exception ex) { return "{\"status\":\"error\",\"message\":" + Quote(ex.Message) + "}"; }
        if (req == null) return "{\"status\":\"error\",\"message\":\"bad json\"}";
        string op = Str(req, "op");
        if (op == "ping") return "{\"status\":\"ok\",\"message\":\"pong\"}";
        if (op == "append")
        {
            object docObj;
            if (!req.TryGetValue("doc", out docObj) || docObj == null)
                return "{\"status\":\"error\",\"message\":\"missing doc\"}";
            var doc = docObj as Dictionary<string, object>;
            if (doc == null)
            {
                var d2 = docObj as IDictionary<string, object>;
                if (d2 != null) doc = new Dictionary<string, object>(d2);
            }
            if (doc == null) return "{\"status\":\"error\",\"message\":\"bad doc\"}";
            string payload = Str(req, "payload");
            try
            {
                string id = Append(doc, payload);
                return "{\"status\":\"ok\",\"audit_id\":" + Quote(id) + "}";
            }
            catch (Exception ex)
            {
                return "{\"status\":\"error\",\"message\":" + Quote(ex.Message) + "}";
            }
        }
        if (op == "verify") return "{\"status\":\"ok\",\"message\":\"broker v2\"}";
        return "{\"status\":\"error\",\"message\":\"unknown op\"}";
    }

    static string Append(Dictionary<string, object> doc, string payload)
    {
        lock (Gate)
        {
            string id = "aud_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + "_" + Guid.NewGuid().ToString("N").Substring(0, 4);
            doc["audit_id"] = id;
            doc["ts"] = DateTimeOffset.UtcNow.ToString("o");
            if (payload != null && payload.Length > 0)
            {
                string pdir = Path.Combine(AuditDir, "payloads");
                Directory.CreateDirectory(pdir);
                doc["payload_ref"] = Path.Combine("payloads", id + ".json");
                File.WriteAllText(Path.Combine(pdir, id + ".json"), payload, new UTF8Encoding(false));
            }
            string canonical = Json.Serialize(doc);
            doc["prev_hash"] = _prevHash;
            string entryHash = HmacLike(Encoding.UTF8.GetBytes(_prevHash + "|" + canonical));
            doc["entry_hash"] = entryHash;
            if (_key != null && _key.Length > 0)
                doc["hmac"] = Hmac(_key, _prevHash + "|" + canonical);
            string line = Json.Serialize(doc);
            using (FileStream fs = new FileStream(AuditPath, FileMode.Append, FileAccess.Write, FileShare.Read))
            using (StreamWriter w = new StreamWriter(fs, new UTF8Encoding(false)))
            {
                w.WriteLine(line);
                w.Flush();
            }
            _prevHash = entryHash;
            return id;
        }
    }

    static void LoadKey()
    {
        try
        {
            if (!File.Exists(KeyPath))
            {
                byte[] b = new byte[32];
                using (RandomNumberGenerator rng = RandomNumberGenerator.Create()) rng.GetBytes(b);
                File.WriteAllBytes(KeyPath, b);
            }
            _key = File.ReadAllBytes(KeyPath);
        }
        catch (Exception ex) { Log("[broker] key: " + ex.Message); _key = null; }
    }

    static string LastHash()
    {
        try
        {
            if (!File.Exists(AuditPath)) return "";
            string last = null;
            foreach (string l in File.ReadLines(AuditPath, Encoding.UTF8))
                if (!string.IsNullOrWhiteSpace(l)) last = l;
            if (last == null) return "";
            var d = Json.Deserialize<Dictionary<string, object>>(last);
            return d != null && d.ContainsKey("entry_hash") ? Convert.ToString(d["entry_hash"]) : "";
        }
        catch { return ""; }
    }

    static string HmacLike(byte[] data)
    {
        using (SHA256 sha = SHA256.Create()) return Convert.ToBase64String(sha.ComputeHash(data));
    }

    static string Hmac(byte[] key, string data)
    {
        using (HMACSHA256 h = new HMACSHA256(key))
            return Convert.ToBase64String(h.ComputeHash(Encoding.UTF8.GetBytes(data)));
    }

    static string Str(Dictionary<string, object> d, string key)
    {
        object v;
        return d.TryGetValue(key, out v) && v != null ? Convert.ToString(v) : "";
    }

    static string Quote(string s)
    {
        return Json.Serialize(s == null ? "" : s);
    }

    static void Log(string s)
    {
        try
        {
            Directory.CreateDirectory(AuditDir);
            File.AppendAllText(Path.Combine(AuditDir, "broker.log"),
                DateTime.Now.ToString("o") + " " + s + Environment.NewLine, new UTF8Encoding(false));
        }
        catch { }
    }
}
