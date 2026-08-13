# unity-visual-novel-framework-dialogue-system

A Visual Novel Framework for Unity built on top of Pixel Crushers' Dialogue System.

基于 Pixel Crushers Dialogue System 的视觉小说（Visual Novel / Galgame）剧情演出框架。
插件本体是内嵌 UPM 包 **[`Packages/com.ale.vnframework`](Packages/com.ale.vnframework/README.md)**；
本仓库同时是它的开发与演示工程（`Assets/Demo` 为可直接运行的示例场景）。

## 📦 安装

Package Manager → Add package from git URL：

```
https://github.com/AleFeng/unity-visual-novel-framework-dialogue-system.git?path=/Packages/com.ale.vnframework
```

指定版本则在末尾追加 `#<tag>`，例如 `…/Packages/com.ale.vnframework#1.0.0`。

⚠️ **装之前请先读[包内 README](Packages/com.ale.vnframework/README.md) 的「依赖与安装顺序」与
「前置条件」两节。** 依赖不会自动拉取，且 Dialogue System 必须先补上 Assembly Definitions，
否则本包编译不过。

## 📖 文档

- [包说明与快速开始](Packages/com.ale.vnframework/README.md)
- [VnStoryManager 使用文档](Packages/com.ale.vnframework/Docs~/VnStoryManager/VnStoryManager.md)
- [为 Dialogue System 补 Assembly Definitions](Packages/com.ale.vnframework/Docs~/Setup/PixelCrushers/README.md)
- [更新日志](Packages/com.ale.vnframework/CHANGELOG.md)

## 📄 许可

[MIT](LICENSE)。Dialogue System 与示例中的第三方美术资源各自遵循其原有许可，不在本许可范围内。
