# 更新日志（Changelog）

本文件记录 VN Framework（`com.ale.vnframework`）的所有重要变更。

格式参考 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.0.0/)，版本号遵循 [语义化版本](https://semver.org/lang/zh-CN/)。

> 迁移说明（2026-08-13）：插件位置由 `Assets/VnStoryManager` 迁移至内嵌 UPM 包 `Packages/com.ale.vnframework`；
> 运行时代码由 `Assembly-CSharp` 独立为程序集 `Ale.VnFramework`；命名空间
> `PixelCrushers.DialogueSystem.VnStoryFramework` → `Ale.VnFramework`。脚本 `.meta` 的 GUID 全部保留，
> 既有场景与预制体的组件引用不受影响。**升级前请先读下方「⚠ 前置条件」。**

## [1.5.1] - 2026-08-15

只动样例与元数据的收尾发布。**`Runtime/` 下没有任何 `.cs` 改动**——公开 API、序列化字段与
运行时行为与 1.5.0 逐字一致，升级不需要改代码，也不会影响既有场景与存档。

### 修复

- **还原 6 个被误重新生成的 `.meta` GUID**：`README.md`、`Runtime/` 目录，以及
  `VnActorAnimator.cs` / `VnResponseButton.cs` / `VnStoryManager.cs` / `VnStoryPlayer.cs`。
  GUID 是 Unity 认资产的唯一凭据，**换了 GUID 等于换了一个资产**——使用者工程里所有指向这些脚本的
  组件引用都会变成 Missing，且预制体与场景无法自动找回。这批 GUID 已全部回到 1.5.0 时的原值，
  **从 1.5.0 直升 1.5.1 不会经历这个状态**；只有在这两次提交之间拉过 `main` 的人需要重新拉一次。

### 变更

- **样例瘦身**：删掉 Cartoon FX Remaster 中三个**未被任何预制体或脚本引用**的文件——
  `CFXR_EmissionBySurface.cs`、`CFXR_ParticleText.cs`、`CFXR_ParticleTextFontAsset.cs`（合计约 740 行）。
  样例实际在用的 `CFXR_Effect.cs` 与 `CFXR_Effect.CameraShake.cs` 保留，两个特效预制体
  （`EF_Magic_Aura`、`EF_Bouncing_Glows_Bubble`）表现不变。`Third Party Notices.md` 的脚本计数已同步。
- **样例角色预制体重新序列化**：`SP_Actor_Test_1/2.prefab` 上 `VnActorAnimator` 的 9 个字段由
  `m_ActorPosSpeed` 一类的旧名迁移到 `actorPosSpeed`——这是 1.5.0 重命名序列化字段的连带结果，
  在 Unity 打开工程时自动完成。数值与对象引用逐项保留，`actorAnimator` 的指向未丢失。
- 样例场景的 `VnStoryPlayer` 实例记下了一条 `conversationName: Conversation 1` 覆写。
  该值与预制体默认值相同，**无行为变化**。

## [1.5.0] - 2026-08-15

补上文字冒险游戏右下角那排**播放控制**按钮：自动播放、播放速度、快进、新对话停止、隐藏 UI，
以及背后的已读记录与存档接口。**Dialogue System 一行代码都没改**（它是 gitignore 的付费资产），
全部走它自带的公开接缝。

### 新增

- **`VnPlaybackController`**（与 `VnStoryManager` 同物体）——五个功能的状态持有者：
  - **自动播放**（默认关）：一句台词「打字机显示完 **且** 语音播完」，再等停留时长（默认 1 秒）后自动推进。
  - **播放速度** 1x / 2x / 3x（默认 1x，点击循环）：只影响台词打字机。
  - **快进**：长按生效、松开退出，倍率可配（默认 5x），覆盖打字机、补间、延时、角色 Animator、粒子与语音。
  - **新对话停止**（默认开）：快进中遇到从未出现过的对话节点即中止快进。
  - **隐藏 UI**：藏起全部界面，点屏幕任意位置恢复。
- **`VnPlaybackRate` + `VnTween`**：倍率权威与倍率感知的补间层。演出层 25 处 `ToolkitTween` 调用改道，
  背景交叉淡的手写协程、角色 `Animator.speed`、粒子 `simulationSpeed` 一并接入。
- **`VnReadHistory` / `VnReadHistoryCodec`**：已读判定复用 DS 的 `SimStatus`，持久化用自研位图编码。
- **存档 Get / Set**：`VnStoryManager` 实现 `ISaveable`，提供 `GetSaveData` / `LoadSaveData` / `ResetAll`。
  **本包不落盘**，Load / Save 归宿主的存档系统。
- **`IVnAudioPlaybackInfo`**：可选的音频后端能力（查询是否在播 + 设置倍速）。不实现不报错，
  只是自动播放退化为「打字机结束 + 停留时长」、快进时语音不倍速。Fs 后端已实现
  （需要 `com.fs.gameframework` ≥ 0.9.4，本次一并给它加了 `IsPlaying` / `SetPitch`）。
- **`VnPlaybackButton` / `VnUiHider`** 与 14 张白色简约图标（每个功能开 / 关两态，开态带外发光），
  样例的 `DialogueUI_Main` 右下角已接好。图标由 `Tools~/icons/generate_icons.py` 程序化生成、可重跑。
- **`VnActorAnimator` 的 `Reset()` 自动补齐引用**：把组件添加到物体上、或在 Inspector 里点 Reset 时，
  从自身与子物体上自动填好 `actorAnimator` 与 `particleSystemRoot`，省掉手工拖拽。
  只在字段为空时才填（主动重置组件时不覆盖已有配置），查找含未激活子物体。
  整段包在 `UNITY_EDITOR` 内，不进发行版程序集。

### 变更

- **修正一处历史误述**：`package.json` 描述与根 README 特性表此前写着「自动播放与跳过」，
  但代码里唯一的 auto play 是 `VnStoryPlayer.AutoPlayTiming`——它管的是**何时开始一段对话**，
  与本次的逐句自动推进不是一回事。文档已把两者分开叙述，包内 README 的
  `### 播放控制（VnStoryPlayer）` 也改名为 `### 剧情启停（VnStoryPlayer）`。
- 样例的 `VnStoryManagerBase` 预制体勾上了 Dialogue Manager 的 **Include SimStatus**（见下）。

### 四条反直觉的事实（备查）

**① 不勾 `Include SimStatus` 会 fail-open 成全量误报，不是「功能不生效」。**
`DialogueLua.GetSimStatus` 在开关关闭时**对任何节点都返回 `Untouched`**，于是每一行都被判成未读，
「新对话停止」会在快进按下的第一行就触发——现象是「快进一按就停」，很容易误判成快进坏了。
代码里有运行时补开 + 告警一次，但那段设置**必须放在 `Start` 而不是 `Awake`**：
`DialogueSystemController` 与本管理器在同一个物体上，两者 `Awake` 先后未定义，
而它的 `Awake` 里会把 `DialogueLua.includeSimStatus` 覆写回 Inspector 的值。

**② 勾上 `Include SimStatus` 的副作用是 DS 存档暴涨，必须同时把 `PersistentDataManager.includeSimStatus` 关掉。**
`DialogueSystemController.Awake` 会做 `PersistentDataManager.includeSimStatus = DialogueLua.includeSimStatus`，
不关的话 DS 会为**每个节点**再往自己的存档串写一份 `Conversation[N].SimX="..."`——
与本包的位图是同一份数据的两次存储，而且是最啰嗦的那种编码。正确答案是「勾上 + 代码里关掉后者」。

**③ 自动播放不能只写 `while (typewriter.isPlaying)`，会整行跳过。**
调用顺序是 `NotifyParticipantsOnConversationLine`（我们的处理器在此被拉起）→ `ShowSubtitle`
→ `SetContent` → `StartTyping`，**我们跑在打字机起播之前**，那一刻 `isPlaying` 还是 false。
必须用「观察到在播」作闩，配合起播宽限窗兜底。
同理也**不能用 `OnConversationLineEnd` 当「本行播完」的信号**：DS 的 `FinishSubtitle()` 开头就是
`if (!waitForContinue)`，而 `continueButton = Always` 时每行都在等继续，那个消息要等玩家点了才发——
正好是我们要产生的动作，用它会成环。

**④ 标点停顿不跟着缩放的话，速度档基本是假的。**
打字机的 `fullPauseDuration` / `quarterPauseDuration` 是**绝对秒数**，不随字速变。
样例配的是句号停 1 秒、逗号停 0.3 秒——一句 30 字的中文台词打字 1.5 秒、标点却要停 1.6 秒，
只调字速的话 3 档速实际只有约 1.48 倍。好在这两个值是每次逐字循环重读的，可以中途改且不跳变；
但基准值**只能在收集打字机时采一次**，等倍率非 1 时再读就已经被污染了。

### 已知限制

- **Spine / Live2D 的状态动画吃不到倍率**。`AnimatorBase` 没有实时时间缩放 API，
  速度在起播时就被烘进后端轨道项了。Unity `Animator.speed` 与粒子 `simulationSpeed` 正常跟随。
  补齐的位置在 `com.ale.animsimulatorsystem`，不在本包。
- **剧本 `Sequence` 里手写的 `Delay()` / `AudioWait()` 不会被压缩**（理由见下节）。
- **已读记录以「会话 ID + 节点 ID」为下标**，整库重导入（Chat Mapper / articy / CSV）会让 ID 重编号。
  存档里带了剧本结构指纹，不匹配时丢弃记录并告警，而不是静默错位。日常追加剧情、删节点不受影响。
- **中途改字速会跳字**（DS 打字循环的行为）。本包只在行边界写字速、进快进时先 `Stop()` 当前行，
  正常使用中看不到。
- 没有 `EventSystem` 时**隐藏 UI 会被拒绝**并报错——藏起来之后点不了任何东西，那是软锁。
- **没挂 `VnActorAnimator` 的纯图片 / 纯粒子预制体走绝对赋值**（`simulationSpeed = 倍率`）而非
  「作者基准 × 倍率」，因为管理器不为这类实例缓存基准，相乘会在连续变速时累乘。
  需要保住自定义基准速度的，给预制体挂上 `VnActorAnimator`。
- **资源加载门在直接模式下测不出来**：未开 `ATK_ADDRESSABLE` 时 `ToolkitAssets` 是同步回调，
  计数器在 `LoadAsset` 返回前就已清空，`IsLoadingAssets` 恒为 false。接异步 Addressables 后端后需重验。

### 评估后决定不做

- **接管 `DialogueTime`（`TimeMode.Custom`）来做全局倍速。** 它能让 DS 的全部时序——包括剧本里
  手写的 `Delay()` / `AudioWait()`——平滑倍速，且中途变速不跳字，确实比逐项乘算更彻底。
  但 `DialogueTime` 是 DS 的**全局静态量**，接管之后必须每帧驱动；组件被禁用、销毁或抛异常导致
  驱动中断，DS 时间就会冻结、对话彻底卡死。为了压缩少数手写 `Delay()` 而引入一个「一失手就卡死」
  的全局依赖，不划算。
- **复用 DS 自带的 `ConversationControl.SkipAll()` 做快进。** 它的实现是往每行 sequence 前插
  `"Continue(); "`，是**瞬间跳过**而非倍速——与需求要的「5 倍速播放」是两种东西。
  但它的 `stopSkipAllOnUnreadSubtitle` 那套 `preparingConversationLine` + SimStatus 的机制是对的，
  本包复用了**机制**、没有复用组件。
- **给已读记录做「按字段做稳定 ID」**（DS 为自己的存档提供了 `saveConversationSimStatusWithField`）。
  那会让编码从紧凑位图退回「每条一个字符串键值对」，把省下的一个数量级又赔回去。
  改用剧本指纹校验 + 丢弃告警，成本几十字节。
- **快进时给 BGM / 环境音也变调。** pitch 变速必然变调，音乐拉成 5 倍速就是噪音，
  主流 VN 的快进也不动音乐。只给语音倍速（需求点名的也只有「语音播放速度」）。

## [1.4.0] - 2026-08-14

把 **Ale Toolkit 的条件系统**接进对话节点的 Conditions：任何系统只要实现了判定器，
就会自动出现在节点的条件下拉里，可与既有的手写 Lua 条件自由组合。

**不改 Dialogue System 一行代码**（它在本工程里是 git-ignore 的第三方付费资产，改了既提交不进去、
重装即失，别人装了本包也没有），全部经它自带的两个扩展点接入。
**无破坏性变更**：既有的 `Variable["…"]` 条件、剧本配置格式、公开 API 均未改动。

### 新增

- **条件系统接入。** 两个新程序集 `Ale.VnFramework.Condition`（运行时）与
  `Ale.VnFramework.Condition.Editor`（编辑器），由 `versionDefines` 从 `com.ale.toolkit` ≥ 1.4.0
  自动推出 `VNS_HAS_CONDITION` 门控——**不给使用者增加一个要记得去勾的开关**，
  toolkit 若降级则功能连同程序集静默消失，工程不会编译失败。
  两个核心程序集 `Ale.VnFramework` / `Ale.VnFramework.Editor` **一行未动**。
- **`VnConditionSources`** —— 宿主接线数值 / 标记 / 领域服务的公开入口。
- **登记资产的生成与自动同步** —— 菜单
  `Tools ▸ Ale Toolkit ▸ VN Framework ▸ Generate Condition Lua Functions` 首次创建（弹框征得同意），
  此后每次脚本重编译比对内容、**有差异才写**。
  - 为什么必须自动同步：第三方新增判定器后若还要使用者记得来点一次按钮，漏点的表现是
    「新条件不出现在下拉里」，**没有任何提示**。

### 设计要点

- **数值 / 标记只来自宿主的实时 getter，不读 Dialogue System 的 Variable。**
  求值当刻才回调宿主，故对话**中途**宿主的值变了会立刻生效。这是与
  `RegisterVariableGetter` 的关键差异——后者只在每段对话开始时推一次
  （`SetAllVariablesToDialogueSystem`），对话进行中取到的是旧值。
- **未接线的 id 返回 `NaN` 而不是 `0`。** `NumberCompareEvaluator` 的五个分支
  （`>` `≥` `=` `≤` `<`）对 NaN 全部为假，于是「忘了接线」表现为节点进不去。
  若回落成 `0`，`时间段 ≤ 5` 这类条件会**静默通过**，判定形同虚设——那是最难查的一类 bug。
  （实测：阈值取 0、五种比较全部判 false；告警按 id 去重，只出一条。）
- **Lua 函数名保留完整的判定器 Key、且只用 ASCII。** 这些字符串会存进使用者的对话库，
  命名规则一改，已存的条件就静默失效；而缩短规则依赖「当前有哪些判定器」这个全集，
  日后新增一个就可能撞名、迫使旧名字改写。难看但永不漂移。
- **带固定选项的参数传标签而非索引**（`"大于等于"` 而不是 `1`）。Dialogue System 没有通用枚举下拉，
  传标签至少让生成的 Lua 一眼看得懂，也扛得住日后调整选项顺序。拼错会告警并列出全部合法选项。
- **命名与跳过规则由 `VnConditionNaming.TryPlan` 单点决定**，运行时桥与编辑器生成器共用。
  两边若各写一遍并漂移，登记资产会列出桥没注册的函数——向导里能选、运行期报
  `attempt to call nil`，而编辑期毫无征兆。

### 三条反直觉的事实（备查）

- **条件向导认的是「资产扫描」而不是 Lua 环境。** 它走
  `AssetDatabase.FindAssets("t:CustomLuaFunctionInfo")`，**从不读 `Lua.Environment`**。
  所以函数注册得再好，工程里没有那份资产，下拉里一个都不会出现。
  （另：资产名叫 `DialogueSystemLuaFunctionInfo` 会被当成内置表、跑进 **Misc** 而非 **Custom**。）
- **`functionName` 只有最后一段是真函数名。** 它支持用 `/` 分子菜单，但生成调用语句时走
  `Tools.GetAllAfterSlashes` 只取末段。所以菜单叶子段**同时就是** Lua 标识符，
  分类只能塞在前面几段——这也是菜单里显示 `AleCond_*` 而不是中文显示名的原因。
- **Lua 传进来的数字是装箱 `float`，不是 `double`。** 见
  `LuaInterpreterExtensions.LuaValueToObject`。反射绑定只允许加宽，声明 `int` 形参会当场抛异常；
  且实参个数必须与 C# 签名**严格一致**（无变参、无默认值），差一个就是
  `TargetParameterCountException`。故形参一律 `object`、并按元数准备 `Eval0`…`Eval8` 一族方法。

### 已知限制

- 条件求值是**同步**的：`Lua.IsTrue` 没有异步扩展点，判定器里不能等 IO。
- 参数超过 8 个的判定器会被跳过并告警。
- 手改剧本里的实参个数会抛 `TargetParameterCountException`，增删参数请用向导重新生成。
- 返回值必须是真 `bool`：`Lua.Result.asBool` 对非 boolean 走 `ToString() == "True"`，
  `return 1` 会被判成 false。生成的记录一律声明 `returnValue = Bool`，向导据此自动补 `== true`。

### 评估后决定不做

- **在节点 Inspector 里内嵌 Toolkit 原版的两级 AND/OR 条件抽屉。** 接缝确实存在且可用
  （`DialogueEditorWindow.customDrawDialogueEntryInspector` 是可跨程序集订阅的 `static event`，
  求值可挂 `DialogueManager.isDialogueEntryValid`），但有两条硬伤：
  ① 条件不在 Conditions 框里——**框里空着、节点却进不去**，比手写 Lua 更难排查；
  ② `isDialogueEntryValid` 是**单一全局委托**，宿主若也用就互相覆盖，且它对**每个候选节点**都触发，
  还要 `Field.LookupValue` 取自定义字段（1.3.0 刚量过这类查找的量级）。
  若将来确有配置复杂表达式的需求，应在现方案**之上追加**而不是替换。
- **把 Script（`userScript`）侧也接上 Toolkit 的效果系统。** 结构完全对称
  （`CustomLuaFunctionInfo.scriptFunctions` + `LuaScriptWizard`），但本次诉求只涉及 Conditions。
- **自定义 Field 绘制器。** `CustomFieldTypeService.CreateClassFromString` 硬拼
  `PixelCrushers.DialogueSystem` 命名空间，且只在 `DialogueSystemEditor` / `Assembly-CSharp-Editor`
  等三个程序集里找类型，放进本包 asmdef 会**静默降级成纯文本框**。

## [1.3.0] - 2026-08-14

清掉 1.2.0 留下的四条「本次不做」。**逐条调查后发现其中三条的前提写错了**，
且错的方向各不相同——所以本次真正做的事与当初记下的并不一样。

**无破坏性变更**：公开 API 与剧本配置格式均未改动，四个消息处理器由 `private` 提升为
`public virtual` 属于纯加法。

### 修复

- **同一地址并发加载会让 Addressable 句柄永久滞留。** 同一行里两个槽位配了同一个预制体时
  （3 个角色槽 + 3 个特效槽，很现实），两次 `LoadAsset` 都在回调之前发生、都未命中缓存，
  于是各向后端请求一次。后端（`AddressableManager`）**按地址去重，并不会重复取句柄**，
  但它的引用计数被加了两次，而本包只在自己的计数归零时释放**一次**——
  计数 2 → 1，永远到不了 0，句柄与资源就此常驻。
  现确立不变式：**同一地址同时只有一次在途加载**，后续请求只排队不重发，
  排空时各自补齐计数，使「一次获取 ↔ 一次释放」重新成立。
  （实测：并发 2 次与 3 次，后端引用计数均为 1，卸载相同次数后条目消失、`LoadedCount` 归零。）

### 变更

- **演出流水日志改为编译期可裁。** 切头像 / 切背景 / 加载角色这 4 条 `Debug.Log` 逐行对话都会刷，
  发行版里会淹掉玩家的日志文件。现改走 `LogVerbose`，由
  `[Conditional("UNITY_EDITOR")]` + `[Conditional("VNS_VERBOSE_LOG")]` 门控：
  编辑器内照常输出，发行版默认静默，需要时定义 `VNS_VERBOSE_LOG` 即可打开。
  18 条 `LogWarning` 是真告警，一条未动。
  - ⚠️ 关键在于**为什么不是运行时开关**：`Debug.Log($"…")` 的插值串在**调用之前**就构造好了，
    `Debug.unityLogger` 的过滤、乃至 `if (flag)` 守卫都救不了实参求值。
    `[Conditional]` 是**调用点**消除，连实参一起从 IL 里消失。
    （实测：同一份源码只差一个 `/define`，未定义时实参求值 **0 次**、方法体 IL 只剩 1 字节。）
- **Dialogue System 的 4 个消息处理器改为 `public virtual` 并加 `[Preserve]`。**
  它们只被 `BroadcastMessage` **按方法名字符串**调用，代码里零引用——对 IL2CPP 的静态分析器
  而言是「没有任何调用方的私有方法」，教科书级的裁剪目标。
  Pixel Crushers 自己的组件正是 `public virtual`（其插件内有 32 处），本包此前比任何一个都严。
  顺带成为正经的覆写点，XML 注释里写明了**不调 `base` 的后果**
  （`OnConversationLine` 不调等于关掉全部演出；`OnConversationEnd` 不调会漏掉资源回收）。
  六个分发子方法仍保持 `private`，不扩大公开面。
  - 现状是**潜伏而非发作**：`managedStrippingLevel` 未设、走默认 Low，不裁用户程序集；
    调到 Medium / High 才会发作（Android 已是 IL2CPP）。
- **头像与背景重复设为同一资源时不再重复加载。** 两者此前都不与当前值比对，
  连续几行配同一张就会重走「加载 → 计数增减 → 重启协程 → 对自己做一次交叉淡入」。
  - ⚠️ 头像按 **「地址 + `actorName`」** 比对，不能只比地址：`SetActorPortraitSprite` 是按角色绑定的，
    同一张图给另一个角色时仍需重新绑一次，只比地址会漏绑。
  - ⚠️ 背景还要求**没有待触发的清理**：`ClearAllBackground` 是延时执行的，
    若在那个窗口内又请求同一张背景就直接早退，稍后清理照样落下来、背景会消失。

### 评估后决定不做

- **把每行对话的 `Field.LookupValue` 换成字典 —— 已评估，永久关闭。**
  实测数据（样例库 33 个节点、每节点 21–33 个字段）：**每行 44.5 次查找、约 1050 次
  `string.Equals`、约 89 次分配（3.9 KB）、10–20 µs**。
  确实全是线性扫描 + 每次分配一个捕获闭包，但**本包不存在逐帧代码**
  （`Update` / `LateUpdate` / `FixedUpdate` / `OnGUI` 一个都没有），
  这是每 2–4 秒随对话行推进一次的离散事件——约合一帧预算的 0.1%。
  - 1.2.0 记的「穿透十几个方法的签名重构」也是错的：18 处查找全在 6 个**单调用点**私有方法里，
    不需要改任何签名，约 30 行即可。**范围小不构成理由**——收益近乎为零，
    而引入字典会多出一条语义细节（重名字段时 `List.Find` 取第一个，字典必须 keep-first）
    与一条新约束（那 6 个方法只能从 `OnConversationLine` 调）。
  - 若将来槽位数组从 3 扩到 8、或多语言字段大幅增加，可重新评估：那两个方向会同时放大
    查找次数与扫描长度。顺带一提，Dialogue System 自己每行也在做同样的事，这部分开销去不掉。

### 已知限制（1.3.0 现状）

- **默认仍不接任何音频后端**：`VnStoryAudio.Backend` 默认是 `NullVnAudioBackend`，剧情全程无声。
  音频系统本就该独立，本包只提供开放接口由第三方对接。
- `VnActorAnimator` 的单条动画播放与换装接口只能从 C# 调用，尚无对应的剧本字段标题。
- 循环环境音每行对话都会被 `ClearAllSfx` 停掉，需要每行重复声明。

## [1.2.0] - 2026-08-14

对整个包做了一次全量扫描（运行时约 3300 行、编辑器约 700 行、包元数据与文档），
修掉的问题比预期多，**其中几条命中的是主线用例而非边角情况**。
另新增你点名的 **Addressables 样例条目一键登记**。

**无破坏性变更**，公开 API 与剧本配置格式均未改动。

### 新增

- **欢迎窗口新增「演示样例」区块：一键把样例文件夹登记进 Addressables**，
  地址自动设为固定短名 `VNFrameworkDemo`，替代此前写在 README 里的手工拖拽步骤。
  - **幂等**：已登记且地址正确时只报告现状、不动配置（实测：点击前后 `AddressableAssetSettings.asset`
    与分组资产的 MD5 完全一致）。
  - **写入前弹确认框**。`com.ale.toolkit` 的既定策略是「绝不改写使用者的 Addressables 配置」，
    本按钮是这套生态里第一个写入方，因此必须让使用者明确点头。
  - 样例路径**靠枚举获得而非按版本号拼接**——落地路径盖的是**导入当时**的包版本，
    与升级后的 `package.json` 不是一回事（本仓库就正好是 `…/1.0.0/…` 而包已是 1.1.0）。
    「未导入」与「多版本并存」两种情况都会明确报出。
  - 预扫描出此前被**单独**登记过的资源：它们会被文件夹条目静默跳过、保留旧地址，
    不报出来的话使用者只会看到「个别资源加载不到」而毫无线索。
  - 实现位于独立程序集 `Ale.VnFramework.Addressables.Editor`，经静态 `Action` 钩子注入欢迎窗口；
    **核心编辑器程序集对 Addressables 零引用**，宏关闭时整块界面自动消失。
    除 `ATK_ADDRESSABLE` 外另加一道由 `versionDefines` 推出的 `VNS_HAS_ADDRESSABLES`——
    前者是手工宏，光有它不能证明包装了。
- **`Third Party Notices.md`**：逐项声明 `Samples~` 内的第三方内容
  （Cartoon FX Remaster、Spine 骨骼数据）与前置依赖 Dialogue System 各自的许可。
  `LICENSE.md` 的 MIT 只覆盖本包自己的代码与文档。
- `VnFrameworkDefineChecker` 增加**宏与运行时的一致性告警**：
  `VNS_FS_GAMEFRAMEWORK` 开着但 Fs 音频系统缺席时给出可读提示，
  而不是让使用者直接吃 `FsVnAudioBackend.cs` 的一堆 `CS0246`。

### 修复

**这三条是必现的，且都在主线路径上：**

- **剧情自然播完后，再播任何一段都是静默空操作。** `StartVnStory` 整个方法被
  `_isVnStoryStarted` 早退守卫罩着，而该标记只由 `StopVnStory` 清除——自然结束走不到那里
  （`VnStoryPlayer.Stop` 因 `IsConversationActive` 已为 false 而早退，其结束回调也只翻自己的 `IsPlaying`）。
  现把「会话级淡入」与「启动对话」拆开：前者仍幂等，后者不再被吞掉。
- **`FadeOutUI` 的兜底分支必然 NRE。** 该分支的前提就是 `uiCanvasGroup` 为空，却又去解引用它；
  且异常发生在 `onComplete` 之前，`StopStoryConversation` 因此永远执行不到、对话停不下来。
  改为关闭 `GraphicRaycaster`（`blocksRaycasts` 的对应物）而不是 `SetActive(false)`——
  同方法上方已注明 Dialogue System 需要 `uiCanvas` 保持激活。并补上两者皆空时缺失的回调。
- **`ClearAllBGM` 用错了后端原语。** BGM 由 `PlayWithChannel` 按通道播出，这里却用 Key 路径的
  `Stop` 去停。任何合规的 `IVnAudioBackend` 实现都找不到那个 Key，**BGM 会在对话结束后继续播**。
  内置的空后端掩盖了它，接入真实后端才会暴露。

**资源与生命周期：**

- **`SetBackgroundImageCoroutine` 的两条 `yield break` 快路径跳过了上一张背景的卸载**，
  而下次换背景会覆写 `_backgroundAssetNameLast`，那个地址的句柄再也放不掉——
  `backgroundFadeDuration` 设 0 时**每换一次背景漏一个**。头像的失败分支同形。
  现把卸载移进 `try/finally`，正常结束、每条 `yield break`、外部 `StopCoroutine` 全部覆盖。
- **背景加载的迟到回调会让「最后完成的」而不是「最后请求的」胜出**，快进时画面停在中间某一张。
  回调改为携带发起时的地址，与当前地址不符即释放并丢弃。背景协程同时改为持句柄、启动新的之前先停旧的。
- **`Sprite.Create` 产生的运行时 Sprite 从未销毁**（`UnloadAsset` 释放的只是背后的纹理句柄），
  每换一次头像 / 背景各泄漏一个。现以地址为键登记，在释放纹理之前销毁——
  顺序不能反，且早一步销毁会让交叉淡出中的旧图凭空消失。
  两处 `Sprite.Create` 合并为一个 `ResolveSprite`，顺带修掉背景那处把 pivot 传成**像素中心**的错误
  （该参数是相对 rect 归一化的；此路径当前不可达，属潜在缺陷）。
- **`ClearAllActors` 抵消了它自己刚触发的优雅销毁**：紧随 `UnloadActorPrefab` 的第二个循环
  把所有实例同帧 `Destroy`，Spine 淡出与在途粒子的延迟回收全部作废。已删除该循环。
- **`OnDestroy` 补 `Lua.UnregisterFunction`。** Dialogue System 的 `RegisterFunction` **不是覆盖语义**
  （实测：不反注册时第二次注册被忽略、旧绑定保留），不反注册会让下一个实例的注册失效，
  剧本里的 `BackgroundFadeDuration()` 从此驱动已销毁的对象。
- **`VnStoryAudio` 与 `NullVnAudioBackend` 的静态字段补 `SubsystemRegistration` 重置。**
  关闭「Reload Domain」后它们会跨播放会话存活，且残留的后端会让 `FsVnAudioBackend.Install`
  的自动注册**永久跳过**。
- **`VnActorAnimator.MarkActorReady` 不可重入**：排空队列用的是实例暂存表，
  而挂起操作可以合法地同步触发重入，重入会 `Clear` 掉外层正在枚举的同一张表。改为摘表到局部。
- `ExecuteInit` 在动画播放器未激活时不再留下**永不排空**的待办闭包（此前逐行对话无限堆积）。
- **玩法系统与变量 getter 的回调改为先快照再遍历**：回调是宿主代码，在其中注册 / 反注册
  会当场抛 `InvalidOperationException`——对「小游戏结束后登记后续处理器」这类用法太自然了。
- `VnStoryPlayer` 的结束事件**校验对话名**（`conversationEnded` 对任何对话都触发，
  别人结束会误清本组件的 `IsPlaying` 并误发 `onPlayEnded`）；`Play` 补重入守卫。
- `FsVnAudioBackend` 补 `AudioManager` 空值守卫，兑现接缝「不报错、不出声、不抛异常」的契约。

**数值解析（影响所有非英语区域的使用者）：**

- **剧本里的全部数值改用 `CultureInfo.InvariantCulture` 解析。** 此前 18 处 `float.TryParse`
  都不带 `IFormatProvider`，跟随运行机器的区域设置。实测两种失败模式：
  **de-DE / pt-BR 下 `"1.5"` 会解析成 `15`（`.` 是千位分隔符）且 `TryParse` 返回 `true`**，
  ru-RU / fr-FR 下解析失败得 `0`。前者更隐蔽——配 `x=1.5` 的角色会跑到 `x=15`，
  而任何「检查返回值」的防御都拦不住。
- **解析失败不再冲掉默认值。** `float.TryParse` 失败会把 out 参数写成 `0`，把调用方预设的哨兵 `-1`
  抹掉，于是「配错了」与「配了 0」变得无法区分。
- **音量 / 音调的判据由 `<= 0` 改为 `< 0`**（哨兵本就是 `-1`）。此前 `音量=0`（本意静音）
  会被当成未配置而放成满音量；负音调（倒放）也表达不出来。
- **位移 / 旋转 / 缩放的时长补除零与 NaN 守卫。** 速度为 0 得 `Infinity`（补间永不完成）；
  已在目标点且速度为 0 得 `NaN`，而 `ToolkitTween` 只挡 `duration <= 0`，
  **`NaN <= 0` 为 `false`**，NaN 会一路写进 `transform` 污染整条子层级。

**编辑器：**

- **`Ale.VnFramework.Editor.asmdef` 删掉零引用的 `Ale.VnFramework` 与 `Ale.Toolkit.Runtime`。**
  这条引用是**反向承重**的：Dialogue System 没补 asmdef 时运行时程序集编译失败，
  Unity 连带跳过编辑器程序集 → 菜单项消失 → 使用者**恰好在最需要的时候**看不到那句
  「怎么补 asmdef」的说明。本次性价比最高的一处改动。
- **文档按钮改用 `PackageInfo.resolvedPath` 解析路径。** 此前用的
  `Path.GetFullPath("Packages/com.ale.vnframework/…")` 是纯 .NET 字符串拼接，只有**内嵌包**才碰巧成立；
  经 git URL 安装时包位于 `Library/PackageCache/…@<hash>/`，三个文档按钮**全部**弹「文档未找到」。
- 删除指向不存在目录、恒返回 `null` 的 Logo 读取代码（整块从未渲染过任何东西）。
- 补两个此前只因隔壁包共用全局译表而「看起来正常」的 L10n 键——干净工程里只装 toolkit + 本包会回落中文。
- 修 `_pendingRecompile` 在 Layout / Repaint 之间控件数错配导致的 `ArgumentException`。

### 变更

- `Ale.VnFramework.asmdef` 移除已无引用的 `Unity.TextMeshPro`（1.1.0 已清空包内的 TMP 专属类型）。
  **`Fs.Utility` 保留**——`AudioManager.Instance` 是继承自其中 `MonoBehaviourSingleton<T>` 的成员，
  宏开启时编译器需要该程序集才看得见它。
- 四个字符串参数解析器改为 `static`（它们不触碰任何实例状态）。
- 删除样例中不应随包发布的 `Data/StoryDatabase (Auto-Backup).asset`（Dialogue System 的自动备份）。
- 文档同步：根 README 的固定版本示例跟进到当前 tag、登记说明改为指向欢迎窗口按钮（保留手工兜底）；
  订正包内 README 中「唯一程序集（无编辑器代码）」与同段落下方并列的 `Editor/` 自相矛盾。

### 评估后决定不做

- **压缩 `Docs~` 下的 65 张截图。** 无损再压确实可行（34.02 → 29.04 MB，省 14.6%，
  65 张全部通过对照 git HEAD 的逐像素校验），但**提交它是负收益**：git 保留每个版本、旧 blob 不会消失，
  于是 `clone` 与 UPM 安装要**多下约 29 MB**，只换来工作区少 5 MB——而「减小下载量」正是这件事的初衷。
  唯一有效的做法是重写历史，那已另行决定不做。
  - 附带两条实测结论，供日后参考：**降尺寸不是省体积的办法**——缩到 1920px 反而**变大**到 31.46 MB，
    重采样把截图里大片纯色块变成渐变，破坏了 PNG 最擅长压的游程结构；
    这些截图颜色数远超 256，无损调色板转换用不上，只剩 deflate 层面的收益。

### 已知限制（1.2.0 现状）

- **默认仍不接任何音频后端**：`VnStoryAudio.Backend` 默认是 `NullVnAudioBackend`，剧情全程无声。
  音频系统本就该独立，本包只提供开放接口（`IVnAudioBackend` 四个方法）由第三方对接。
- **暂无一键 Demo 向导**（欢迎窗口已就位）。
- 每行对话约 45 次 `Field.LookupValue`，每次都是带闭包的线性扫描；热路径上另有无条件 `Debug.Log`。
  收敛这两条需要穿透十几个方法的重构，等有实测的性能问题再动。
- `VnActorAnimator` 的单条动画播放与换装接口只能从 C# 调用，尚无对应的剧本字段标题。
- 循环环境音每行对话都会被 `ClearAllSfx` 停掉，需要每行重复声明。

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
- ~~**启用 `ATK_ADDRESSABLE` 时，导入后的样例文件夹需自行加入 Addressables 分组**。
  本包无法替使用者写入其工程的 Addressables 配置。（1.1.0 起条目地址改用固定短名 `VNFrameworkDemo`。）~~
  **（已在 1.2.0 解决：欢迎窗口提供一键登记）**
- **暂无一键 Demo 向导**（欢迎窗口已就位）。
