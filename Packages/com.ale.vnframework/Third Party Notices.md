# 第三方内容声明（Third Party Notices）

`LICENSE.md` 的 MIT 授权覆盖的是**本包自己的代码与文档**（`Runtime/`、`Editor/`、`README.md`、
`Docs~/VnStoryManager/`）。本文件列出包内**不属于**该授权范围的第三方内容，它们各自遵循原有许可。

> 简言之：`Samples~` 里的美术与特效资源、以及 `Docs~/Setup/` 针对的付费插件，
> **都不随本包的 MIT 授权一并授予**。将样例内容用于你自己的项目前，请确认你已持有对应授权。

---

## 1. Cartoon FX Remaster — `Samples~/VN Framework Demo/Assets/Effects/CFXR Assets/`

- **来源**：Jean Moreno，Unity Asset Store 付费资产（Cartoon FX Remaster）
- **版权**：`(c) 2012-2025 Jean Moreno`（见 `Scripts/CFXR_Effect.cs` 等文件头）
- **包含**：`Graphics/`（贴图与材质）、`Meshes/`、`Shaders/`、`Scripts/`（5 个 C# 脚本）
- **许可**：遵循 Unity Asset Store 的
  [Asset Store EULA](https://unity.com/legal/as-terms)。**不在本包 MIT 授权范围内。**
- **说明**：仅作为演示样例中两个特效预制体（`EF_Magic_Aura`、`EF_Bouncing_Glows_Bubble`）的
  依赖资源随样例分发。若你的项目要使用它们，需自行从 Asset Store 获得授权。
  不需要这些特效时，删除 `Assets/Effects/` 整个目录不影响框架本体。

## 2. Spine 骨骼数据 — `Samples~/VN Framework Demo/Assets/Actors/Actor_Test_*/`

- **包含**：`*.skel.bytes`、`*.atlas.txt`、`*_SkeletonData.asset` 及配套贴图
- **说明**：这些是演示用的角色骨骼数据。**运行它们需要 Spine Runtime**，
  而 Spine Runtime 由 Esoteric Software 单独授权
  （[Spine Runtimes License](http://esotericsoftware.com/spine-runtimes-license)），
  使用者需持有有效的 Spine 授权。本包不分发 Spine Runtime 本身。
- 框架对动画后端无关：不使用 Spine 时，`VnActorAnimator` 可对接 Live2D 或 Unity 动画，
  纯图片 / 纯粒子预制体也能正常工作。

## 3. Pixel Crushers Dialogue System — 前置依赖，**本包不分发**

- **来源**：Pixel Crushers，Unity Asset Store 付费资产
- **说明**：本框架构建于 Dialogue System 之上，但**不包含它的任何代码或资源**，使用者需自行购买安装。
- `Docs~/Setup/PixelCrushers/` 下的 6 个 `.asmdef` 是**本项目原创**的配置文件
  （按官方 `_README.txt` 的说明整理，并补了一个官方遗漏的编辑器程序集），
  随本包以 MIT 授权分发；但它们**针对**的是上述付费资产。

## 4. 演示用的背景、头像与 UI 资源 — `Samples~/VN Framework Demo/Assets/`

`Backgrounds/`、`ActorsHead/`、`Emoji/`、`UI/` 下的图片与预制体仅供**演示与学习**，
用于评估本框架的演出效果。请勿直接用于商业发行；商业项目请替换为你自己的美术资源。

---

## 依赖的其他 UPM 包（不随本包分发）

| 包 | 许可 |
| --- | --- |
| [`com.ale.toolkit`](https://github.com/AleFeng/unity-ale-toolkit) | MIT |
| [`com.ale.animsimulatorsystem`](https://github.com/AleFeng/unity-ale-anim-simulator) | MIT |
| `com.unity.addressables` / `com.unity.localization` / TextMeshPro | Unity Companion License |

---

如果你认为本文件遗漏或错误标注了某项内容，请在
[Issues](https://github.com/AleFeng/unity-visual-novel-framework-dialogue-system/issues) 中指出。
