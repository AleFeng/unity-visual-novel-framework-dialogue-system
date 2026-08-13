# 为 Dialogue System 补 Assembly Definitions

## 为什么必须做这一步

`com.ale.vnframework` 是 UPM 包，包内代码**必须**由 asmdef 覆盖（Unity 不会把包里的脚本编进
`Assembly-CSharp`）。而 asmdef 程序集**无法引用预定义程序集** `Assembly-CSharp`——
Dialogue System 默认没有 asmdef，它的代码正落在那里。

所以：**Dialogue System 不先有 asmdef，本包就编译不过**，报错形如
`error CS0246: 找不到 'DialogueManager' / 'StandardUIResponseButton'`。

这不是本包的特殊要求。任何以 UPM 包形式依赖 Dialogue System 的插件都会撞到同一堵墙，
Pixel Crushers 官方也因此提供了 asmdef 方案（见
`Assets/.../Pixel Crushers/Dialogue System/Scripts/_README.txt`）。

## 做法

本目录下 6 个 `.asmdef` 已按**目标相对路径**摆好，直接整体复制到你的 Pixel Crushers 安装目录即可。
下表的「放置位置」相对于 `Pixel Crushers/` 文件夹本身（它可能在 `Assets/Plugins/Pixel Crushers/`、
`Assets/Pixel Crushers/` 或任何你放它的地方）：

| 文件 | 放置位置 | 程序集名 | 类型 |
|---|---|---|---|
| `PixelCrushers.asmdef` | `Common/` | `PixelCrushers` | 运行时 |
| `PixelCrushersEditor.asmdef` | `Common/Scripts/Editor/` | `PixelCrushersEditor` | 编辑器 |
| `PixelCrushersWrappersEditor.asmdef` | `Common/Wrappers/Editor/` | `PixelCrushersWrappersEditor` | 编辑器 |
| `DialogueSystem.asmdef` | `Dialogue System/` | `DialogueSystem` | 运行时 |
| `DialogueSystemEditor.asmdef` | `Dialogue System/Scripts/Editor/` | `DialogueSystemEditor` | 编辑器 |
| `DialogueSystemTemplatesEditor.asmdef` | `Dialogue System/Templates/Scripts/Editor/` | `DialogueSystemTemplatesEditor` | 编辑器 ⚠ 见下 |

放好后回到 Unity 等待重新编译。验收标准：控制台零错误，且
`Tools → Pixel Crushers → Dialogue System` 菜单仍在（说明编辑器程序集也正常）。

顺带一提，若你的工程开启了代码剥离（code stripping），Dialogue System 要求把
`Dialogue System/Templates/Link/link.xml` 换成同目录的 `link_asmdef.xml`——因为 sequencer 走反射。

## 前 5 个来自官方，第 6 个是补丁

前 5 个的 `name` / `references` / `includePlatforms` 逐字取自官方的
`CommonAssemblyDefinitions.unitypackage` 与 `DialogueSystemAssemblyDefinitions.unitypackage`。
两点说明：

- `PixelCrushers` 与 `PixelCrushersEditor` 在两个官方子包里各有一份，内容不同。
  这里采用 **DialogueSystem 子包中的较新版本**（引用更完整）。
- `PixelCrushersWrappersEditor` 只在 Common 子包里有，且是 2019 之前的旧 schema
  （带已废弃的 `optionalUnityReferences`）。这里按现代 schema 重写，
  `name` / `references` / `includePlatforms` 三项与官方一致。

**第 6 个 `DialogueSystemTemplatesEditor.asmdef` 不是官方的，是修官方遗漏的补丁。**

官方把 `DialogueSystem.asmdef` 放在 `Dialogue System/` 根目录，作用域覆盖整个子树。
而在 asmdef 的作用域内，`Editor` 这个目录名**不再具有特殊含义**，于是
`Templates/Scripts/Editor/` 下这 3 个文件会被并入**运行时**程序集：

- `ConverterWindowTemplate.cs`
- `CustomFieldType_Conversation.cs`
- `CustomFieldType_TemplateType.cs`

三者都 `using UnityEditor` 且**没有 `#if UNITY_EDITOR` 保护**。由于编辑器下运行时程序集同样能访问
`UnityEditor`，**编辑器里编译完全不报错，问题只在出包（Build）时才暴露**——
「控制台零错误」这道关卡拦不住它。补上这个 `includePlatforms: ["Editor"]` 的 asmdef 即可隔离。

若你不需要这些模板脚本，直接删掉 `Dialogue System/Templates/Scripts/` 也是等效的做法。

## 关于未解析的引用

`PixelCrushers.asmdef` 里有一条指向 Cinemachine 的 GUID 引用，`DialogueSystem.asmdef` 按名字引用了
`Cinemachine` / `Unity.Cinemachine` / `LoveHate` / `LoveHateEditor`。
这些是**官方有意留下的可选集成**：未安装时 Unity 会静默跳过，不影响编译，也不会刷警告。
无需删除。

## 这些文件为什么放在这里

Dialogue System 是付费资产，通常不进版本库（本仓库把它放在被 gitignore 的
`Assets/PluginsIgnore/` 下）。因此直接加在插件目录里的 asmdef 也提交不到仓库、
且会被插件升级覆盖。放一份副本在包内，是为了让这一步**可复现**：
重装或升级 Dialogue System 之后，照着本文再复制一次即可。
