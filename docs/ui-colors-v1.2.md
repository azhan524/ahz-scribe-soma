# 硅基生物 UI 配色规划书 v1.2
## AhzScribe 观察窗 · 深色主题 Token 系统

> 配套文档：silicon-being-ui-spec-v1.3.md（规格书颜色一律以本文件为唯一来源，冲突时以本文件为准）
> 技术栈：C# WinForms 自绘 + WebView2（.NET 10，单实例）/ CefSharp（.NET Framework 4.8 兼容分支）
> 定位：可直接写进代码的配色 token 系统。所有组件只引用 token，不硬编码颜色。

---

## 版本记录

| 版本 | 变更 |
|---|---|
| v1.2 | .NET 10 正式改用 WebView2；CefSharp 仅保留在 .NET Framework 4.8 兼容分支；颜色 token 不变。 |
| v1.0 | 初版：五层背景、四级文字、六状态色、组合规则、区域配色、对比度检查、WinForms 实现、浅色预留 |
| v1.1 | 补 `bg-disputed` token（对齐规格书 disputed 行浅黄底）；补动画辅助 token `state-info-dim`；补 `fallback` 备用通道标注色（复用 state-warning）；补充 DPI 最低系统要求；补验收清单对应项；声明"本文件为唯一颜色来源" |

---

## 1. 配色原则

1. **深色为主**：托盘常驻的观察窗，深色不刺眼，适合长时间低亮度存在。
2. **颜色只用于语义**：任何颜色出现都对应一个状态（成功/警告/危险/强调/触发源），不做装饰性渐变、不做彩色边框。
3. **背景分五层，文字分四级**：层次靠明度差区分，不靠色相。色相只留给状态。
4. **状态色小面积点缀**：状态色只用于圆点、左边框、下划线、标签文字，不用于大面积背景。
5. **事实层中性，解释层柔和**：事实用无彩色（灰阶），解释层可带轻微暖色/冷色倾向，但明度必须低于事实层。
6. **急停是唯一例外**：全界面唯一允许纯白文字 + 大面积状态色背景的元素。

---

## 2. 色彩 Token 表

### 2.1 背景层级（5 层 + 1 专用）

| Token | HEX | 用途 |
|---|---|---|
| `bg-base` | `#121212` | 窗口最底层（很少直接见） |
| `bg-primary` | `#1E1E1E` | 主窗口背景、对话区、底栏 |
| `bg-secondary` | `#252526` | 左栏、输入框、卡片底、Agent 消息气泡 |
| `bg-tertiary` | `#2D2D30` | Tab 头、折叠块头、thought 展开区 |
| `bg-elevated` | `#3E3E42` | hover、选中行、弹出菜单、用户消息气泡 |
| `bg-disputed` | `#3A3520` | 记忆/审计中 `disputed` 行底色（v1.1 新增，浅黄底） |
| `border-subtle` | `#333333` | 1px 分割线 |
| `border-strong` | `#4A4A4F` | 输入框聚焦边框 |

> 五层背景明度差约 8-10 点，肉眼可辨但不刺眼。**不要增加第六层**。`bg-disputed` 是语义专用底，不计入背景层级。

### 2.2 文字层级（4 级）

| Token | HEX | 对比度(on `bg-primary`) | 用途 |
|---|---|---|---|
| `text-primary` | `#CCCCCC` | ~8.5:1 | 正文、消息、表格主内容 |
| `text-secondary` | `#9D9D9D` | ~5.2:1 | 辅助信息、时间戳、解释层 |
| `text-tertiary` | `#808080` | ~3.9:1 | `none` 行动、占位、弱提示 |
| `text-disabled` | `#5A5A5A` | ~2.3:1 | 禁用态、急停时输入框 placeholder |

> `text-tertiary` 的 3.9:1 低于 WCAG AA 正文标准，但**它是 `none` 的指定色**——`none` 需要"可读但明显弱于 message"，3.9:1 是刻意的选择。`text-disabled` 不承载信息，只表示不可用，低对比度可接受。

### 2.3 状态色（语义，小面积使用）

