# Ale VN Framework（剧情演出框架）

基于 [Pixel Crushers Dialogue System](https://assetstore.unity.com/packages/tools/behavior-ai/dialogue-system-for-unity-11672)
的视觉小说（Visual Novel / Galgame）剧情演出框架。Dialogue System 负责对话数据、分支与 Lua 环境，
本包在其之上补齐**演出层**：背景 / 角色 / 特效 / 头像的切换与补间、消息提示、富文本符号、多语言条目、
自动播放与跳过。

角色动画经 `VnActorAnimator` 对接 [`com.ale.animsimulatorsystem`](https://github.com/AleFeng/unity-ale-anim-simulator)，
**后端无关**——Spine / Live2D / Unity 动画都走同一套接口；没有 `VnActorAnimator` 组件的纯图片、
纯粒子预制体也能正常实例化与销毁。

---

## 依赖与安装顺序

| 依赖 | 版本 | 来源 | 必需 |
|---|---|---|---|
| Dialogue System for Unity | — | Asset Store（付费） | ✅ **且需补 asmdef，见下节** |
| `com.ale.toolkit` | ≥ 1.7.9 | git URL | ✅ |
| `com.ale.animsimulatorsystem` | ≥ 2.6.0 | git URL | ✅ |
| `com.unity.textmeshpro` / `com.unity.localization` | — | Package Manager | 可选，见「可选宏开关」 |
| `com.fs.gameframework` | — | 私有 | 可选，仅音频后端需要 |

⚠️ **`package.json` 的 `dependencies` 是空的**，装本包**不会**自动拉取上述依赖。
原因是 `com.ale.toolkit` 与 `com.ale.animsimulatorsystem` 经 git URL 分发，而 UPM 不支持在
`dependencies` 里写 git URL。**请按 toolkit → animsimulatorsystem → 本包的顺序安装**，
颠倒会报「找不到 `Ale.Toolkit.*` / `Ale.AnimSimulatorSystem`」。

## ⚠ 前置条件：为 Dialogue System 补 Assembly Definitions

**这一步不做，本包编译不过。**

本包是 UPM 包，代码位于独立程序集 `Ale.VnFramework`。而 asmdef 程序集**无法引用预定义程序集
`Assembly-CSharp`**——Dialogue System 默认没有 asmdef，它的代码正落在那里，于是从包里看不见
`DialogueManager`、`StandardUIResponseButton` 这些类型。

Pixel Crushers 官方提供了 asmdef 方案（见 `Pixel Crushers/Dialogue System/Scripts/_README.txt`）。
本包在 **[`Docs~/Setup/PixelCrushers/`](Docs~/Setup/PixelCrushers/README.md)** 附了一份整理好的副本：
6 个 `.asmdef` 已按目标相对路径摆放，整体复制到你的 `Pixel Crushers/` 目录即可。

其中第 6 个是**补官方遗漏的补丁**：官方布局会把 `Templates/Scripts/Editor/` 下 3 个纯编辑器脚本
卷进运行时程序集，而它们用了 `UnityEditor` 却没有 `#if UNITY_EDITOR` 保护——
**编辑器里编译不报错，只在出包时失败**。详情与逐条说明见
[`Docs~/Setup/PixelCrushers/README.md`](Docs~/Setup/PixelCrushers/README.md)。

## 基础设置

![Dialogue System 欢迎窗口](Docs~/image.png)

在 `Tools → Pixel Crushers → Dialogue System → Welcome Window` 打开欢迎界面，
在 **Enable support for** 栏勾选实际用到的插件：

| 选项 | 用途 |
|---|---|
| TextMesh Pro | 文本显示组件，支持更丰富的文本样式 |
| 2D Physics | 2D 物理系统，支持角色的碰撞检测 |
| Addressables | 资源管理系统，支持更高效的资源加载 |
| New Input System | 输入系统，支持更灵活的输入配置 |
| Timeline | 时间轴系统，支持更复杂的剧情演出 |

## 快速开始

1. 按上面两节装好依赖、补好 Dialogue System 的 asmdef。
2. Package Manager → **Ale VN Framework** → Samples → 导入 **VN Framework Demo**。
3. 打开样例场景 `VnStorySamples.unity` 直接运行，即可看到完整的剧情演出流程。
4. 参照 [使用文档](Docs~/VnStoryManager/VnStoryManager.md) 配置自己的剧情库、角色与资源。

代码侧启动一段剧情：

```csharp
using Ale.VnFramework;

// 方式一：挂 VnStoryPlayer 组件，在 Inspector 配置对话名与自动播放时机，
//         也可由 Button.OnClick 触发。
// 方式二：直接调管理器。
VnStoryManager.Instance.StartVnStory("Chapter01/Prologue");
VnStoryManager.Instance.StopVnStory();
```

> ⚠️ 样例导入后会落到 `Assets/Samples/Ale VN Framework/1.0.0/Demo/`，
> 而 `VnStoryManager` 上的四个 Addressables 地址前缀默认写死指向 `Assets/Demo/…`，
> 需要手工订正为样例的实际路径。

## 可选宏开关

| 宏 | 作用 | 由谁定义 |
|---|---|---|
| `HAS_TMPRO` | 启用 TextMeshPro 文本路径（`VnResponseButton` / `VnStoryManager`） | **Fs GameFramework** 的 DefineChecker |
| `HAS_LOCALIZATION` | 启用 Unity Localization 的多语言条目 | **Fs GameFramework** 的 DefineChecker |
| `ATK_ADDRESSABLE` | 资源经 `ToolkitAssets` 走 Addressables，否则回落 `Resources` | Ale Toolkit 的 DefineChecker |
| `VNS_FS_GAMEFRAMEWORK` | 把 `VnStoryAudio` 接到 Fs 的 `AudioManager` | 手工添加 |

⚠️ **`HAS_TMPRO` 与 `HAS_LOCALIZATION` 目前由 `com.fs.gameframework` 维护，不是本包也不是
Ale Toolkit 维护的。** 没装 Fs 的工程里这两个宏不会被自动定义，对应功能会静默关闭——
即使 `Ale.VnFramework.asmdef` 已经引用了 `Unity.TextMeshPro` / `Unity.Localization`。
需要时可在 Project Settings 手工添加这两个宏。（后续版本考虑改用 Ale Toolkit 的 `ATK_TMP` / `ATK_LOCALIZATION`。）

⚠️ **`VNS_FS_GAMEFRAMEWORK` 默认关闭，此时剧情全程无声。** `VnStoryAudio` 整个文件包在该宏内，
关闭时四个播放 / 停止接口是空操作。开启它需要 `com.fs.gameframework`，
并自行在 `Runtime/Ale.VnFramework.asmdef` 的 `references` 补上 `Fs.GameFramework.Common.AudioSystem`
（本包默认不引用它，以免没装 Fs 的使用者每次域重载都吃一条未解析引用警告）。

## 目录结构

```
com.ale.vnframework/
├── package.json
├── README.md                     本文件
├── CHANGELOG.md
├── LICENSE.md                    MIT
├── Runtime/
│   ├── Ale.VnFramework.asmdef    唯一的程序集（无编辑器代码）
│   ├── VnStoryManager.cs         演出核心：背景/角色/特效/头像/消息/变量/扩展点
│   ├── VnStoryPlayer.cs          播放控制：按对话名启停，自动播放时机
│   ├── VnActorAnimator.cs        角色动画对接层（→ AnimatorBase，后端无关）
│   ├── VnResponseButton.cs       分支选项按钮（已读/未读态）
│   └── VnStoryAudio.cs           音频接缝（默认空实现）
├── Docs~/                        Unity 不导入（~ 后缀）
│   ├── Setup/PixelCrushers/      ⚠ Dialogue System 的 asmdef 副本 + 说明
│   └── VnStoryManager/           使用文档与截图
└── Samples~/
    └── Demo/                     样例：剧情库、预制体、Spine 角色、特效、UI、场景
```

## 文档

- **[VnStoryManager 使用文档](Docs~/VnStoryManager/VnStoryManager.md)** —— 资源导入、剧情演出配置、
  UI 样式与功能示例的完整说明。
- [为 Dialogue System 补 Assembly Definitions](Docs~/Setup/PixelCrushers/README.md) —— 前置条件的操作步骤。
- [更新日志](CHANGELOG.md)

## 许可

[MIT](LICENSE.md)。注意 Dialogue System 与样例中的第三方美术资源（Cartoon FX Remaster 等）
各自遵循其原有许可，不在本许可范围内。
