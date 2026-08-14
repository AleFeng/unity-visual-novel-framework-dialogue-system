using static Ale.Toolkit.Editor.ToolkitEditorL10n;

namespace Ale.VnFramework.Editor
{
    /// <summary>
    /// <see cref="VnFrameworkWelcomeWindow"/> 的英 / 日译表。中文为源语言，故此处只登记英、日两栏；
    /// 未登记的条目在对应语言下自动回退中文。
    ///
    /// <para>Key 必须与调用处传给 <c>Tr()</c> 的字符串<b>逐字一致</b>，包括跨行拼接的部分。</para>
    /// </summary>
    internal static partial class VnFrameworkEditorL10NTables
    {
        static partial void RegisterWelcome()
        {
            // ── 窗口标题 / 页眉 ───────────────────────────────────────────────
            Add("VN Framework 剧情演出框架",
                "VN Framework",
                "VN フレームワーク");
            Add("基于 Dialogue System 的视觉小说剧情演出框架",
                "Visual novel presentation framework built on Dialogue System",
                "Dialogue System ベースのビジュアルノベル演出フレームワーク");

            // ── 前置条件自检 ─────────────────────────────────────────────────
            Add("前置条件自检",
                "Prerequisite Check",
                "前提条件のセルフチェック");
            Add("本包是 UPM 包、代码在独立程序集里，而 asmdef 无法引用预定义程序集 Assembly-CSharp，" +
                "因此 Dialogue System 必须先有自己的 Assembly Definitions。",
                "This is a UPM package: its code lives in its own assembly, and an asmdef cannot reference the " +
                "predefined Assembly-CSharp. Dialogue System must therefore have its own Assembly Definitions first.",
                "本パッケージは UPM パッケージで、コードは独立したアセンブリにあります。asmdef は定義済み" +
                "アセンブリ Assembly-CSharp を参照できないため、Dialogue System 側に Assembly Definitions が必要です。");
            Add("  ✓ Dialogue System 程序集已就位",
                "  ✓ Dialogue System assembly is in place",
                "  ✓ Dialogue System アセンブリを確認");
            Add("  ⚠ 未找到 DialogueSystem 程序集",
                "  ⚠ DialogueSystem assembly not found",
                "  ⚠ DialogueSystem アセンブリが見つかりません");
            Add("  ✓ 编辑器模板脚本未混入运行时程序集",
                "  ✓ No editor-only template scripts leaked into the runtime assembly",
                "  ✓ エディタ専用テンプレートスクリプトはランタイムアセンブリに混入していません");
            Add("  ⚠ 有 {0} 个纯编辑器脚本被并入了运行时程序集 DialogueSystem：{1}",
                "  ⚠ {0} editor-only script(s) were merged into the runtime assembly DialogueSystem: {1}",
                "  ⚠ {0} 個のエディタ専用スクリプトがランタイムアセンブリ DialogueSystem に含まれています：{1}");
            Add("它们使用 UnityEditor 却没有 #if UNITY_EDITOR 保护。编辑器下编译不报错，" +
                "但出包（Build）时会失败。按下方文档补一个 DialogueSystemTemplatesEditor.asmdef 即可。",
                "They use UnityEditor without an #if UNITY_EDITOR guard. This compiles fine in the Editor but " +
                "fails at build time. Add a DialogueSystemTemplatesEditor.asmdef as described in the doc below.",
                "これらは #if UNITY_EDITOR で保護されずに UnityEditor を使用しています。エディタでは通り" +
                "ますが、ビルド時に失敗します。下記ドキュメントに従い DialogueSystemTemplatesEditor.asmdef を追加してください。");
            Add("查看 asmdef 设置说明",
                "View asmdef Setup Guide",
                "asmdef 設定ガイドを見る");
            Add("重新检测",
                "Re-check",
                "再チェック");

            // ── 插件支持（编译宏） ────────────────────────────────────────────
            Add("按项目实际接入的外部系统启用。TextMeshPro / Localization / Addressables 那一组 ATK_* 宏" +
                "是项目级全局设定，在 Ale Toolkit 欢迎窗口配置。",
                "Enable according to the external systems your project actually integrates. The ATK_* macros " +
                "(TextMeshPro / Localization / Addressables) are project-wide settings configured in the Ale Toolkit welcome window.",
                "プロジェクトで実際に統合する外部システムに応じて有効化します。ATK_*（TextMeshPro / " +
                "Localization / Addressables）はプロジェクト全体の設定で、Ale Toolkit のウェルカムウィンドウで設定します。");
            Add("启用后接入 Fs GameFramework 的相关系统功能。本版本只接入音频系统：" +
                "剧情演出的音频接缝 VnStoryAudio 会转调 AudioManager，BGM 按通道播放（同通道交叉淡入淡出），" +
                "环境音 / 音效 / 语音按 Key 播放。关闭时四个接口为空操作——不报错、不出声，演出流程照常推进。",
                "Integrates Fs GameFramework. This version wires up the audio system only: the VnStoryAudio seam " +
                "forwards to AudioManager — BGM plays per channel (cross-fading within a channel), while ambience / " +
                "SFX / voice play by key. When disabled, all four entry points are no-ops: no errors, no sound, " +
                "and the presentation continues normally.",
                "Fs GameFramework を統合します。本バージョンではオーディオシステムのみ接続します：VnStoryAudio " +
                "が AudioManager に転送し、BGM はチャンネル単位（同一チャンネルはクロスフェード）、環境音 / SE / " +
                "ボイスは Key 単位で再生されます。無効時は 4 つの API がすべて空操作となり、エラーも音も出ず演出は通常どおり進行します。");
            Add("  ⚠ 未检测到 {0} 的音频系统",
                "  ⚠ Audio system of {0} not detected",
                "  ⚠ {0} のオーディオシステムが検出されません");
            Add("尚未检测到 Fs GameFramework 的音频系统（Fs.GameFramework.Common.AudioSystem）。\n" +
                "启用宏后，VnStoryAudio 将无法编译。\n\n确定要继续启用吗？",
                "The Fs GameFramework audio system (Fs.GameFramework.Common.AudioSystem) was not detected.\n" +
                "With the macro enabled, VnStoryAudio will fail to compile.\n\nEnable anyway?",
                "Fs GameFramework のオーディオシステム（Fs.GameFramework.Common.AudioSystem）が検出されませんでした。\n" +
                "マクロを有効にすると VnStoryAudio はコンパイルできません。\n\nそれでも有効にしますか？");

            // ── 文档 ──────────────────────────────────────────────────────────
            Add("文档", "Documentation", "ドキュメント");
            Add("查看说明文档", "View README", "README を見る");
            Add("查看使用文档", "View Manual", "使用ドキュメントを見る");
        }
    }
}