| Token | HEX | 语义 | 使用位置 |
|---|---|---|---|
| `state-info` | `#3794FF` | 强调、链接、选中 | Tab 下划线、链接文字、`user_message` 标签 |
| `state-success` | `#4EC9B0` | 成功、`ok`、`memory` | `memory` 行动标签、`ok` 结果、`body_event` 标签 |
| `state-warning` | `#D7BA7D` | 警告、`disputed`、待确认、**备用通道** | `disputed` 左边框、`intent_due` 标签、待确认圆点、`fallback` 行 `[备用]` 标签 |
| `state-danger` | `#F14C4C` | 危险、`blocked`、`error` | `blocked` 左边框、`error` 状态、危险圆点 |
| `state-drive` | `#C586C0` | 内驱 | `drive` 触发源标签 |
| `state-neutral` | `#808080` | 中性、休眠 | `timer` 标签、休眠圆点 |
| `state-info-dim` | `#1F5A99` | 呼吸动画暗端插值色（v1.1 新增，非语义，仅动画用） | 脉冲中圆点呼吸插值 `#1F5A99`↔`#3794FF` |

> 六个状态色 + 一个动画辅助色，覆盖所有语义。**不新增第七个状态色**——需要新语义时，优先复用已有色 + 文字说明。

### 2.4 触发源标签色（脉冲 Tab 专用）

| 触发源 | Token | HEX | 视觉 |
|---|---|---|---|
| `user_message` | `state-info` | `#3794FF` | 蓝色文字标签 |
| `body_event` | `state-success` | `#4EC9B0` | 青色文字标签 |
| `drive` | `state-drive` | `#C586C0` | 紫色文字标签 |
| `timer` | `state-neutral` | `#808080` | 灰色文字标签 |
| `intent_due` | `state-warning` | `#D7BA7D` | 黄色文字标签 |

> 标签是**纯文字着色，不加背景块**。加背景块会让列表变花，违背"安静"原则。

---

## 3. 组合规则（前景 + 背景配对表）

| 前景 \ 背景 | `bg-primary` | `bg-secondary` | `bg-tertiary` | `bg-elevated` |
|---|---|---|---|---|
| `text-primary` | ✅ 正文 | ✅ 左栏卡片 | ✅ Tab 未选中 | ✅ hover 行 |
| `text-secondary` | ✅ 时间戳 | ✅ 辅助 | ✅ | ✅ |
| `text-tertiary` | ✅ `none` | ✅ 占位 | ⚠️ 仅短文本 | ⚠️ |
| `state-info` | ✅ 链接/标签 | ✅ | ✅ Tab 选中 | ✅ |
| `state-success` | ✅ `ok`/`memory` | ✅ | ⚠️ | ⚠️ |
| `state-warning` | ✅ 标签 | ✅ | ⚠️ | ⚠️ |
| `state-danger` | ✅ `blocked`/`error` | ✅ | ⚠️ | ⚠️ |
| `state-drive` | ✅ 标签 | ✅ | ⚠️ | ⚠️ |

**规则**：
- ✅ = 可自由组合
- ⚠️ = 仅在短文本（≤10 字）或小面积（标签、圆点、边框）使用，不用于正文段落
- 状态色在 `bg-tertiary` / `bg-elevated` 上对比度会下降，长文本一律用 `text-primary`

---

## 4. 各区域具体配色方案

### 4.1 顶栏 BodyStateBar
```
背景：bg-primary #1E1E1E
文字：text-primary #CCCCCC（字段值）+ text-secondary #9D9D9D（分隔符 │）
圆点：按状态取 state-* 色
底边：border-subtle #333333 1px
```

圆点四态：
- 休眠：`state-neutral #808080`，无动画
- 脉冲中：`state-info #3794FF`，呼吸（插值 `state-info-dim #1F5A99`↔`state-info #3794FF`，周期 2.5s，正弦缓动）
- 待确认：`state-warning #D7BA7D`，常亮
- 急停：`state-danger #F14C4C`，常亮

### 4.2 左栏 StatePanel
```
背景：bg-secondary #252526
卡片底：透明（露出 bg-secondary）
卡片间分割：border-subtle #333333 1px
卡片标题：text-secondary #9D9D9D，10pt
卡片内容：text-primary #CCCCCC，11pt 加粗
情绪圆点：按 mood 取 state-* 色（平静=neutral，活跃=info，低落=secondary）
```

**左栏不加卡片背景色**。卡片用分割线区隔，不用色块——色块会让左栏显得"碎"，违背观察窗的安静感。

