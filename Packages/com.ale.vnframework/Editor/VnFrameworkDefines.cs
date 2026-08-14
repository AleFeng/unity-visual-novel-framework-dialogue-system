using Ale.Toolkit.Editor;

namespace Ale.VnFramework.Editor
{
    /// <summary>
    /// 本包自有的编译宏与其依赖探测。
    ///
    /// <para><c>ATK_*</c> 那一组（TextMeshPro / Localization / Addressables / Input System）是<b>项目级全局设定</b>，
    /// 归 Ale Toolkit 的欢迎窗口统一管理，本包不重复登记也不代为开关。</para>
    ///
    /// <para>安装探测一律走命名空间反射（<see cref="DefineUtils.HasNamespace"/>）而非
    /// <c>PackageInfo</c>——被探测方未必以 UPM 包形式安装。</para>
    /// </summary>
    public static class VnFrameworkDefines
    {
        // ── 宏名常量 ─────────────────────────────────────────────────────────
        /// <summary>接入 Fs GameFramework（本版本只接音频系统）。</summary>
        public const string FsGameFramework = "VNS_FS_GAMEFRAMEWORK";

        // ── 依赖的包名（仅用于界面显示） ────────────────────────────────────
        public const string PackageFs = "com.fs.gameframework";

        // ── 探测用命名空间 ───────────────────────────────────────────────────
        private const string NsFsAudio = "Fs.GameFramework.Common.AudioSystem";

        /// <summary>宏是否已在 PlayerSettings 中启用。</summary>
        public static bool IsFsGameFrameworkEnabled() => ToolkitDefines.IsDefineEnabled(FsGameFramework);

        /// <summary>Fs GameFramework 的音频系统是否已存在于当前工程。</summary>
        public static bool IsFsGameFrameworkInstalled() => DefineUtils.HasNamespace(NsFsAudio);
    }
}
