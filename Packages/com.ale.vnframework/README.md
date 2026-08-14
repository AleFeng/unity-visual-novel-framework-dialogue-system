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
| **头像** | `DialogueHead` | 图片文件名 | 对话框头像 |
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

- **资源目录约定**：背景 / 角色 / 头像 / 特效各配一组「文件夹路径 + 扩展名」
  （`backgroundAddressableFolder` / `…Extension`、`actorAddressableFolder` / `…Extension`、
  `dialogueHeadAddressableFolder` / `dialogueHeadExtension`、`effectAddressableFolder` / `…Extension`），
  配置里只写资源名。路径以 `Assets/` 开头、以 `/` 结尾。
  加载经 `ToolkitAssets` 统一入口——开启 `ATK_ADDRESSABLE` 时走 Addressables 异步加载并回收句柄，
  否则回落 `Resources`。
  ⚠️ **这八个设置项本身包在 `#if ATK_ADDRESSABLE` 内，未开启该宏时不会出现在 Inspector 上。**
- **预制体生命周期**：角色与特效走**同一套**加载 / 定位 / 卸载流程。预制体设置一次持续存在，
  把 `Value` 置空才清除。
- **补间**：位置 / 旋转 / 缩放的第 4 个参数是速度倍率；背景切换可经 Dialogue System 的
  `StandardSceneTransitionManager` 做淡入淡出。
- **富文本与打字机**：支持富文本图标符号与打字机符号，可在逐字显示中插入停顿与表情图标。
- **多语言**：开启 `HAS_LOCALIZATION` 后，`[字段标题]+[语言代码]` 形式的条目接 Unity Localization。

### 播放控制（`VnStoryPlayer`）

挂在任意 GameObject 上，按对话名启停某段剧情。可在 Inspector 配置对话名与自动播放时机
（嵌套枚举 `AutoPlayTiming`），也可由 `Button.OnClick` 或脚本触发。

```csharp
using Ale.VnFramework;

VnStoryManager.Instance.StartVnStory("Chapter01/Prologue");
VnStoryManager.Instance.StopVnStory();                 // 默认清空演出数据
VnStoryManager.Instance.StopVnStory(clearAllData: false);

string[] names = VnStoryManager.Instance.GetAllConversationName();
```

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

## UI 样式

样例中的 `DialogueUI_Main` 预制体给出了一整套可直接改的对话 UI：玩家对话面板与配角对话面板、
分支选项面板、消息提示面板，以及继续按钮的 UI 动画。逐项说明见
[使用文档 - UI样式](Docs~/VnStoryManager/VnStoryManager.md#ui样式)。

---

## 配置流程

完整图文步骤见 [VnStoryManager 使用文档](Docs~/VnStoryManager/VnStoryManager.md)，此处为脉络：

1. **剧情演出管理器预制体** —— 在 Project 面板的预制体源文件上改配置（不是场景实例），
   配好四组资源「文件路径与类型」和各类「字段条目的标题」。二者都有可用默认值，
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
│   ├── Ale.VnFramework.asmdef   唯一程序集（无编辑器代码）
│   ├── VnStoryManager.cs        演出核心：字段条目解析、背景/角色/特效/头像/消息、
│   │                            预制体生命周期、全局变量、玩法扩展点
│   ├── VnStoryPlayer.cs         播放控制：按对话名启停、自动播放时机
│   ├── VnActorAnimator.cs       角色动画对接层（→ AnimatorBase，就绪门控）
│   ├── VnResponseButton.cs      分支选项按钮（已读 / 未读态）
│   └── Audio/                   可替换的音频后端
│       ├── IVnAudioBackend.cs   ← 接入自己的音频系统实现这个
│       ├── EVnAudioCategory     （同文件）Bgm / Ambient / Sfx / Voice
│       ├── VnStoryAudio.cs      静态门面，持有当前后端
│       ├── NullVnAudioBackend.cs 默认空实现
│       └── FsVnAudioBackend.cs  Fs 后端（VNS_FS_GAMEFRAMEWORK 门控，兼作范例）
├── Editor/                      欢迎窗口与编译宏开关
│   ├── Ale.VnFramework.Editor.asmdef
│   ├── VnFrameworkWelcomeWindow.cs   前置条件自检 + 插件支持（编译宏）+ 文档入口
│   ├── VnFrameworkDefines.cs         VNS_FS_GAMEFRAMEWORK 宏名与依赖探测
│   ├── VnFrameworkDefineChecker.cs   启动时按需弹窗（只提示，绝不自动改写宏）
│   ├── VnFrameworkEditorPrefs.cs     EditorPrefs / SessionState 键
│   └── L10n/                         编辑器界面的英 / 日译表
├── Docs~/                       Unity 不导入（~ 后缀）
│   ├── Setup/PixelCrushers/     ⚠ Dialogue System 的 asmdef 副本与说明
│   └── VnStoryManager/          使用文档、截图与演示视频
└── Samples~/VN Framework Demo/  演示 Sample
    ├── VnStorySamples.unity     示例场景
    ├── VnStoryManagerBase.prefab
    ├── Data/StoryDatabase.asset 剧情库
    └── Assets/                  Actors(Spine) / ActorsHead / Backgrounds /
                                 Effects / Emoji / UI / TextTable
```

---

## 详细文档

- **[VnStoryManager 使用文档](Docs~/VnStoryManager/VnStoryManager.md)** —— 资源配置、资源导入、
  剧情演出配置、UI 样式、功能示例的逐项图文说明（含演示视频）。
- [为 Dialogue System 补 Assembly Definitions](Docs~/Setup/PixelCrushers/README.md) —— 安装前置条件的
  操作步骤与原理（**不做则本包编译不过**）。
- [更新日志](CHANGELOG.md) ｜ [许可](LICENSE.md)