### 4.3 Tab 头
```
背景：bg-tertiary #2D2D30
未选中文字：text-secondary #9D9D9D
选中文字：text-primary #CCCCCC
选中下划线：state-info #3794FF，2px
hover：文字变 text-primary，不改变背景
脉冲 Tab 未读小圆点：state-info #3794FF，4px
```

### 4.4 对话 Tab
```
用户消息气泡：bg-elevated #3E3E42，文字 text-primary #CCCCCC
Agent 消息气泡：bg-secondary #252526，文字 text-primary #CCCCCC
折叠块头：bg-tertiary #2D2D30，文字 text-secondary #9D9D9D
折叠块内容（解释层）：bg-primary #1E1E1E + text-secondary #9D9D9D + 斜体
最后响应时间：text-tertiary #808080
错误红点：state-danger #F14C4C
```

**用户和 Agent 气泡靠背景明度差区分**（elevated vs secondary），不靠色相。色相留给状态。

### 4.5 脉冲 Tab
```
列表背景：bg-primary #1E1E1E
交替行（斑马纹）：bg-primary #1E1E1E / bg-secondary #252526
hover 行：bg-elevated #3E3E42
选中行：bg-elevated #3E3E42 + 左边框 2px state-info
时间列：text-secondary #9D9D9D，等宽
触发源标签：按 2.4 表取色，纯文字
行动文字：
  message / tool → text-primary #CCCCCC
  none → text-tertiary #808080（规格书指定）
  memory → state-success #4EC9B0
展开区 state_restore / state_writeback：bg-secondary #252526 + text-primary，等宽
展开区 thought（解释层）：bg-tertiary #2D2D30 + text-secondary #9D9D9D + 斜体
展开区 action：bg-secondary #252526 + text-primary，等宽
audit_id 链接：state-info #3794FF
```

**关键**：`thought` 用 `bg-tertiary`（比周围亮一档）+ 斜体 + 次级文字色，形成"这是解释、不是事实"的视觉暗示。工具结果用等宽 + 主文字色，形成"这是事实"的暗示。

### 4.6 审计 Tab
```
表格背景：bg-primary #1E1E1E
表头：bg-tertiary #2D2D30 + text-secondary #9D9D9D
斑马纹：bg-primary / bg-secondary
blocked 行：左边框 3px state-danger #F14C4C（整行不变色）
approved / ok：文字 state-success #4EC9B0
rejected / error：文字 state-danger #F14C4C
pending：文字 state-warning #D7BA7D
fallback 行（v1.1）：时间列后加 state-warning #D7BA7D 的 [备用] 文字标签，不加行底色
```

**`blocked` 行只加左边框，不改整行底色**。整行染红会让审计表格变"警报墙"，失去可读性。`fallback` 同样用小面积标签标注，不污染整行。

### 4.7 记忆 Tab
```
语义表格：同审计表格配色
disputed 行：底 bg-disputed #3A3520 + 左边框 3px state-warning #D7BA7D
情节卡片：bg-secondary #252526 + border-subtle #333333 1px
  open 徽章：state-success 文字 + 透明底
  closed 徽章：text-tertiary 文字
  disputed 卡片：左边框 3px state-warning
情绪标签：text-secondary #9D9D9D + 小圆点按情绪取色
```

### 4.8 底栏 ActionBar
```
背景：bg-primary #1E1E1E
顶边：border-subtle #333333 1px
输入框：bg-secondary #252526 + text-primary #CCCCCC
输入框聚焦：border-strong #4A4A4F 1px
占位文字：text-tertiary #808080
⏸ / 📋 按钮：图标 text-secondary #9D9D9D，hover 变 text-primary
急停按钮：DangerButtonBg #C42B1C + 纯白 #FFFFFF（唯一使用纯白的地方）
```

**急停按钮是整套 UI 里唯一使用纯白文字 + 大面积状态色背景的地方**。这是刻意的——它必须是全界面最"扎眼"的元素，其余一切克制。

### 4.9 托盘图标
```
休眠：state-neutral #808080
脉冲中：state-info #3794FF（呼吸）
待确认：state-warning #D7BA7D
急停：state-danger #F14C4C
```

图标本身是纯色圆点，不加边框、不加阴影。提供 16×16、32×32、48×48 三套 PNG，按 DPI 选择，不靠 GDI+ 缩放。

