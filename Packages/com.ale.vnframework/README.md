# 剧情演出框架（VN Framework）

面向策划的 Unity 视觉小说（Visual Novel / Galgame）**演出层**插件，建立在 Pixel Crushers Dialogue System 之上。
Dialogue System 负责对话数据、分支逻辑与 Lua 环境；本插件把「这句话时背景换成什么、谁站在哪、播什么动画、
出什么特效、放什么音」变成**对话节点上的字段条目**，配置全程在 Dialogue 编辑器窗口内完成。

- 演出配置**不写代码**：字段条目的 `Title` 指明用途、`Value` 用 `|` 分隔参数。
- 资源按**目录约定**加载，配置里只写资源名；可选经 Addressables 异步加载。
- 角色动画**后端无关**（Spine / Live2D / Unity 动画），且不挂动画组件的预制体也走同一套流程。

> 安装、环境要求、依赖顺序与整体介绍见[仓库根 README](../../README.md)，本文不再赘述。

---

## 功能概览

| 演出对象 | 字段条目（默认标题） | 内容值格式 | 说明 |
|---|---|---|---|
| **背景** | `Background` | 图片文件名 | 切换场景背景 |
| | `BackgroundFadeDuration` | 秒（默认 `0.3`） | 淡入淡出时长 |
| **角色** | `Actor1Prefab` ~ `Actor3Prefab` | `文件夹/预制体名` | 出场；置空则清除 |
| | `Actor1Pos` ~ | `X\|Y\|Z\|速度倍率` | 位置，可只填前几项 |
| | `Actor1Rotate` ~ | `X\|Y\|Z\|速度倍率` | 旋转 |
| | `Actor1Scale` ~ | `X\|Y\|Z\|速度倍率` | 缩放 |
| | `Actor1Anim` ~ | `Key1\|Key2\|Key3` | 动画状态 |
| **特效** | `Effect1Prefab` ~ `Effect3Prefab` | `文件夹/预制体名` | 同角色，四组条目一致 |
| | `Effect1Pos` / `Rotate` / `Scale` / `Anim` ~ | 同上 | |
| **头像** | `DialogueHead` | 图片文件名 | 对话框头像（图片） |
| | `DialogueHeadPrefab` | `预制体名称\|延迟` | 对话框头像（预制体），与 `DialogueHead` 二选一 |
| | `DialogueHeadAnim` | `Key1\|Key2\|Key3` | 预制体头像的动画状态 |
| **音频** | `AudioBGM1` ~ / `AudioAmbient1` ~ / `AudioSFX1` ~ / `AudioVoice1` ~ | `Key\|音量\|音调\|延迟` | 见[音频接缝](#音频接缝) |
| **文本** | `DialogueTypewriterSpeed` | 倍率 | 打字机速度 |

> 上表是**默认标题**，全部可在 `VnStoryManager` 上改。角色 / 特效 / 音频默认各 3 个槽位，
> 用 `[+] [-]` 增删即可支持更多同屏对象。逐项的图文说明见
> [VnStoryManager 使用文档](Docs~/VnStoryManager/VnStoryManager.md#剧情演出配置)。

### 字段条目机制

演出的全部输入都是 Dialogue System 原生的 **Field**，因此不需要为本插件学一套新编辑器：

- **`Title`（字段标题）** —— 告诉演出系统这条数据的用途。`VnStoryManager` 上以 `FieldTitle` 结尾的
  设置项保存了这些标题，改设置即改标题，互不写死。
- **`Value`（内容值）** —— 具体参数。多参数用 `|` 分隔，**可只填前几项，未填的用默认值**
  （位置默认 `0|0|0|1.0`，所以 `1|-14` 是合法的）。
- **`Type`（条目类型）** —— 演出条目一般用 `Text`；`Actor` / `Boolean` / `Localization` 用于
  Dialogue System 自身的角色、开关与多语言字段。

⚠️ Inspector 上方重复显示的**主要字段条目不要删除**——它们是 Dialogue System 的内置字段。

### 剧情演出（`VnStoryManager`）

演出核心单例（派生自 `ToolkitMonoSingleton`）。订阅 Dialogue System 的对话推进，逐节点解析字段条目，
驱动背景 / 角色 / 特效 / 头像 / 消息，并统一管理预制体的加载与卸载。

- **资源目录约定**：背景 / 角色 / 头像 / 头像预制体 / 特效各配一组「文件夹路径 + 扩展名」
  （`backgroundAddressableFolder` / `…Extension`、`actorAddressableFolder` / `…Extension`、
  `dialogueHeadAddressableFolder` / `dialogueHeadExtension`、
  `dialogueHeadPrefabAddressableFolder` / `dialogueHeadPrefabExtension`、
  `effectAddressableFolder` / `…Extension`），
  配置里只写资源名。路径以 `Assets/` 开头、以 `/` 结尾。
  加载经 `ToolkitAssets` 统一入口——开启 `ATK_ADDRESSABLE` 时走 Addressables 异步加载并回收句柄，
  否则回落 `Resources`。
  ⚠️ **这十个设置项本身包在 `#if ATK_ADDRESSABLE` 内，未开启该宏时不会出现在 Inspector 上。**
- **预制体生命周期**：角色与特效走**同一套**加载 / 定位 / 卸载流程。预制体设置一次持续存在，
  把 `Value` 置空才清除。
- **补间**：位置 / 旋转 / 缩放的第 4 个参数是速度倍率；背景切换可经 Dialogue System 的
  `StandardSceneTransitionManager` 做淡入淡出。
- **富文本与打字机**：支持富文本图标符号与打字机符号，可在逐字显示中插入停顿与表情图标。
- **多语言**：开启 `ATK_LOCALIZATION` 后，本框架把 Unity Localization 当前选中的**语言代码**
  同步给 Dialogue System（`Localization.language`）。真正的取值由 Dialogue System 自己完成，
  且只作用于**它自己的**字段——对白按**裸语言代码**字段查找（`ja` / `zh-Hans` …），
  其余字段按「标题 + 空格 + 语言代码」查找（`Display Name ja`，也支持下划线）。
  本框架的演出字段（`Background`、`Actor1Prefab` 等）不参与多语言。
  批量翻译走 Dialogue System 自带的 CSV 导出 / 导入，见
  [使用文档 · 多语言的导出与导入](Docs~/VnStoryManager/VnStoryManager.md#多语言的导出与导入)。

### 剧情启停（`VnStoryPlayer`）

挂在任意 GameObject 上，按对话名启停某段剧情。可在 Inspector 配置对话名与自动播放时机
（嵌套枚举 `AutoPlayTiming`），也可由 `Button.OnClick` 或脚本触发。

> ⚠️ **`AutoPlayTiming` 管的是「什么时候**开始**一段对话」**（`Manual` / `OnStart` / `OnEnable`），
> 与玩家侧那个「一句台词播完自动进下一句」的自动播放**不是一回事**——后者在
> [播放控制](#播放控制) 章节，由 `VnPlaybackController` 负责。
> 1.5.0 之前的文档把两者都叫「自动播放」，容易误读。

```csharp
using Ale.VnFramework;

VnStoryManager.Instance.StartVnStory("Chapter01/Prologue");
VnStoryManager.Instance.StopVnStory();                 // 默认清空演出数据
VnStoryManager.Instance.StopVnStory(clearAllData: false);

string[] names = VnStoryManager.Instance.GetAllConversationName();
```

要**一段接一段地连播**用 `PlaySequence`（1.6.4+）。段间的时序细节都在组件内部消化掉，
调用方只管给一个名称列表：

```csharp
player.PlaySequence(new[] { "Chapter01/Prologue", "Chapter01/Scene01" },
    onFinished: () => Debug.Log("开场剧情播完"));
```

前 N-1 段不收场（UI 与背景留在场上等下一段接手），最后一段按 `autoStopOnFinished` 收场——
于是连播一队与播一段的收尾表现完全一致。整队的 `onFinished` 只兑现一次。
`Stop()` 与组件被停用都会中止整队；详见 [`VnStoryPlayer` API](#vnstoryplayer)。

剧本里的主线通常是靠**跨会话链接**一路串到底的（序章 → 第一章 → …），所以默认会顺着
链接一直播下去。只想播点名的那几段时（如新游戏的开场剧情）传
`stopAtConversationBoundary: true`（1.6.5+），每段播到会话边界即止：

```csharp
player.PlaySequence(new[] { "序章：入职-1" }, stopAtConversationBoundary: true);
```

### 演出黑幕（1.6.6+）

一场演出的**开场与收场**各遮一层黑幕，把两段本来就不该被看见的过程盖住：开场时对话框会先于
背景与角色亮相、还要等演出资源陆续加载回来才铺齐；收场时对话框又会比宿主界面晚一步淡完，
半透明地浮在下一层界面上。

```csharp
// 开场：黑幕淡入 → 全黑后才开播 → 演出铺好了才揭幕
VnStoryManager.Instance.PlayStoryIntroTransition(() => player.Play("Chapter01/Prologue"));

// 收场：黑幕淡入 → 全黑后停演出、等它真正停妥 → 关界面 → 揭幕
VnStoryManager.Instance.PlayStoryOutroTransition(() => storyView.Close());
```

配置只有一步：在 `VnStoryManager` 的「演出黑幕」里接上一个 `CanvasGroup`。**留空则整套功能关闭**，
两个转场方法退化成直通，表现与没有本功能时完全一致。

⚠️ 两条摆放上的硬要求，不满足就等于没遮：

- 黑幕所在的画布必须是 **`ScreenSpaceOverlay` 且 `sortingOrder` 高于对话画布**。Dialogue System
  的对话画布正是 Overlay，永远盖在所有相机输出之上——宿主的 UI 分层若是 `ScreenSpaceCamera` /
  `WorldSpace`，排多前都盖不住对话框。
- 黑幕要挂在 **`VnStoryManager`（常驻）** 身上，不能挂在宿主界面上。收场时界面会先关掉，
  幕布跟着一起消失的话，遮了等于没遮。

**连播不会插进黑幕**：转场由宿主界面的开与关驱动，而连播全程界面既不开也不关。详见
[演出黑幕 API](#演出黑幕)。

### 角色动画对接（`VnActorAnimator`）

演出层与动画系统之间的唯一接缝。组件只持有 `com.ale.animsimulatorsystem` 的 **`AnimatorBase`**，
因此 Spine / Live2D / Unity 动画对演出层完全一致；Inspector 栏名为 **Actor Animator**，
留空时经 `AnimatorBase.FindFor(this)` 自动获取。

- **就绪门控** —— 播放、状态切换、皮肤接口一律排在动画器初始化完成之后执行，**不依赖帧序**。
  外部调用无需关心组件是否已 `Start`。
- **单条播放与回调** —— 支持按名播放单条动画（可配循环 / 倒放 / 速度 / 延迟）并在播完时回调，
  以及按名停止。
- **状态与皮肤** —— 状态数组切换、换装皮肤的读写。
- **初始状态**配在动画器组件（`AnimatorBase.StateInitList`）上；对话节点的 `ActorNAnim` 条目
  **缺失**时沿用预制体自身初始状态，**为空**时才清空。

> ⚠️ `SwitchStateArray(空数组)` 会隐藏角色——这是 `AnimatorBase` 把可见性绑在状态使用计数上的既有语义。

### 无动画组件的预制体降级

角色与特效本质上是同一套预制体流程，因此**不挂 `VnActorAnimator` 的预制体同样可用**：
自动播放的粒子特效、单张图片的预制体都能正常实例化与销毁。

- 加载时若未找到 `VnActorAnimator`，走降级路径（仅记一条 Log，不报警告）。
- 卸载时对纯粒子预制体先 `Stop(StopEmitting)`，按最大 `startLifetime` 延迟销毁，让粒子自然消散。
- 三条降级路径都会正常销毁实例并释放 Addressable 句柄，不残留、不泄漏。

> 限制：位移 / 旋转 / 缩放的补间**速度与缓动参数序列化在 `VnActorAnimator` 上**，
> 没有该组件就没有参数，因此降级路径为瞬间置位而非补间。

### 分支选项（`VnResponseButton`）

派生自 Dialogue System 的 `StandardUIResponseButton`，在原有行为上叠加**已读 / 未读态**表现：
按状态切换文本颜色、按钮颜色、提示对象显示与图片颜色。已读状态存在 Dialogue System 的 Lua 变量里，
可用于实现「阅读所有选项后才解锁」这类玩法，见
[功能示例 - 阅读所有选项](Docs~/VnStoryManager/VnStoryManager.md#阅读所有选项)。

### 音频接缝

音频后端是**可替换的**：`VnStoryAudio` 是静态门面，真正干活的是
`VnStoryAudio.Backend`（类型 `IVnAudioBackend`）。默认为 `NullVnAudioBackend`——全空操作，
不报错、不出声，演出流程照常推进。

#### 接入自己的音频系统

**只需实现四个原语，不需要定义任何编译宏，也不需要改动本包源码：**

```csharp
using Ale.VnFramework;
using UnityEngine;

public sealed class MyAudioBackend : IVnAudioBackend
{
    public void PlayWithChannel(EVnAudioCategory category, string channelName, string audioKey, float volume, float pitch) { }
    public void StopWithChannel(EVnAudioCategory category, string channelName) { }
    public void Play(EVnAudioCategory category, string audioKey, float volume, float pitch) { }
    public void Stop(EVnAudioCategory category, string audioKey) { }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install() => VnStoryAudio.Backend = new MyAudioBackend();
}
```

`audioKey` 就是剧本节点上配的那串字符串，**怎么解析成实际资源完全由后端决定**。
`EVnAudioCategory` 为 `Bgm` / `Ambient` / `Sfx` / `Voice`，可据此路由到不同的 Mixer 组或做独立音量控制。

> **演出语义不用你操心。** 解析 `Key|音量|音调|延迟`、延迟播放、切换对话行时停掉上一行的音效、
> BGM 值为空即停该通道、跨行去重——全部由 `VnStoryManager` 负责。后端只管「播 / 停」这四件事。
> 这也是扩展点开在这一层、而不是开在字段级的原因：开在字段级，每个接入者都得把上面这些重写一遍。

#### 内置的 Fs GameFramework 后端

`FsVnAudioBackend` 由 `VNS_FS_GAMEFRAMEWORK` 宏门控（宏关闭时整个文件不参与编译，本包对 Fs 零依赖），
在**欢迎窗口**的「插件支持（编译宏）」里勾选即可（`Tools → Ale Toolkit → VN Framework → Welcome`），
启动时经 `[RuntimeInitializeOnLoadMethod]` 自动注册。它同时也是一份**接入范例**。

所需的程序集引用（`Fs.GameFramework.Common.AudioSystem` 与 `Fs.Utility`）已常驻在
`Runtime/Ale.VnFramework.asmdef` 里，**无需手工添加**——Unity 对按名字解析不到的 asmdef 引用
是静默跳过的，没装 Fs 的工程不会因此收到任何警告。

> ⚠️ 开启后还需要在 Fs 的音频系统里配置好 `AudioLibrary`，否则会逐条报
> `AudioEntry with key '...' not found` 且依然没有声音——宏只负责接通调用链，音频资产要自行准备。
>
> 显式赋值优先于自动注册：`Install()` 发现 `VnStoryAudio.IsAvailable` 已为 true 时不会覆盖你的后端。

### 对外扩展点

```csharp
// 按字段标题接管演出流程：节点上出现该标题的条目时，回调收到其内容值
VnStoryManager.Instance.RegisterGameplaySystem("QuestUnlock", value => { /* 自定义玩法 */ });
VnStoryManager.Instance.UnregisterGameplaySystem("QuestUnlock");

// 把宿主变量同步进 Dialogue System 的 Lua 环境（供对话条件 / 分支使用）
VnStoryManager.Instance.RegisterVariableGetter("PlayerLevel", () => player.Level);
VnStoryManager.Instance.SetAllVariablesToDialogueSystem();
```

`RegisterGameplaySystem` 接受**任意**字段标题，不限于内置白名单——这是把任务系统、好感度系统等
接进剧情流程的入口。

---

## 条件系统

对话节点的 **Conditions** 决定「能不能进这个节点」。除了手写 Lua（如 `Variable["选项序号"] == 1`），
本包还把 **Ale Toolkit 条件系统**的判定器接了进来：任何系统只要实现了判定器，就会**自动出现**在
节点的 Conditions 配置里，不需要改本包一行代码，也不需要改 Dialogue System 一行代码。

### 三步接入

**1. 生成登记资产**（每个工程一次）——菜单
`Tools ▸ Ale Toolkit ▸ VN Framework ▸ Generate Condition Lua Functions`，
会在 `Assets/AleVnFramework/` 下建一份 `VnFrameworkConditionLuaFunctions.asset`。

> 为什么非要有它：Dialogue System 的条件向导枚举可选函数走的是**扫描工程内的
> `CustomLuaFunctionInfo` 资产**，它**不读 Lua 环境**——没有这份资产，判定器一个都不会出现在下拉里。
>
> 建好之后不用再管：此后判定器增减，资产会在**脚本重编译时自动同步**（内容无变化则一个字节都不写）。

**2. 接线数据源**（宿主侧）：

```csharp
void OnEnable()
{
    VnConditionSources.RegisterNumber("时间段", () => timeSystem.CurrentHour);
    VnConditionSources.RegisterFlag("已见过面", () => flags.Has("met"));
}

void OnDisable()
{
    VnConditionSources.UnregisterNumber("时间段");
    VnConditionSources.UnregisterFlag("已见过面");
}
```

> 除了 number / flag 两种取值器，还能按接口登记整个服务：
> `VnConditionSources.RegisterService<IMyService>(impl)` —— 判定器经 `ctx.GetService<IMyService>()` 取用。
> 本包自带的 `Vn.StoryChoiceIs` 就是这么拿数据的（见下文「内置判定器」）。

**3. 在节点上配置**：Conditions 右侧点 `...` 打开向导 → 条件类型选 **Custom** →
在 `Ale 条件` 分组里挑判定器 → 填参数。生成的表达式形如：

```lua
AleCond_Condition_NumberCompare("时间段", "大于等于", 3) == true
```

> ⚠️ 向导只在**打开的那一刻**扫一次资产。新判定器没出现的话，把向导关掉重开。

### 两条必须知道的语义

**① 数值 / 标记只来自宿主的实时 getter，不读 Dialogue System 的 Variable。**

求值当刻才回调宿主，所以**对话中途**宿主的值变了会立刻生效。这一点与
[`RegisterVariableGetter`](#对外扩展点) 不同——后者只在**每段对话开始时**推一次
（`SetAllVariablesToDialogueSystem`），对话进行中取到的是旧值。

既有的 `Variable["…"]` 写法完全不受影响，两者可以在同一个表达式里组合：

```lua
AleCond_Condition_NumberCompare("时间段", "大于等于", 3) == true
    and Variable["选项序号"] == 1
```

**② 没接线的 id 一律判「不成立」，并在控制台告警一次。**

取不到数值时返回 `NaN`，于是 `>` `≥` `=` `≤` `<` 五种比较**全部为假**——「忘了接线」表现为
节点进不去，而不是静默放行（若回落成 `0`，`时间段 ≤ 5` 这类条件会悄悄通过）。
告警按 id 去重，不会刷屏。

### 内置判定器：`Vn.StoryChoiceIs`（1.6.2+）

判断**某个分支点当时选了第几项**。剧情走向本就是 VN 最常用的门槛，本包直接提供：

| 参数 | 类型 | 说明 |
| --- | --- | --- |
| `dialogue` | String | 分支点的对话编号（剧本里那串数字），**不含变量名前缀** |
| `op` | Int | 比较符下拉（`大于` / `大于等于` / `等于` / `小于等于` / `小于`），复用 toolkit 的 `ConditionCompare` |
| `index` | Int | 选项序号，**从 1 开始** |

数据来自宿主实现并注册的 `IVnStoryChoiceSource`：

```csharp
public class MyStorySave : IVnStoryChoiceSource
{
    // 约定：入参不含前缀；序号 1 起；没选过 / 查不到一律返回 0
    public int GetChoice(string dialogueNumber) => _choices.TryGetValue(dialogueNumber, out var v) ? v : 0;
}

VnConditionSources.RegisterService<IVnStoryChoiceSource>(myStorySave);
```

> **为什么不直接读对话变量**：变量只是运行时的一份工作副本——回放 / 试玩会污染它、切场景会重置它、
> 读档要靠推送才同步。哪一次选择「算数」由宿主的存档说了算，所以由宿主把权威值交出来。
>
> 因为「没选过 = 0」，`等于 0` 天然表达「这个分支点还没做过选择」，`大于等于 1` 表达「做过任何选择」。
> 没注册数据源时**一律不成立**并告警一次，不会当作「没选过」放行。

同一个判定器在对话库与外部系统（如节点树的解锁条件）里通用 —— 前者经 `AleCond_Vn_StoryChoiceIs`
桥接，后者直接把判定器写进 `ConditionExpression`。

### 命名规则

Lua 函数名 = `AleCond_` + 判定器 Key（非 `[A-Za-z0-9_]` 的字符换成 `_`）：

| 判定器 Key | Lua 函数名 | 向导里的位置 |
| --- | --- | --- |
| `Condition.NumberCompare` | `AleCond_Condition_NumberCompare` | `Ale 条件/Condition/…` |
| `AnimSim.LevelProgress` | `AleCond_AnimSim_LevelProgress` | `Ale 条件/动画模拟器/…` |

> 名字刻意保留**完整的 Key**、也刻意**只用 ASCII**：这些字符串会存进你的对话库，命名规则一旦改动，
> 已存的条件就会静默失效；而菜单的叶子段同时就是真实的 Lua 标识符（Dialogue System 生成调用时
> 只取最后一段），中文标识符不值得赌。

### 参数映射

Dialogue System 的参数类型只有 Bool / Double / String / 数据库实体，**没有通用枚举下拉**，故折中如下：

| Toolkit 参数 | 映射为 | 剧本里写成 |
| --- | --- | --- |
| `String` | String | `"文本"` |
| `Float`、无选项的 `Int` | Double | `3` |
| **带固定选项**的 `Int` / `Enum` | **String（传标签）** | `"大于等于"` |
| 无选项的 `Enum` | String（成员名） | `"Morning"` |
| `Bool` | Bool | `true` |
| 数组 | String（以 `\|` 分隔） | `"a\|b\|c"` |

标签拼错会告警并判不成立，且告警里会列出全部合法选项。也接受直接写数字索引。

### 限制

- **求值是同步的**。Dialogue System 的 `Lua.IsTrue` 没有异步扩展点，判定器里不能等 IO；
  需要异步取的数据请先算好，再由 getter 返回。
- **参数不能超过 8 个**，超了会被跳过并告警。Dialogue System 的 Lua 绑定要求实参个数与 C# 方法
  签名严格一致（不支持变参与默认值），只能按元数逐个准备方法。
- 因此**别手改剧本里的实参个数**，会抛 `TargetParameterCountException`；增删参数请用向导重新生成。
- 需要 `com.ale.toolkit` ≥ 1.4.0（本包本就要求 ≥ 1.7.10，正常都满足）。toolkit 若被降级到没有
  条件系统的版本，本功能连同它的两个程序集一起**静默消失**，不会让工程编译失败。

---

## 播放控制

文字冒险游戏右下角那排按钮：**自动播放、播放速度、快进、新对话停止、隐藏 UI**。
核心是 `VnPlaybackController`，与 `VnStoryManager` 挂在同一个物体上；按钮用 `VnPlaybackButton`。

样例里已经接好了（`DialogueUI_Main` 预制体右下角），导入 Sample 进 Play 即可直接用。

### 五个功能

| 功能 | 默认 | 行为 |
| --- | --- | --- |
| 自动播放 | **关** | 开启后，一句台词「演完」自动进下一句。演完 = 打字机显示完 **且**（若配了语音）语音播完，再等一个可配的停留时长（默认 1 秒）。 |
| 播放速度 | **1x** | 点一下在 1x → 2x → 3x → 1x 之间循环。**只影响台词打字机**（字速与标点停顿）。 |
| 快进 | — | **长按**进入、松开退出。倍率**分两条**、各自可配：打字机默认 **30x**，补间 / 延时 / 角色 Animator / 粒子 / 语音默认 **5x**。 |
| 新对话停止 | **开** | 快进中遇到**从未出现过**的对话节点时中止快进。 |
| 隐藏 UI | — | 点一下藏起全部界面，再点屏幕任意位置恢复。 |

### 三条必须知道的语义

**① 快进隐含自动推进。** 需求上没写，但一个还要逐句点击的「快进」不成立：快进态下无论自动播放
开关如何都会自动推进，且停留时长按倍率压缩。

**② 快进被「新对话停止」打断后，按着不放不会自行恢复**，必须**松开再按**。
否则玩家一路按住会在每个未读行抖动式地一停一走，等于没停。这个中间态在代码里是
`EVnFastForwardState.Suppressed`，Inspector 与日志里都看得见。

**③ 快进与速度档取 `Max`，不是相乘。** 相乘会让 3 档速的玩家按下快进拿到 90 倍；
纯覆盖又会在「快进打字机倍率 2、档位 3」时**反而变慢**。`Max` 保证单调不减，默认值（30 > 3）下等价于覆盖。

> 快进的两条倍率各管各的：打字机取 `FastForwardTypewriterRate`（默认 30），
> 其余演出取 `FastForwardRate`（默认 5）。分开是因为快进时台词是唯一需要「一眼扫过」的东西，
> 而演出跟着 30 倍走只会糊成一片。

### 已读记录

「新对话停止」要知道哪些台词读过。这一层**直接复用 Dialogue System 的 `SimStatus`**
（逐节点三态，由它在行准备时自动打标，运行时零维护成本），本包只负责**紧凑持久化**。

> ⚠️ **必须勾上 Dialogue Manager 的 `Include SimStatus`**（样例预制体已勾）。
> 不勾时 `DialogueLua.GetSimStatus` 对任何节点都返回 `Untouched`，于是**每一行都被判成未读**，
> 表现为「快进一按就停」。本包会在运行时检测并补开 + 告警一次，但不如直接勾上。

存档编码（`VnReadHistoryCodec`）：每个节点 **2 bit**，按会话打包成位图再 Base64；
整个会话状态一致时**塌缩**成「会话 ID + 容量 + 状态」共 9 字节。1 万行剧本约 **2.4–4.4 KB**，
而 DS 自带的 `SimX` 字符串格式同规模是 60–80 KB。

留 2 bit 而不是 1 bit，是为了保住 `WasOffered` 与 `WasDisplayed` 的区别——
DS 的 `emTagForOldResponses`（把选过的选项置灰）靠它工作。

### 存档接口

**本包只提供 Get / Set，不做 Load / Save**——落盘归宿主的存档系统。

```csharp
using Ale.VnFramework;

// 存档时
VnStorySaveData data = VnStoryManager.Instance.GetSaveData();   // 深拷贝
string json = JsonUtility.ToJson(data);                          // 或 ES3 / 自定义二进制

// 读档时
VnStoryManager.Instance.LoadSaveData(JsonUtility.FromJson<VnStorySaveData>(json));
// 1.6.1 起按钮图标由 LoadSaveData 自动刷新，不必再手动调 NotifyStateChanged()

// 开新游戏
VnStoryManager.Instance.ResetAll();
```

沿用 `com.ale.toolkit` 的 `ISaveable` 四条契约：**取时深拷贝**、**载入是覆盖而非合并**、
**容忍 `null` 与脏数据**、**三个方法都不触发变更事件**。

> 最后一条指的是**玩法**事件，为的是读档时不层层级联。**1.6.1 起纯视图刷新是个例外**：
> `LoadSaveData` 与 `ResetAll` 会自动调一次 `Playback.NotifyStateChanged()`，
> 按钮图标不再需要调用方手动刷新。该事件只被按钮用来换图，不会回流进任何玩法逻辑。
> 直接调底层的 `Playback.ApplySettings()` / `ResetToDefaults()` 则仍需自己刷。

> 本类只实现非泛型的 `ISaveable`，不实现 `ISaveable<TState>`——后者是 `List<TState>` 形状、
> 为集合型系统设计，而这里是「一个设置块 + 一份已读位图」的单体聚合。

> **覆写点（1.6.0 起）**：三个方法均为 `virtual`。`VnStorySaveData.choices`
> （分支选择：`List<VnStoryChoiceData>`，变量名 → 取值）由宿主子类在覆写里采集与回填——
> 哪些变量算「剧情选择」是宿主剧本约定的事，基类不填不读；按名字寻址，不受剧本指纹校验约束。
> 配套钩子 `OnConversationChoiceSelected(Subtitle)` 在玩家选中分支选项、其 Script 执行完毕后触发。
> ⚠️ `LoadSaveData(null)` 会调 `ResetAll()`——覆写 `ResetAll` 时不要反过来调 `LoadSaveData`，会成环。

### 接入自己的 UI

不想用样例那套按钮的话，直接调控制器即可，方法都能挂到 `Button.OnClick`：

```csharp
var playback = VnStoryManager.Instance.Playback;

playback.ToggleAutoPlay();
playback.CycleSpeedTier();
playback.ToggleStopOnUnread();
playback.BeginFastForward();   // 按下时
playback.EndFastForward();     // 松开时（务必成对，否则倍率会一直卡住）
playback.StateChanged += RefreshMyIcons;
```

**快进必须自己保证 `Begin` / `End` 成对**。样例的 `VnPlaybackButton` 为此做了三重兜底：
指针移出按钮、组件被禁用、对话结束，都会强制收回。

### 语音：可选后端能力

自动播放要等语音播完、快进要让语音倍速，这两件事需要后端支持
**可选接口 `IVnAudioPlaybackInfo`**（查询是否在播 + 设置倍速）。
后端不实现也不会报错，只是自动播放退化为「打字机结束 + 停留时长」、快进时语音按常速播，
并在首次需要时告警一次。

内置的 Fs 后端已经实现了它（需要 `com.fs.gameframework` ≥ 0.9.4）。

### 限制

- **Spine / Live2D 的状态动画吃不到倍率**。`AnimatorBase` 没有实时时间缩放 API，速度在起播时
  就被烘进后端的轨道项了。Unity `Animator.speed` 与粒子 `simulationSpeed` 都正常跟随。
  补齐的位置在 `com.ale.animsimulatorsystem`，不在本包。
- **剧本 `Sequence` 里手写的 `Delay()` / `AudioWait()` 不会被压缩**。本包只逐项调自己的补间与延时，
  不接管 Dialogue System 的全局时钟 `DialogueTime`（那是个静态量，一旦驱动中断会让 DS 时间冻结、
  对话卡死，代价太大）。
- **已读记录以「会话 ID + 节点 ID」为下标**，剧本被**整库重导入**（Chat Mapper / articy / CSV）
  会让 ID 重编号、记录错位。存档里带了剧本结构指纹，不匹配时会**丢弃记录并告警**，而不是静默用错。
  日常的追加剧情、删节点不受影响——DS 分配节点 ID 用 `max + 1` 且不回收。
- **中途改字速会跳字**，这是 DS 打字循环的行为（`goal = elapsed × cps`，`elapsed` 从行首累计）。
  本包只在行边界写字速，并在进入快进时先把当前行 `Stop()` 掉，正常使用中看不到。
- **没有 `EventSystem` 时隐藏 UI 会被拒绝**并报错——藏起来之后点不了任何东西，那是软锁。

---

## UI 样式

样例中的 `DialogueUI_Main` 预制体给出了一整套可直接改的对话 UI：玩家对话面板与配角对话面板、
分支选项面板、消息提示面板，以及继续按钮的 UI 动画。逐项说明见
[使用文档 - UI样式](Docs~/VnStoryManager/VnStoryManager.md#ui样式)。

---

## 配置流程

完整图文步骤见 [VnStoryManager 使用文档](Docs~/VnStoryManager/VnStoryManager.md)，此处为脉络：

1. **剧情演出管理器预制体** —— 在 Project 面板的预制体源文件上改配置（不是场景实例），
   配好五组资源「文件路径与类型」和各类「字段条目的标题」。二者都有可用默认值，
   [通常无需修改](Docs~/VnStoryManager/VnStoryManager.md#资源配置)。
2. **资源导入** —— 按类别放进约定目录：
   [背景图片](Docs~/VnStoryManager/VnStoryManager.md#背景图片的导入)、
   [角色头像](Docs~/VnStoryManager/VnStoryManager.md#角色头像图片的导入)、
   [角色](Docs~/VnStoryManager/VnStoryManager.md#角色的导入)（含
   [角色动画预制体](Docs~/VnStoryManager/VnStoryManager.md#角色动画预制体)的制作）、
   [特效](Docs~/VnStoryManager/VnStoryManager.md#特效的导入)、
   [音频](Docs~/VnStoryManager/VnStoryManager.md#音频的导入)、
   [富文本图标](Docs~/VnStoryManager/VnStoryManager.md#富文本图标的导入)。
3. **剧情演出配置** —— 在 Dialogue 编辑器里编排
   [角色](Docs~/VnStoryManager/VnStoryManager.md#角色)、
   [对话节点](Docs~/VnStoryManager/VnStoryManager.md#对话节点)、
   [分支对话节点](Docs~/VnStoryManager/VnStoryManager.md#分支对话节点)，
   并在节点上添加演出用的字段条目。
4. **触发播放** —— 挂 `VnStoryPlayer` 或直接调 `VnStoryManager.Instance.StartVnStory(...)`。

---

## 目录结构

```
com.ale.vnframework/
├── Runtime/
│   ├── Ale.VnFramework.asmdef   运行时程序集
│   ├── VnStoryManager.cs        演出核心：字段条目解析、背景/角色/特效/头像/消息、
│   │                            预制体生命周期、全局变量、玩法扩展点、
│   │                            会话围栏、演出黑幕
│   ├── VnStoryPlayer.cs         剧情启停：按对话名启停、多段连播、自动播放时机（≠ 逐句自动推进）
│   ├── VnActorAnimator.cs       角色动画对接层（→ AnimatorBase，就绪门控）
│   ├── VnResponseButton.cs      分支选项按钮（已读 / 未读态）
│   ├── Audio/                   可替换的音频后端
│   │   ├── IVnAudioBackend.cs   ← 接入自己的音频系统实现这个
│   │   ├── EVnAudioCategory     （同文件）Bgm / Ambient / Sfx / Voice
│   │   ├── IVnAudioPlaybackInfo.cs 可选能力：查询是否在播 + 设置倍速（自动播放等语音、快进倍速语音）
│   │   ├── VnStoryAudio.cs      静态门面，持有当前后端
│   │   ├── NullVnAudioBackend.cs 默认空实现
│   │   └── FsVnAudioBackend.cs  Fs 后端（VNS_FS_GAMEFRAMEWORK 门控，兼作范例）
│   ├── Playback/                播放控制：自动播放 / 倍速 / 快进 / 新对话停止 / 隐藏UI
│   │   ├── VnPlaybackController.cs 状态机与自动推进协程（与 VnStoryManager 同物体）
│   │   ├── VnPlaybackRate.cs    倍率权威。打字机倍率与演出倍率分开，理由见文件注释
│   │   ├── VnTween.cs           ToolkitTween 的倍率感知包装
│   │   │                        ⚠ 在途补间用 Kill(false) 重起而非 Complete()——后者会同步
│   │   │                        触发完成回调，把 StopStoryConversation / 角色出场提前引爆
│   │   ├── IVnPlaybackRateReceiver.cs 可选契约：预制体自行跟随倍率
│   │   ├── VnReadHistory.cs     已读记录：SimStatus 读写、惰性水合、剧本指纹
│   │   ├── VnReadHistoryCodec.cs 2bit/节点 位图 ⇄ Base64（纯函数，无 Unity 依赖）
│   │   ├── VnPlaybackSaveData.cs 存档 DTO
│   │   ├── VnPlaybackButton.cs  按钮视图：开/关图切换、快进长按
│   │   └── VnUiHider.cs         隐藏UI + 运行时自建的全屏点击捕获器（独立画布）
│   └── Condition/               Toolkit 条件系统接入（独立程序集，按 toolkit 版本自动门控）
│       ├── Ale.VnFramework.Condition.asmdef
│       │                        versionDefines 从 com.ale.toolkit≥1.4.0 推出 VNS_HAS_CONDITION
│       ├── VnConditionBridge.cs 把判定器注册成 DS 的 Lua 条件函数
│       │                        ⚠ 必须晚于 SubsystemRegistration：DS 在那个时机会清空
│       │                        整个 Lua 环境（含已注册函数），故用 BeforeSceneLoad
│       ├── VnConditionSources.cs ← 宿主在这里接线数值 / 标记 / 领域服务
│       ├── VnConditionContext.cs 求值上下文；取不到数值时返回 NaN 以「失败即不成立」
│       ├── VnConditionLuaBinding.cs Eval0..Eval8 一族定长入口 + 参数编组
│       └── VnConditionNaming.cs 判定器 Key ↔ Lua 函数名，运行时与编辑器共用
├── Editor/                      欢迎窗口与编译宏开关
│   ├── Ale.VnFramework.Editor.asmdef  核心编辑器程序集
│   │                            ⚠ 刻意不引用 Ale.VnFramework：运行时程序集编译失败时
│   │                            （Dialogue System 未补 asmdef），本程序集仍需存活，
│   │                            否则菜单项消失、使用者恰好看不到那句「怎么补 asmdef」
│   ├── VnFrameworkWelcomeWindow.cs   前置条件自检 + 插件支持 + 演示样例 + 文档入口
│   ├── VnFrameworkDefines.cs         VNS_FS_GAMEFRAMEWORK 宏名与依赖探测
│   ├── VnFrameworkDefineChecker.cs   启动时按需弹窗 + 宏与运行时一致性告警（绝不改写宏）
│   ├── VnFrameworkEditorPrefs.cs     EditorPrefs / SessionState 键
│   ├── Addressables/                 样例条目一键登记
│   │   ├── Ale.VnFramework.Addressables.Editor.asmdef
│   │   │                        ATK_ADDRESSABLE + VNS_HAS_ADDRESSABLES 双重门控
│   │   └── VnFrameworkDemoAddressables.cs  经静态 Action 钩子注入欢迎窗口
│   ├── Condition/                    条件函数登记资产的生成与自动同步
│   │   ├── Ale.VnFramework.Condition.Editor.asmdef
│   │   └── VnConditionLuaFunctionInfoGenerator.cs
│   │                            菜单首次创建（征得同意）+ DidReloadScripts 同步
│   └── L10n/                         编辑器界面的英 / 日译表
├── Docs~/                       Unity 不导入（~ 后缀）
│   ├── Setup/PixelCrushers/     ⚠ Dialogue System 的 asmdef 副本与说明
│   └── VnStoryManager/          使用文档、截图与演示视频
└── Samples~/VN Framework Demo/  演示 Sample
    ├── VnStorySamples.unity     示例场景
    ├── VnStoryManagerBase.prefab
    ├── Data/StoryDatabase.asset 剧情库
    └── Assets/                  Actors(Spine) / ActorsHead / Backgrounds / Effects /
                                 Emoji / Prefab / TextTable / UI
```

---

## 详细文档

- **[VnStoryManager 使用文档](Docs~/VnStoryManager/VnStoryManager.md)** —— 资源配置、资源导入、
  剧情演出配置、UI 样式、功能示例的逐项图文说明（含演示视频）。
- [为 Dialogue System 补 Assembly Definitions](Docs~/Setup/PixelCrushers/README.md) —— 安装前置条件的
  操作步骤与原理（**不做则本包编译不过**）。
- [更新日志](CHANGELOG.md) ｜ [许可](LICENSE.md)

---

## API 参考

> **本章仅供二次开发参考。** 只做剧情配置的话不需要读——演出全部由对话节点上的字段条目驱动，
> 上面的章节已经够用。这里列的是从 C# 侧调用框架时的公开接口。
>
> 除条件系统外，全部类型位于命名空间 `Ale.VnFramework`，程序集 `Ale.VnFramework`；
> 条件系统另在命名空间 `Ale.VnFramework.Conditions`、程序集 `Ale.VnFramework.Condition`
> （独立程序集，按 toolkit 版本自动门控，见 [条件系统](#条件系统)）。
> 包内**没有任何 C# `event`**，也**没有任何 `[Obsolete]` 成员**。
> `VnActorAnimator` 与 `VnResponseButton` 的配置项**全是 `[SerializeField] private`**，只能在 Inspector 配。

### `VnStoryManager`

```csharp
public class VnStoryManager : ToolkitMonoSingleton<VnStoryManager>
```

演出核心单例。静态入口 `VnStoryManager.Instance` 与 `VnStoryManager.IsQuitting` 由基类
（`com.ale.toolkit` 的 `ToolkitMonoSingleton<T>`）提供。

⚠️ **`Instance` 不会惰性创建**：场景里没有 `VnStoryManager` 组件跑过 `Awake` 时它是 `null`。
退出播放模式时 `Instance` 不会变 `null`，所以拆卸路径上要先查 `IsQuitting`（`VnStoryPlayer.Stop()` 正是这么做的）。

| 成员 | 说明 |
|---|---|
| `void StartVnStory(string conversationName = null, Action onFinished = null, bool autoStopOnFinished = true, bool skipStartEntryConditions = false)` | 开始演出。UI / 背景 / 角色的**淡入只做一次**（重复调用会跳过），但传入的对话名**始终生效**——已在播放时再调一次即切到新对话（此时上一次登记的 `onFinished` 会被顶掉并告警）。<br>`onFinished`：该段对话播放完成后触发；**自然播完与中途 `StopVnStory` 都会触发**，两者经 DS 同一个 `OnConversationEnd` 收口、框架无从区分。经 `OnConversationEnd` 派发，覆写该方法的子类**必须调 base**。<br>`autoStopOnFinished`（1.6.1 起，默认 `true`）：播完后自动 `StopVnStory()`。回调里若接着开了下一段对话则自动跳过，不会把新剧情立刻淡出。传 `false` 用于连播。<br>`skipStartEntryConditions`（1.6.2 起，默认 `false`）：跳过**入口节点的条件判定**。会话的 START 指向首个真实节点，那个节点的条件顺带成了「本会话的准入门」——条件不成立时（`falseConditionAction` 为 Block）START 没有任何有效出边，`StartConversation` 原地返回，**一行都不播、也不发 `OnConversationEnd`**，调用方只看到「点了没反应」。传 `true` 改为直接以首个真实节点为入口开播，用于**准入已由外部判定**的场景（剧情回顾等）。只影响入口这一步，开播后各出边上的条件照常求值。 |
| `void StopVnStory(bool clearAllData = true, Action onComplete = null)` | 停止演出。⚠️ 停对话发生在 UI 淡出的**完成回调**里，不是同步；且仅当 `clearAllData` 为 true 时才停。<br>`onComplete`（1.6.6+）：收场**真正完成**（对话已停、UI 已淡到全透明）后触发——需要「等它彻底停妥」的调用方不必自己数帧。本来就没在演出时立刻兑现。 |
| `string[] GetAllConversationName()` | 取剧情库中全部对话名。 |
| `void RegisterGameplaySystem(string fieldTitle, Action<string> callback)` | 按**任意**字段标题接管演出流程：节点上出现该标题且值非空时，回调收到其 `Value`。重复注册会覆盖并告警。 |
| `void UnregisterGameplaySystem(string fieldTitle)` | 注销。传空值时静默忽略。 |
| `void RegisterVariableGetter(string variableName, Func<object> valueGetter)` | 把宿主变量同步进 Dialogue System 的 Lua 环境。⚠️ **没有反注册方法**；重复注册会覆盖并告警。 |
| `void SetAllVariablesToDialogueSystem()` | 立刻把已注册的变量全部推给 Dialogue System。每段对话开始时会自动调用一次。 |
| `float backgroundFadeDuration` | 公开字段，背景淡入淡出时长（秒），默认 `0.3`。 |
| `string ConversationResponseButtonVariableIsReadFieldTitle { get; }` | 只读。选项「是否已读」所用的 Lua 变量名，`VnResponseButton` 读它。 |

剧本侧还可用 Lua 函数 `BackgroundFadeDuration(duration)` 在对话中改背景淡入淡出时长（非 C# API）。

### 会话围栏

只播点名的那几段、不跟着跨会话链接流出去。`PlaySequence` 的 `stopAtConversationBoundary`
用的就是它，也可以直接调用来圈定任意一组会话。

| 成员 | 说明 |
|---|---|
| `void SetConversationFence(IList<string> conversationNames)` | 设围栏：演出只允许留在这些会话里。传 null 或空集合等同于撤销。 |
| `void ClearConversationFence()` | 撤围栏，恢复「跟着链接一路播下去」的默认行为。 |
| `bool HasConversationFence { get; }` | 当前是否设了围栏。 |
| `bool IsOutsideConversationFence(Subtitle subtitle)` | 这一行是否越出围栏。没设围栏时恒为 `false`。 |

- 越界的那一行会在 `OnConversationLine` 最前面被拦掉并 `StopVnStory()`，**不会播出来**。
- ⚠️ 但它**已经被 Dialogue System 走到并标记了已读状态**。覆写 `OnConversationLine` 的子类
  若在 base 之后还有自己的记账（已读、解锁之类），应当**先查 `IsOutsideConversationFence`**
  再记，否则会凭空记下一段玩家没看到的剧情。需要零副作用的场景请自行做状态快照。
- 围栏在 `StopVnStory` **淡出结束后**才撤（不是方法一进门就撤）：那半秒里对话还没真正停，
  过早撤销会让这段窗口里的行变成没围栏的普通行。

### 演出黑幕

开场与收场各遮一层黑幕（1.6.6+）。摆放要求与概览见「功能概览 · 演出黑幕」。

| 成员 | 说明 |
|---|---|
| `void PlayStoryIntroTransition(Action onBlackout, Action onComplete = null)` | **开场转场**：黑幕淡入到全黑 → 全黑时执行 `onBlackout`（调用方在此开播）→ 等 `IsStoryPresentationReady` → 黑幕淡出。 |
| `void PlayStoryOutroTransition(Action onStopped, Action onComplete = null)` | **收场转场**：黑幕淡入到全黑 → 全黑时 `StopVnStory()` 并**等它真正停妥** → 执行 `onStopped`（调用方在此关闭宿主界面）→ 等一帧 → 黑幕淡出。<br>停演出这一步由本方法自己做：它是异步的（UI 要淡出约半秒），这个等待没理由让每个调用方各写一遍。 |
| `bool IsStoryPresentationReady { get; }` | 演出是否已经「铺好」：UI 淡入已**走完** 且没有演出资源在加载中。<br>刻意**不**要求「对话处于激活状态」——会话被入口条件拦下时它永远为 false，那样只会让玩家干瞪着黑幕直到超时。 |
| `void FadeInScreenMask(Action onComplete = null)` | 低层原语：黑幕淡入（淡到全黑）。 |
| `void FadeOutScreenMask(Action onComplete = null)` | 低层原语：黑幕淡出（淡回全透明）。 |
| `bool HasScreenMask { get; }` | 是否配了黑幕。为 `false` 时上面两个转场退化成直通。 |
| `bool IsScreenMaskOpaque { get; }` | 黑幕当前是否全黑。 |
| `bool IsPlayingScreenMaskTransition { get; }` | 当前是否正在跑一次转场。 |
| `CanvasGroup screenMaskCanvasGroup` | Inspector 设置项。留空则黑幕整体关闭。 |
| `float screenMaskFadeDuration` | Inspector 设置项，淡入 / 淡出时长（秒），默认 `0.3`。 |
| `float screenMaskTimeout` | Inspector 设置项，两处等待的超时（秒），默认 `8`。 |

- **同一时刻只有一个转场在跑**：再起一个会先把在跑的那个停掉。
- **回调里的异常会被就地吞掉并记录**：`onBlackout` / `onStopped` 是宿主代码（开播、关界面），
  让异常穿出去会打断转场协程，黑幕便永远停在全黑上——那是最糟的失败方式。
- **两处等待都有超时**（`screenMaskTimeout`）：超时只告警不抛错，宁可让玩家看到一个还没铺好的
  画面，也绝不把他永久留在全黑里。
- 黑幕的补间直接走 `ToolkitTween` 而非 `VnTween`：它是转场 chrome，不跟演出倍速走；
  且用 unscaled 时间，暂停菜单把 `timeScale` 压到 0 时转场仍然走得完。
- 幕布一动就 `blocksRaycasts = true`，淡回全透明时才还回去——遮幕期间的点击没有任何合理去处，
  漏下去只会点在对话框的继续按钮上、白白跳掉一行。

### `VnStoryPlayer`

```csharp
public class VnStoryPlayer : MonoBehaviour
public enum VnStoryPlayer.AutoPlayTiming { Manual, OnStart, OnEnable }
```

| 成员 | 说明 |
|---|---|
| `void Play()` | 用当前 `ConversationName` 播放。可直接绑到 Button 的 OnClick。名称为空时告警并返回。 |
| `void Play(string conversationNamePlay)` | 播放指定对话，并把名称记进 `ConversationName`。 |
| `void Play(string conversationNamePlay, Action onFinished, bool autoStopOnFinished = true, bool skipStartEntryConditions = false)` | 播放并登记完成回调与收场方式，四个参数直通 `VnStoryManager.StartVnStory`。<br>⚠️ `onFinished` **自然播完与中途停止都会触发**，框架不区分这两者。<br>1.6.4 起：**会先中止在排的连播队列**——单段播放是一次全新的播放请求，不清队列的话，排在后面的那一段会在一帧后把这次请求顶掉。 |
| `void PlaySequence(IList<string> conversationNamePlays, Action onFinished = null, bool autoStopOnFinished = true, bool skipStartEntryConditions = false, bool stopAtConversationBoundary = false)` | **按顺序连播多段**（1.6.4+）。空项跳过；只有一段时等价于 `Play`，不留下队列状态。<br>**前 N-1 段不收场**，UI 与背景留在场上等下一段接手；**最后一段透传 `autoStopOnFinished`**，于是连播与单播的收尾表现一致。<br>**段间跨一帧再播下一段**：完成回调跑在 DS 的 `ConversationController.Close()` 广播里，此刻 `IsPlaying` 尚未被 `conversationEnded` 复位，直接接着播会被 `Play` 开头的「已在播放中」守卫**静默吞掉**；在 DS 的收尾调用栈里重入开新对话本身也不稳。<br>`onFinished` 是**整队**的回调，只兑现一次（不是每段一次），同样「播完与被打断都会触发」。 |
| `stopAtConversationBoundary`（上一行的第五个参数，1.6.5+） | 每一段是否**播到会话边界即止**。默认 `false`，跟着跨会话链接一路播下去。传 `true` 则每段开播前给管理器架一道[会话围栏](#会话围栏)，流出这一段就停。围栏一次只圈当前这一段——圈住整个列表反而会让段间的链接无缝流过去、再被队列重播一次。 |
| `void Stop()` | 停止。**只停由本组件播放的那段**（当前对话名需与 `ConversationName` 一致）。<br>1.6.4 起：**无条件中止连播队列**，且排在其余守卫之前——段间那一帧里 `IsPlaying` 已复位、对话也已结束，守卫会全部早退，不先清队列的话下一段会在一帧后凭空开播。 |
| `string ConversationName { get; set; }` | 当前要播放的对话名。连播时它是**正在播的那一段**。 |
| `bool IsPlaying { get; private set; }` | 是否正在播放。由 Dialogue System 的 `conversationEnded` 置回 false。 |
| `bool IsPlayingSequence { get; }` | 是否正在连播（1.6.4+）。整队走完、或被 `Stop()` / `OnDisable` 中止后变 false。 |
| `UnityEvent OnPlayStarted { get; }` | 只读属性，返回可 `AddListener` 的实例（非泛型 `UnityEvent`）。 |
| `UnityEvent OnPlayEnded { get; }` | 同上。⚠️ 对话因**任何**原因结束都会触发，不限于本组件调用 `Stop()`。 |

### `VnActorAnimator`

```csharp
public class VnActorAnimator : MonoBehaviour
```

演出层与动画后端之间的唯一接缝。所有播放 / 状态 / 皮肤接口都经**就绪门控**，
在动画器初始化完成前调用会自动挂起，**不依赖帧序**。

**就绪与生命周期**

| 成员 | 说明 |
|---|---|
| `bool IsActorReady { get; }` | 是否已完成过一次 `ExecuteInit`。没有动画器的普通预制体也会正常置位。 |
| `void RunWhenReady(Action action)` | 就绪则**同步立即**执行；否则挂起，按登记顺序执行一次。从未 `ExecuteInit` 过则永不执行。 |
| `void ExecuteInit(Vector3 toPos, Vector3 toRot, Vector3 toScale, string[] toStateArray)` | 落位 → 激活 → 就绪后淡入并切到目标状态。`toStateArray` 传 `null` = 沿用预制体自身初始状态；传**空数组** = 明确不进入任何状态。会先 `StopAllAnims()` 并掐掉在途补间。 |
| `void ExecuteDestroy(Action onComplete = null)` | 销毁。按「动画淡出」与「粒子生命期」取较大者延迟；`onComplete` 在对象已销毁后仍会触发。 |
| `bool FadeOut()` / `bool FadeIn()` | 临时隐藏 / 恢复，不销毁。返回值 = **是否由本方法处理了**（无动画器也无粒子的普通预制体返回 false，需外部自行 SetActive）。 |

**位移 / 旋转 / 缩放**（速度制，不是时长制）

| 成员 | 说明 |
|---|---|
| `void SetToPosition(Vector3 targetPos, float speedRate = 1f)` | 时长由「距离 ÷ (配置速度 × speedRate)」推出。⚠️ 对象未激活时直接**瞬置**而非补间。 |
| `void SetToRotation(Vector3 targetRot, float speedRate = 1f)` | 取最短弧。 |
| `void SetToScale(Vector3 targetScale, float speedRate = 1f)` | |
| `void CompleteTransformTween()` | 立刻完成在途的移动 / 旋转 / 缩放，并触发其完成回调。 |

**状态列表**

| 成员 | 说明 |
|---|---|
| `void SwitchStateArray(string[] actorAnims)` | 差集移除旧状态、差集添加新状态。⚠️ **传空数组会把角色淡出隐藏**——渲染器引用计数归零所致。只想换动画时别传空数组。 |
| `void AddState(string state)` / `void RemoveState(string state)` | 增 / 删单个状态。已存在 / 不存在时不处理。 |

**单条动画**（叠加在状态动画之上，不改变状态列表）

```csharp
bool PlayAnim(
    string animName,
    EAnimTrack animTrack = EAnimTrack.Action,
    int animTrackSub = 0,
    bool isLoop = false,
    bool isReverse = false,
    float speed = 1f,
    float startDelayTime = 0f,
    Action onComplete = null)
```

返回值 = **是否受理了这次请求**；`false` 表示明确不会播、也不会有回调（动画器缺失 / 名称为空 / `speed≈0`）。

⚠️ **`onComplete` 在以下五种情形一律不触发**：① `isLoop` 为 true（调用时会告警）；② `speed` 为 0（返回 false）；
③ 动画名查不到（返回 **true**，但 `IsAnimPlaying` 随即为 false）；④ 动画时长为 0；
⑤ 被 `StopAnim` / `StopAllAnims` / 新一轮 `ExecuteInit` 打断。

⚠️ **完成时刻是「时长 ÷ 速度」的定时器估算**，不是后端的结束事件——需要严格同步的场合不要依赖它。

| 成员 | 说明 |
|---|---|
| `bool StopAnim(string animName)` | 停止指定单条动画，该轨道上被它压住的上一条自动恢复。返回是否确实停掉了一条。 |
| `void StopAllAnims()` | 停止全部单条动画，不影响状态动画。 |
| `bool IsAnimPlaying(string animName)` | 是否在播（含仍在等待起播延时的）。读的是本组件的记账，不是后端状态。 |

**换装 / 皮肤**

| 成员 | 说明 |
|---|---|
| `void SetBaseSkin(string[] baseSkinNames, bool isRefresh = true)` | 设置基础皮肤组，覆盖预制体上配置的那一组。 |
| `void AddSkin(string skinName, bool isRefresh = true)` | 添加皮肤，可叠加多件。**没有**「同部位只能穿一件」的互斥语义。 |
| `void RemoveSkin(string skinName, bool isRefresh = true)` | 移除皮肤。 |
| `void RefreshSkin()` | 把基础皮肤与应用中皮肤的并集重新应用到渲染器。 |

> 批量增删时前几次传 `isRefresh: false`、最后一次传 `true`，避免重复刷新。

### `VnResponseButton`

```csharp
public class VnResponseButton : StandardUIResponseButton
public override Response response { get; set; }
```

派生自 Dialogue System 的 `StandardUIResponseButton`。`response` 是覆写基类的属性（故为小写命名）；
被赋值时按「是否已读」刷新文本颜色、按钮颜色、提示对象与图片颜色。

### 音频后端

```csharp
public enum EVnAudioCategory { Bgm, Ambient, Sfx, Voice }

public interface IVnAudioBackend
{
    void PlayWithChannel(EVnAudioCategory category, string channelName, string audioKey, float volume, float pitch);
    void StopWithChannel(EVnAudioCategory category, string channelName);
    void Play(EVnAudioCategory category, string audioKey, float volume, float pitch);
    void Stop(EVnAudioCategory category, string audioKey);
}

public static class VnStoryAudio
{
    public static IVnAudioBackend Backend { get; set; }   // 永不为 null；未赋值时返回空实现
    public static bool IsAvailable { get; }               // 是否已接入真实后端
    // 四个与接口同签名的静态转发方法
}

public sealed class NullVnAudioBackend : IVnAudioBackend  // 单例 NullVnAudioBackend.Instance
```

接入方式与注意事项见上文 [音频接缝](#音频接缝)。

```csharp
// 可选能力。后端在 IVnAudioBackend 之外**再**实现它，就能让自动播放精确等到语音播完、
// 让快进把语音一起倍速。不实现不报错，只是这两处退化。
public interface IVnAudioPlaybackInfo
{
    bool IsPlaying(EVnAudioCategory category, string audioKey);
    void SetPlaybackRate(EVnAudioCategory category, string audioKey, float rate);
}

public static class VnStoryAudio
{
    public static bool SupportsPlaybackInfo { get; }   // 当前后端是否实现了上面这个接口
    public static bool IsPlaying(EVnAudioCategory category, string audioKey);
    public static void SetPlaybackRate(EVnAudioCategory category, string audioKey, float rate);
}
```

### `VnPlaybackController`

播放控制的状态持有者。与 `VnStoryManager` **挂在同一个物体上**（选项菜单的通知是
`BroadcastMessage` 发到那里的，挂别处收不到）。取用：`VnStoryManager.Instance.Playback`。

```csharp
public enum EVnPlaybackSpeedTier { X1 = 1, X2 = 2, X3 = 3 }
public enum EVnFastForwardState { Off, Active, Suppressed }   // Suppressed = 按着但已被未读行打断

public class VnPlaybackController : MonoBehaviour
{
    public bool AutoPlay { get; set; }
    public EVnPlaybackSpeedTier SpeedTier { get; set; }
    public EVnFastForwardState FastForwardState { get; }
    public bool IsFastForwarding { get; }
    public bool StopOnUnread { get; set; }
    public float FastForwardRate { get; set; }           // 快进倍率：补间/延时/动画/粒子/语音（默认 5）
    public float FastForwardTypewriterRate { get; set; } // 快进时的打字机倍率（默认 30）
    public float AutoPlayDelay { get; set; }

    public event Action StateChanged;          // 任意状态变化，供按钮刷新图标
    public event Action FastForwardBlocked;    // 被「新对话停止」打断时

    public void ToggleAutoPlay();
    public void CycleSpeedTier();              // 1x → 2x → 3x → 1x
    public void ToggleStopOnUnread();
    public void BeginFastForward();            // 按下
    public void EndFastForward();              // 松开。务必与 Begin 成对
    public void ForceStopFastForward();

    public VnPlaybackSettingsData GetSettings();
    public void ApplySettings(VnPlaybackSettingsData data);   // 不触发 StateChanged
    public void ResetToDefaults();                            // 同上
    public void NotifyStateChanged();                         // 主动刷一次界面（读档由管理器自动调用）
    public void SetUiHidden(bool hidden);                     // 由 VnUiHider 调用
}
```

### 播放倍率与补间

```csharp
public static class VnPlaybackRate
{
    public static float Playback { get; }      // 补间 / 延时 / 角色动画 / 粒子 / 语音
    public static float Typewriter { get; }    // 打字机字速与标点停顿
    public static event Action<float> PlaybackChanged;
}

// ToolkitTween 的倍率感知包装。演出层一律走它；签名与 ToolkitTween 逐字一致。
public static class VnTween { /* FadeCanvasGroup / FadeSpriteRenderer / MoveTransform / … / DelayedCall / Kill */ }

// 可选契约：角色 / 特效预制体上的组件实现它就能跟随倍率。
public interface IVnPlaybackRateReceiver { void OnVnPlaybackRateChanged(float rate); }
```

`VnStoryManager` 侧：

```csharp
public void SetPlaybackRate(float playbackRate, float typewriterRate);   // 唯一写入者
public void ApplyPlaybackRateToActors(float rate);
public void RefreshTypewriterSpeed();
public void StopAllTypewriters();
public bool IsAnyTypewriterPlaying();
public bool IsLineVoicePlaying();
public bool IsLoadingAssets { get; }   // 资源加载中，此时不应推进对话
public Canvas UiCanvas { get; }
```

### 已读记录与存档

```csharp
public sealed class VnReadHistory
{
    public static void EnsureSimStatusEnabled();    // 检测并补开，顺带关掉 DS 自己的 SimStatus 存档
    public static string BuildStamp();              // 剧本结构指纹
    public bool IsUnread(DialogueEntry entry);      // id == 0 的 START 结构节点恒为 false
    public void HydrateConversation(int conversationId);
    public void HydrateAll();
    public string Encode();
    public bool Load(string encoded, out string error);
    public void ClearAll();
}

// 纯函数、无 Unity 依赖，可直接单测
public static class VnReadHistoryCodec
{
    public const ushort Version = 1;
    public const int MaxCapacity = 100000;
    public static int ByteLengthFor(int capacity);
    public static byte GetStatus(byte[] bits, int index);
    public static void SetStatus(byte[] bits, int index, byte status);
    public static string Encode(IList<FVnReadRecord> records);
    public static bool TryDecode(string base64, out List<FVnReadRecord> records, out string error);
}

// VnStoryManager 实现 Ale.Toolkit.Runtime.ISaveable；1.6.0 起可覆写，宿主子类在 base 上扩展
public virtual VnStorySaveData GetSaveData();           // 深拷贝。覆写点：补充 choices 等宿主数据
public virtual void LoadSaveData(VnStorySaveData data); // 覆盖语义，容忍 null 与脏数据，不触发事件
public virtual void ResetAll();                          // 覆写里不要反调 LoadSaveData（成环）
protected virtual void OnConversationChoiceSelected(Subtitle subtitle); // 分支选项落地（其 Script 已执行完毕）；基类空实现
```

### `VnPlaybackButton` / `VnUiHider`

```csharp
public enum EVnPlaybackButtonKind { AutoPlay, SpeedTier, FastForward, StopOnUnread, HideUi }

// 挂在按钮物体上，按 kind 自动接控制器并切换开 / 关两张图。
// 走 EventSystem 的指针事件，与输入后端无关；快进是长按，其余是点击。
public class VnPlaybackButton : MonoBehaviour { public void Refresh(); }

public class VnUiHider : MonoBehaviour
{
    public bool IsHidden { get; }
    public void Hide();      // 没有 EventSystem 时拒绝执行并报错（避免软锁）
    public void Show();
    public void Toggle();
    public void RegisterCanvas(Canvas canvas);     // 宿主自己的 HUD 也一起藏
    public bool UnregisterCanvas(Canvas canvas);
}
```

### `VnConditionSources`

```csharp
namespace Ale.VnFramework.Conditions   // 程序集 Ale.VnFramework.Condition

public static class VnConditionSources
{
    public static object Subject { get; set; }                       // 透传给 IConditionContext.Subject

    public static void RegisterNumber(string id, Func<double> getter);
    public static bool UnregisterNumber(string id);                  // 返回是否确有其项
    public static bool HasNumber(string id);

    public static void RegisterFlag(string id, Func<bool> getter);
    public static bool UnregisterFlag(string id);
    public static bool HasFlag(string id);

    public static void RegisterService<T>(T service) where T : class; // 供第三方判定器 ctx.GetService<T>()
    public static bool UnregisterService<T>() where T : class;

    public static void Clear();                                      // 清空全部注册与 Subject
}
```

宿主向条件系统提供实时数据的唯一入口。`getter` 在**每次条件求值时**被调用，因此取到的永远是当前值。

- 重复注册同一 id 会**覆盖并告警**（与 `RegisterGameplaySystem` 同风格）。
- **未注册的 id 判「不成立」并告警一次**，不会静默放行；详见上文 [条件系统](#条件系统)。
- 注册的服务**优先于**内置的数值 / 标记源，故 `RegisterService<IConditionNumberSource>(...)`
  可以整套换掉内置实现。
- 每次进入播放模式时自动 `Clear()`。关掉「重新加载域」后静态表会跨播放存活，
  里头的 getter 闭包却捕获着上一轮已销毁的对象——不清就会在下一轮求值时抛异常。
  **请在 `OnEnable` / `OnDisable` 里成对注册与注销。**

### `VnConditionBridge`

```csharp
namespace Ale.VnFramework.Conditions

public static class VnConditionBridge
{
    public static IReadOnlyList<string> RegisteredFunctionNames { get; }  // 已注册的 Lua 函数名
    public static void RegisterAll();                                     // 重复调用安全
    public static void UnregisterAll();
}
```

通常**不需要手动调用**：`[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` 已在每次进入播放时自动注册。
`RegisteredFunctionNames` 便于自检「某判定器到底接上了没有」。

### `VnConditionNaming`

```csharp
namespace Ale.VnFramework.Conditions

public static class VnConditionNaming
{
    public const string LuaFunctionPrefix = "AleCond_";
    public const string MenuRoot          = "Ale 条件";
    public const int    MaxParameterCount = 8;

    public static string ToLuaFunctionName(string evaluatorKey);
    public static string ToMenuPath(string category, string luaFunctionName);
    public static bool   TryPlan(IConditionEvaluator evaluator,
                                 out string luaFunctionName, out int parameterCount, out string skipReason);
}
```

命名规则的唯一出处，运行时桥与编辑器生成器共用。一般只在写工具、需要由判定器 Key 反推
Lua 函数名时才用得到。
