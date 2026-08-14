# 更新日志（Changelog）

本文件记录 VN Framework（`com.ale.vnframework`）的所有重要变更。

格式参考 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.0.0/)，版本号遵循 [语义化版本](https://semver.org/lang/zh-CN/)。

> 迁移说明（2026-08-13）：插件位置由 `Assets/VnStoryManager` 迁移至内嵌 UPM 包 `Packages/com.ale.vnframework`；
> 运行时代码由 `Assembly-CSharp` 独立为程序集 `Ale.VnFramework`；命名空间
> `PixelCrushers.DialogueSystem.VnStoryFramework` → `Ale.VnFramework`。脚本 `.meta` 的 GUID 全部保留，
> 既有场景与预制体的组件引用不受影响。**升级前请先读下方「⚠ 前置条件」。**

## [1.1.0] - 2026-08-14

把两个宏归一到 Ale Toolkit，并修掉一个会让资源地址「永久失效」的缓存缺陷。
**功能与 API 无破坏性变更**——除非你的工程正靠 `com.fs.gameframework` 定义的
`HAS_TMPRO` / `HAS_LOCALIZATION` 来开启本包的对应路径（见「升级指引」）。

### 变更

- **不再使用 `HAS_TMPRO`，该宏被整个删除**（不是改名）。它此前只门控 `VnResponseButton.stateTxtArray`
  一个字段，而该字段唯一的用途是 `ToolkitTween.TintGraphic` 染色——包内**从未读写过 `.text`**。
  字段类型放宽为 `Graphic[]`（`TextMeshProUGUI` 与 `UnityEngine.UI.Text` 都派生自它），
  TMP 装没装都能用，无需编译宏。**已实测既有预制体的序列化引用不丢失**（「收窄 → 放宽」）。
- **`HAS_LOCALIZATION` → `ATK_LOCALIZATION`**（4 处，仅 `VnStoryManager`）。纯改名，
  包内只用到 `Locale` 与 `LocalizationSettings` 两个类型，无 API 变化。
- **打字机组件改用基类 `AbstractTypewriterEffect`**（原为 `TextMeshProTypewriterEffect`）。
  该处本就裸露在宏外：未装 TextMeshPro 时面板挂的是 `UnityUITypewriterEffect`，
  取具体子类会漏掉它、**打字机调速静默失效**。改用基类后两种实现都能收集与调速。
- 至此包内**再无 TextMeshPro 专属类型**；`ATK_ADDRESSABLE`（8 处）本就正确，未改动；
  `ATK_INPUT_SYSTEM` 无事可做——全包对输入系统零引用。
- **样例资源改用固定短地址 `VNFrameworkDemo/…`，不再用完整资产路径。**
  完整路径里含包版本号（样例落地于 `Assets/Samples/{包名}/{版本}/…`），本次升到 1.1.0 就会让
  1.0.0 时写死的四个前缀全部失效、使用者导入样例后资源全部加载不到。
  现在 Addressables 文件夹条目挂在 `VN Framework Demo` 文件夹本身、地址取固定名 `VNFrameworkDemo`，
  四个前缀相应变为 `VNFrameworkDemo/Assets/{ActorsHead,Backgrounds,Actors,Effects}/`，
  **与版本号和样例落地路径彻底解耦，此后发版不必再改**。
  ⚠️ 使用者登记 Addressables 条目时，需把该条目的 Address 改成 `VNFrameworkDemo`。

### 修复

- **`LoadAsset` 会把加载失败的 `null` 写进缓存**，导致该地址被永久毒化：
  `Dictionary` 存了 `null` 值后 `TryGetValue` 仍返回 `true`，于是此后同一地址的每次请求都命中缓存、
  直接回传 `null` 并 `return`，**再也不会真正发起加载**；每次命中还会累加已加载计数，
  进而在卸载时对一个从未持有过句柄的地址调用 `ReleaseAddress`。
  现改为：失败不入缓存、不计数、不释放，但仍回调 `null` 让调用方走自己的失败分支。
- **缓存命中处增加 Unity 语义判活**：已销毁的对象在托管层仍是有效引用、但 `==` 判定为 null
  （所谓「假 null」），此前同样会造成永久毒化。现在死条目视为未命中，清理后重新加载。
- **待卸载路径仅在确实加载成功时才 `ReleaseAddress`**，避免对未持有的句柄空放。
- `OnSelectedLocaleChanged` 增加 `SelectedLocale` 空值守卫。可用语言表为空或 Localization
  尚未初始化完成时它会是 `null`（编辑模式下实测如此），原代码取 `.Identifier.Code` 会抛 NRE。
  优先使用事件传入的 `locale`，取不到语言代码则维持 Dialogue System 现状。签名保持不变。
- 修正错位的打字机告警：真正「没找到打字机组件」的分支原本不打日志，而该文案却挂在
  「字幕文本组件为空」的分支上。两条均已归位并带上面板名。
- 修正使用文档中失效的资源导入链接（原指向 Fs 的私有仓库）与 `#字段条目名称` 的锚点错配。

### 文档

- 使用文档新增 **「多语言的导出与导入」** 小节：说明 Dialogue System 自带的
  `Database → Localization Export/Import` 流程——语言列表与 `Find Languages`、额外字段、四个开关、
  按语言生成 `Actors_` / `Dialogue_` / `Quests_` / `Items_` 四组 CSV、以及编码是**带 BOM 的 UTF-8**
  （Excel 双击直接打开不乱码）。**逐个节点手工填多语言文本效率很低，这才是推荐做法。**
- 订正包内 README 的多语言表述：Unity Localization 只提供**语言代码**，取值由 Dialogue System 自己完成，
  且只作用于它自己的字段——对白按**裸语言代码**、其余按「标题 + 空格 + 语言代码」；
  本框架的演出字段（`Background`、`Actor1Prefab` 等）不参与多语言。

### 升级指引

- 若你的工程此前**依赖 `com.fs.gameframework` 定义的 `HAS_LOCALIZATION`** 来开启多语言路径，
  升级后请改在 **Ale Toolkit 欢迎窗口**（`Tools > Ale Toolkit > Welcome`）勾选 `ATK_LOCALIZATION`。
- `HAS_TMPRO` 无需任何替代——该路径已不再需要编译宏。
- 若你在自有代码里覆写过 `VnResponseButton`，注意 `stateTxtArray` 的类型已由
  `TextMeshProUGUI[]` / `Text[]` 变为 `Graphic[]`。

### 依赖要求

- **`com.ale.toolkit` 需 ≥ 1.7.10。** 本次的加载重试要端到端成立，还依赖 toolkit 侧的同类修复：
  1.7.10 之前的 `AddressableManager` 会把加载失败的条目以 `Done = true, Result = null`
  长驻静态表，把同地址的后续请求直接短路回 `null`——本包这一层修好了，请求也到不了 Addressables。
  两侧都修好后，失败地址才真正可重试（已实测）。

## [1.0.0] - 2026-08-13

首个以 UPM 包形式发布的版本。**功能本身与迁移前一致，本条目是对现有能力的一次完整登记**，
真正的变化只有三处：包化、程序集独立、命名空间归位。

### ⚠ 前置条件（重要）

本包的代码位于独立程序集 `Ale.VnFramework`，而 asmdef 程序集**无法引用预定义程序集 `Assembly-CSharp`**。
Dialogue System 默认没有 asmdef、其代码正落在 `Assembly-CSharp` 里，因此：

- **必须先为 Dialogue System 补上 Assembly Definitions，本包才能编译。**
  官方在 Asset Store 下载包内提供了 `CommonAssemblyDefinitions.unitypackage` 与
  `DialogueSystemAssemblyDefinitions.unitypackage`（见 `Dialogue System/Scripts/_README.txt`）。
  本包在 `Docs~/Setup/PixelCrushers/` 附了一份按官方内容整理、可直接复制到位的副本。
- 官方那套 asmdef 有一处遗漏：`DialogueSystem.asmdef` 放在 `Dialogue System/` 根目录，
  会把 `Templates/Scripts/Editor/` 下 3 个纯编辑器脚本卷进**运行时**程序集。
  这三个文件用了 `UnityEditor` 且没有 `#if UNITY_EDITOR` 保护，**编辑器下编译不报错，只在出包时失败**。
  副本里额外附了一个 `DialogueSystemTemplatesEditor.asmdef` 修掉它。详见 `Docs~/Setup/PixelCrushers/README.md`。

### 新增

- **UPM 包结构**：`package.json`（1.0.0）、`CHANGELOG.md`、中文 `README.md`、MIT `LICENSE.md`；
  `Samples~/VN Framework Demo` 登记为可从 Package Manager 导入的样例（剧情库、管理器与播放器预制体、Spine 角色与特效资源、对话 UI 与示例场景）。
  开发副本 `Assets/Samples/Ale VN Framework/1.0.0/VN Framework Demo` 与样例落地路径一致，
  故 `VnStoryManager` 的四个资源地址前缀对两者通用、导入后无需订正。
- **独立程序集 `Ale.VnFramework`**（`Runtime/Ale.VnFramework.asmdef`），引用
  `PixelCrushers` / `DialogueSystem` / `Ale.Toolkit.Runtime` / `Ale.AnimSimulatorSystem` /
  `Unity.TextMeshPro` / `Unity.Localization` / `Fs.GameFramework.Common.AudioSystem` / `Fs.Utility`。
  后两条是为 `VNS_FS_GAMEFRAMEWORK` 常驻的——`Fs.Utility` 容易漏，因为
  `AudioManager` 的基类 `MonoBehaviourSingleton<T>` 在那里，而 asmdef 引用不传递。
  Unity 对按名字解析不到的 asmdef 引用静默跳过，故没装 Fs 的工程不会收到警告。
- **包内 README 末尾新增「API 参考」章节** —— 供二次开发查阅：逐条列出五个运行时类型与音频后端的
  公开签名、默认参数与行为陷阱（`StartVnStory` 重入是空操作、`PlayAnim` 回调不触发的 5 种情形、
  `SwitchStateArray` 传空数组会隐藏角色、位移接口是速度制且未激活时瞬置等）。
  只做剧情配置的使用者无需阅读。
- **可替换的音频后端** —— `IVnAudioBackend` 接口 + `VnStoryAudio` 静态门面 +
  `NullVnAudioBackend` 默认空实现。**接入自己的音频系统只需实现四个原语并赋值给
  `VnStoryAudio.Backend`，不需要定义任何编译宏、也不需要改动本包源码。**
  四个原语带 `EVnAudioCategory`（`Bgm` / `Ambient` / `Sfx` / `Voice`）参数，便于按类别路由到
  不同 Mixer 组。演出语义（解析 `Key|音量|音调|延迟`、延迟播放、切行清理、空值即停、跨行去重）
  仍由 `VnStoryManager` 负责，后端不必重复实现。
  与 `com.ale.toolkit` 的 `ToolkitAssets` / `IAssetLoader` 是同一套模式。
  原先写死的 Fs 支持改为 `FsVnAudioBackend`，仍由 `VNS_FS_GAMEFRAMEWORK` 门控，
  启动时经 `[RuntimeInitializeOnLoadMethod]` 自动注册，并兼作接入范例；
  显式赋值优先于自动注册。
- **编辑器程序集 `Ale.VnFramework.Editor` 与欢迎窗口**（`Tools > Ale Toolkit > VN Framework > Welcome`）：
  - **前置条件自检** —— 按运行时程序集 `DialogueSystem` 的源文件列表判定
    `Templates/Scripts/Editor/` 下的纯编辑器脚本是否被卷了进去。这条**编辑器编译不报错、只在出包时失败**，
    「控制台零错误」拦不住，故单独检测；判定与 Pixel Crushers 装在哪个目录无关。
  - **插件支持（编译宏）** —— `VNS_FS_GAMEFRAMEWORK` 一键开关，经 toolkit 的 `DefineUtils.ApplyDefine` 写入；
    未检测到 Fs 时勾选会先弹确认框。
  - 版本号从 `PackageInfo` 动态读取，不写死常量。
  - 界面语言与 `ATK_*` 宏仍归 Ale Toolkit 欢迎窗口统一管理，本窗口只提供跳转按钮。

### 变更

- **命名空间 `PixelCrushers.DialogueSystem.VnStoryFramework` → `Ale.VnFramework`**，不再寄生在第三方命名空间下。
  用到 Dialogue System 类型的三个文件（`VnStoryManager` / `VnStoryPlayer` / `VnResponseButton`）改为显式
  `using PixelCrushers.DialogueSystem;`；`VnStoryManager` 另需 `using PixelCrushers;`
  （`StandardSceneTransitionManager` 位于 Common 而非 DialogueSystem 命名空间）。
  ⚠️ 对下游是**破坏性变更**：引用了这些类型的自有代码需要改 `using`。
  场景与预制体按 GUID 引用，不受影响。

### 本版本已具备的能力（登记）

- **剧情演出**（`VnStoryManager`）：背景切换（可经 Dialogue System 的 `StandardSceneTransitionManager` 做淡入淡出）、
  角色与特效预制体的加载 / 定位 / 缩放 / 卸载及补间、对话头像切换、消息提示、分支选项、
  富文本符号与打字机、多语言条目。
- **播放控制**（`VnStoryPlayer`）：按对话名启动 / 停止某段剧情，可在 Inspector 配置自动播放时机，
  也可由 `Button.OnClick` 或其他脚本触发。
- **角色动画对接**（`VnActorAnimator`）：经 `com.ale.animsimulatorsystem` 的 `AnimatorBase` 抽象，
  **后端无关**（Spine / Live2D / Unity 动画均可）。提供单条动画播放与播完回调、按名停止、换装皮肤、
  状态切换；所有播放 / 状态 / 皮肤接口一律经就绪门控，不依赖帧序。
- **无动画组件的预制体降级**：角色与特效走同一套预制体流程，没有 `VnActorAnimator` 组件的
  纯图片、纯粒子预制体同样能正常实例化与销毁（粒子按最大生命期延迟回收），不会残留实例或泄漏 Addressable 句柄。
- **对外扩展点**：`RegisterGameplaySystem` / `UnregisterGameplaySystem`（按字段标题接管玩法系统回调）、
  `RegisterVariableGetter` / `SetAllVariablesToDialogueSystem`（把宿主变量同步进 Dialogue System 的 Lua 环境）。

### 已知限制

- ~~**`HAS_TMPRO` / `HAS_LOCALIZATION` 由 `com.fs.gameframework` 维护**，不是本包也不是 Ale Toolkit 维护的。
  没装 Fs 的工程里这两个宏不会被自动定义，TextMeshPro 与 Unity Localization 的代码路径会静默关闭——
  即使 `Ale.VnFramework.asmdef` 已经引用了 `Unity.TextMeshPro` / `Unity.Localization`。
  临时办法是在 Project Settings 手工添加；后续版本考虑改用 Ale Toolkit 的 `ATK_TMP` / `ATK_LOCALIZATION`
  （二者在本仓库中同时定义，故本仓库无感）。~~ **（已在 1.1.0 解决）**
- **默认不接任何音频后端**：`VnStoryAudio.Backend` 默认为 `NullVnAudioBackend`，
  四个播放 / 停止接口是空操作，剧情全程无声。接入自己的音频系统见 README「音频接缝」；
  内置的 Fs 后端还需在 Fs 的音频系统中配置好 `AudioLibrary` 才会真正出声。
- **启用 `ATK_ADDRESSABLE` 时，导入后的样例文件夹需自行加入 Addressables 分组**。
  本包无法替使用者写入其工程的 Addressables 配置。（1.1.0 起条目地址改用固定短名 `VNFrameworkDemo`。）
- **暂无一键 Demo 向导**（欢迎窗口已就位）。