---

## 5. 对比度检查（关键配对）

| 配对 | 对比度 | 是否达标 |
|---|---|---|
| `#CCCCCC` on `#1E1E1E` | 8.5:1 | ✅ AAA |
| `#9D9D9D` on `#1E1E1E` | 5.2:1 | ✅ AA |
| `#808080` on `#1E1E1E` | 3.9:1 | ⚠️ 大字体/短文本可用（`none` 刻意选择） |
| `#3794FF` on `#1E1E1E` | 3.5:1 | ⚠️ 仅链接/下划线/标签，不用于正文 |
| `#4EC9B0` on `#1E1E1E` | 6.8:1 | ✅ AA |
| `#D7BA7D` on `#1E1E1E` | 7.2:1 | ✅ AA |
| `#F14C4C` on `#1E1E1E` | 4.1:1 | ⚠️ 仅边框/圆点/短标签 |
| `#FFFFFF` on `#C42B1C` | 5.9:1 | ✅ AA（急停按钮） |

**结论**：正文和主要信息全部 ≥ AA；三个 ⚠️ 都是刻意的——`#808080` 用于 `none`（需要弱化）、`#3794FF` 用于链接和下划线（不承载长文本）、`#F14C4C` 用于边框和圆点（不承载长文本）。

---

## 6. WinForms 实现注意

### 6.1 颜色定义

所有 token 写在一个 `static class Theme`，组件只引用 token：

```csharp
public static class Theme
{
    // 背景
    public static readonly Color BgBase      = Color.FromArgb(0x12, 0x12, 0x12);
    public static readonly Color BgPrimary   = Color.FromArgb(0x1E, 0x1E, 0x1E);
    public static readonly Color BgSecondary = Color.FromArgb(0x25, 0x25, 0x26);
    public static readonly Color BgTertiary  = Color.FromArgb(0x2D, 0x2D, 0x30);
    public static readonly Color BgElevated  = Color.FromArgb(0x3E, 0x3E, 0x42);
    public static readonly Color BgDisputed  = Color.FromArgb(0x3A, 0x35, 0x20); // v1.1 新增
    public static readonly Color BorderSubtle = Color.FromArgb(0x33, 0x33, 0x33);
    public static readonly Color BorderStrong = Color.FromArgb(0x4A, 0x4A, 0x4F);

    // 文字
    public static readonly Color TextPrimary   = Color.FromArgb(0xCC, 0xCC, 0xCC);
    public static readonly Color TextSecondary = Color.FromArgb(0x9D, 0x9D, 0x9D);
    public static readonly Color TextTertiary  = Color.FromArgb(0x80, 0x80, 0x80);
    public static readonly Color TextDisabled  = Color.FromArgb(0x5A, 0x5A, 0x5A);

    // 状态
    public static readonly Color StateInfo     = Color.FromArgb(0x37, 0x94, 0xFF);
    public static readonly Color StateInfoDim  = Color.FromArgb(0x1F, 0x5A, 0x99); // v1.1 新增，呼吸暗端
    public static readonly Color StateSuccess  = Color.FromArgb(0x4E, 0xC9, 0xB0);
    public static readonly Color StateWarning  = Color.FromArgb(0xD7, 0xBA, 0x7D);
    public static readonly Color StateDanger   = Color.FromArgb(0xF1, 0x4C, 0x4C);
    public static readonly Color StateDrive    = Color.FromArgb(0xC5, 0x86, 0xC0);
    public static readonly Color StateNeutral  = Color.FromArgb(0x80, 0x80, 0x80);

    // 急停专用
    public static readonly Color DangerButtonBg = Color.FromArgb(0xC4, 0x2B, 0x1C);
    public static readonly Color DangerButtonFg = Color.White;
}
```

### 6.2 自绘抗锯齿

所有 `Graphics` 对象设：
```csharp
g.SmoothingMode = SmoothingMode.AntiAlias;
g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
```
圆点和边框在非整数 DPI 下会有锯齿，必须开抗锯齿。

### 6.3 呼吸动画用实色插值

**WinForms 不支持稳定的 alpha 混合**，不要用 `Color.FromArgb(alpha, ...)` 直接画。用实色插值：

