using System;
using System.Collections.Generic;
using AhzScribe.Contracts;

namespace AhzScribe.Face
{
    public sealed class UiSnapshot
    {
        public FaceState State { get; set; }
        public BodyStatus Body { get; set; }
        public List<MemoryItem> Memories { get; set; }
    }

    // 唯一状态广播点。面板订阅这里，不允许各自轮询文件。
    public sealed class UIStateBus
    {
        public event Action<UiSnapshot> Changed;

        public void Publish(UiSnapshot snapshot)
        {
            Action<UiSnapshot> h = Changed;
            if (h != null) h(snapshot);
        }
    }
}