```csharp
Color Lerp(Color a, Color b, float t) =>
    Color.FromArgb(
        (int)(a.R + (b.R - a.R) * t),
        (int)(a.G + (b.G - a.G) * t),
        (int)(a.B + (b.B - a.B) * t));

// 蓝色呼吸：在暗和亮之间插值
var t = 0.5f + 0.5f * (float)Math.Sin(DateTime.Now.TimeOfDay.TotalSeconds * Math.PI / 1.25);
var color = Lerp(Theme.StateInfoDim, Theme.StateInfo, t);
```

### 6.4 WebView2 侧 CSS 变量

HTML 的 CSS 变量直接对应 token：

```css
:root {
  --bg-base: #121212;
  --bg-primary: #1E1E1E;
  --bg-secondary: #252526;
  --bg-tertiary: #2D2D30;
  --bg-elevated: #3E3E42;
  --bg-disputed: #3A3520;
  --border-subtle: #333333;
  --border-strong: #4A4A4F;
  --text-primary: #CCCCCC;
  --text-secondary: #9D9D9D;
  --text-tertiary: #808080;
  --text-disabled: #5A5A5A;
  --state-info: #3794FF;
  --state-info-dim: #1F5A99;
  --state-success: #4EC9B0;
  --state-warning: #D7BA7D;
  --state-danger: #F14C4C;
  --state-drive: #C586C0;
  --state-neutral: #808080;
  --danger-button-bg: #C42B1C;
  --danger-button-fg: #FFFFFF;
}

body {
  background: var(--bg-primary);
  color: var(--text-primary);
  font-family: 'Microsoft YaHei UI', 'Segoe UI', sans-serif;
}
```

C# 侧通过 `host.getTheme()` 暴露 token 给 JS，**不把颜色硬编码在 HTML 里**——以后切主题只改 C# 一处。

### 6.5 DPI 缩放

颜色本身不随 DPI 变，但**自绘的圆点半径、边框宽度要乘 `scaleFactor`**：
```csharp
var scale = g.DpiX / 96.0f;
var penWidth = Math.Max(1, (int)Math.Round(1 * scale));  // 1px 边框
var dotRadius = (int)Math.Round(3 * scale);               // 3px 圆点
```

1px 边框在 150% DPI 下应该是 1.5px 取整 2px，否则会消失或变粗。

### 6.6 DPI 最低系统要求（v1.1 补充）

- **PerMonitorV2 需要 Windows 10 1703+ 完整支持**；Windows 10 1703 以下系统上，4.8 分支自动退化为 System DPI（跟随系统主显示器），表现为缩放平滑度略降，功能不受影响。
- .NET 10 分支的 `Application.SetHighDpiMode(PerMonitorV2)` 在低版本系统上由运行时自行兜底，无需额外处理。

---

## 7. 反例（这些组合不要用）

- ❌ **状态色做大面积背景**：比如整行 `bg-danger`。用左边框 + 文字色代替。
- ❌ **彩色渐变**：深色主题下渐变会显得廉价，违背"安静"。
- ❌ **超过 6 个状态色**：新增语义时复用已有色 + 文字说明（`state-info-dim` 是动画辅助色，不计入）。
- ❌ **文字用纯白 `#FFFFFF`**：除了急停按钮，其余文字最高到 `#CCCCCC`。纯白在深色背景上会有光晕感。
- ❌ **`bg-primary` 上直接放 `state-info` 长文本**：对比度 3.5:1，长文本读起来累。链接可以，正文不行。
- ❌ **给卡片加背景色块**：左栏和记忆卡片用分割线 + 透明底，不用色块（`bg-disputed` 是语义标记，不是装饰）。
- ❌ **用色相区分"事实/解释"**：用明度 + 字体（等宽 vs 斜体）区分，色相留给状态。
- ❌ **WinForms 里用 alpha 做呼吸**：用实色插值，避免渲染不一致。
- ❌ **托盘图标靠 GDI+ 缩放**：提供多套 PNG，按 DPI 选择。
- ❌ **新增第七个状态色**：优先复用已有色 + 文字说明。

---

## 8. 浅色主题扩展预留

token 系统已经为浅色主题留好口子——**所有颜色只通过 token 引用**，浅色主题只需换一套 token 值，不改任何组件代码：

```csharp
public interface ITheme
{
    Color BgBase { get; }
    Color BgPrimary { get; }
    Color BgSecondary { get; }
    Color BgTertiary { get; }
    Color BgElevated { get; }
    Color BgDisputed { get; }        // v1.1 新增
    Color BorderSubtle { get; }
    Color BorderStrong { get; }
    Color TextPrimary { get; }
    Color TextSecondary { get; }
    Color TextTertiary { get; }
    Color TextDisabled { get; }
    Color StateInfo { get; }
    Color StateInfoDim { get; }      // v1.1 新增
    Color StateSuccess { get; }
    Color StateWarning { get; }
    Color StateDanger { get; }
    Color StateDrive { get; }
    Color StateNeutral { get; }
}

public class DarkTheme : ITheme { /* 本文件这套 */ }
public class LightTheme : ITheme { /* 对应的浅色值 */ }
```

浅色主题的核心改动：
- 背景层级反转：`bg-primary` 变 `#FFFFFF`，`bg-secondary` 变 `#F3F3F3`，`bg-tertiary` 变 `#E8E8E8`，`bg-elevated` 变 `#DADADA`，`bg-disputed` 变 `#F5EFD8`。
- 文字层级反转：`text-primary` 变 `#1E1E1E`，`text-secondary` 变 `#5A5A5A`，`text-tertiary` 变 `#8A8A8A`，`text-disabled` 变 `#B0B0B0`。
- 状态色保持色相，降低明度以适配浅底：`#3794FF` → `#0066CC`，`#4EC9B0` → `#0E7A5F`，`#D7BA7D` → `#8A6D1F`，`#F14C4C` → `#C42B1C`。

**但第一版只做深色**。浅色主题等深色跑顺了、确实觉得白天刺眼再加，不要一开始就做双主题——双主题会让每个组件的自绘代码复杂度翻倍。

---

## 9. 验收清单

### Token 完整性
- [ ] `Theme` 静态类覆盖本文件第 2 节全部 token（含 `BgDisputed`、`StateInfoDim`）。
- [ ] 组件代码中无硬编码颜色（`Color.FromArgb` 只出现在 `Theme.cs`）。
- [ ] WebView2 的 CSS 变量与 C# 侧 token 一一对应。
- [ ] `host.getTheme()` 能把 C# token 传给 JS。

### 语义正确性
- [ ] 状态色只用于圆点、左边框、下划线、标签文字，无大面积背景。
- [ ] `blocked` 行只加左边框，不改整行底色。
- [ ] `disputed` 行使用 `bg-disputed` 底 + 左边框，两者同时存在。
- [ ] `fallback` 行使用 `state-warning` 的 `[备用]` 文字标签，不加行底色（v1.1 新增）。
- [ ] 急停按钮是全界面唯一纯白文字 + 大面积状态色背景。
- [ ] `none` 行动使用 `text-tertiary #808080`，不用占位符隐藏。

### 对比度
- [ ] `text-primary` 在 `bg-primary` 上对比度 ≥ 7:1。
- [ ] `text-secondary` 在 `bg-primary` 上对比度 ≥ 4.5:1。
- [ ] 状态色长文本不在 `bg-tertiary` / `bg-elevated` 上使用。

### 实现
- [ ] 自绘 `Graphics` 开 `AntiAlias` + `ClearTypeGridFit`。
- [ ] 呼吸动画用实色插值（`StateInfoDim`↔`StateInfo`），不用 alpha。
- [ ] 圆点半径和边框宽度乘 `scaleFactor`。
- [ ] 托盘图标提供 16/32/48 三套 PNG。
- [ ] 4.8 分支 PerMonitorV2 在 Win10 1703 以下退化为 System DPI（v1.1 新增）。

### 扩展性
- [ ] 颜色只通过 token 引用，无硬编码。
- [ ] `ITheme` 接口预留，浅色主题只需换值不改组件。

---

## 10. 一句话总结

**五层背景靠明度区分，四级文字靠对比度分层，六个状态色只做小面积语义点缀，事实层用灰阶+等宽，解释层用次级灰+斜体，急停是唯一允许纯白+大面积状态色的元素。** 所有颜色走 `Theme` 静态类 token，WinForms 自绘用实色插值做呼吸，CefSharp 用 CSS 变量引用同一套 token。这套配色不追求"好看"，追求的是**长时间盯着不累、状态一眼可辨、坏了也能看出哪里坏了**。
