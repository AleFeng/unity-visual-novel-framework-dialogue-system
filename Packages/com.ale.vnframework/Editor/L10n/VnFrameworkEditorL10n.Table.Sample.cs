using static Ale.Toolkit.Editor.ToolkitEditorL10n;

namespace Ale.VnFramework.Editor
{
    /// <summary>
    /// 「演示样例（Addressables 登记）」区块的英 / 日译表。
    ///
    /// <para>界面在 <see cref="VnFrameworkWelcomeWindow"/>，登记逻辑在独立程序集
    /// <c>Ale.VnFramework.Addressables.Editor</c> 里。译表统一放在本程序集：
    /// 它只是往全局字典里写条目，与登记逻辑是否参与编译无关；
    /// <c>ATK_ADDRESSABLE</c> 关闭时这些条目登记了也只是没人用，无害。</para>
    ///
    /// <para>Key 必须与调用处传给 <c>Tr()</c> / <c>Fmt()</c> 的字符串<b>逐字一致</b>，
    /// 包括跨行拼接与 <c>\n</c>。</para>
    /// </summary>
    internal static partial class VnFrameworkEditorL10NTables
    {
        static partial void RegisterSample()
        {
            // ── 区块标题与说明 ────────────────────────────────────────────────
            Add("演示样例", "Demo Sample", "デモサンプル");
            Add("启用 ATK_ADDRESSABLE 后，导入的样例文件夹需要登记进 Addressables，" +
                "并把地址改成 VNFrameworkDemo——VnStoryManager 上的四个资源前缀就是按这个短地址写的。" +
                "下面的按钮代劳这一步。",
                "With ATK_ADDRESSABLE enabled, the imported sample folder must be registered in Addressables " +
                "with the address VNFrameworkDemo — the four asset prefixes on VnStoryManager are written " +
                "against that short address. The button below does it for you.",
                "ATK_ADDRESSABLE を有効にした場合、インポートしたサンプルフォルダーを Addressables に登録し、" +
                "アドレスを VNFrameworkDemo にする必要があります（VnStoryManager の 4 つのアセット接頭辞は" +
                "この短いアドレスを前提にしています）。下のボタンがこの手順を代行します。");
            Add("登记样例到 Addressables",
                "Register Sample in Addressables",
                "サンプルを Addressables に登録");
            Add("登记失败：{0}", "Registration failed: {0}", "登録に失敗しました：{0}");

            // ── 现状描述 ──────────────────────────────────────────────────────
            Add("  ⚠ 未找到已导入的样例。请先在 Package Manager 里导入 VN Framework Demo。",
                "  ⚠ No imported sample found. Import VN Framework Demo from the Package Manager first.",
                "  ⚠ インポート済みのサンプルが見つかりません。まず Package Manager から VN Framework Demo をインポートしてください。");
            Add("  ⚠ 本工程尚未初始化 Addressables 设置，点击下方按钮会一并创建。",
                "  ⚠ Addressables settings are not initialized in this project; the button below will create them.",
                "  ⚠ このプロジェクトでは Addressables 設定が未初期化です。下のボタンで併せて作成されます。");
            Add("  ⚠ 暂时读不到 Addressables 设置（可能正在编译）。稍后重试。",
                "  ⚠ Addressables settings are temporarily unavailable (compiling?). Try again shortly.",
                "  ⚠ Addressables 設定を一時的に取得できません（コンパイル中の可能性）。しばらくしてから再試行してください。");
            Add("  ⚠ 发现 {0} 个已导入的样例版本，它们会争用同一个地址。建议只保留一个。",
                "  ⚠ Found {0} imported sample versions; they would compete for the same address. Keep only one.",
                "  ⚠ インポート済みのサンプルが {0} 個見つかりました。同じアドレスを奪い合うため、1 つだけ残してください。");
            Add("  ⚠ 样例已导入但尚未登记：{0}",
                "  ⚠ Sample imported but not registered yet: {0}",
                "  ⚠ サンプルはインポート済みですが未登録です：{0}");
            Add("  ⚠ 已登记，但地址是「{0}」而不是「{1}」，资源会加载不到。",
                "  ⚠ Registered, but the address is \"{0}\" instead of \"{1}\"; assets will fail to load.",
                "  ⚠ 登録済みですが、アドレスが「{1}」ではなく「{0}」のため、アセットを読み込めません。");
            Add("  ✓ 已登记：地址「{0}」，分组「{1}」",
                "  ✓ Registered: address \"{0}\", group \"{1}\"",
                "  ✓ 登録済み：アドレス「{0}」、グループ「{1}」");

            // ── 登记结果与确认框 ──────────────────────────────────────────────
            Add("未找到已导入的样例。请先在 Package Manager 里导入 VN Framework Demo，再回来点这个按钮。",
                "No imported sample found. Import VN Framework Demo from the Package Manager, then click this button again.",
                "インポート済みのサンプルが見つかりません。Package Manager から VN Framework Demo をインポートしてから、もう一度このボタンを押してください。");
            Add("发现 {0} 个已导入的样例版本：\n{1}\n它们会争用同一个地址，请先删掉多余的版本，只保留一个。",
                "Found {0} imported sample versions:\n{1}\nThey would compete for the same address. Delete the extras and keep only one.",
                "インポート済みのサンプルが {0} 個見つかりました：\n{1}\n同じアドレスを奪い合うため、余分なものを削除して 1 つだけ残してください。");
            Add("取不到文件夹的 GUID：{0}", "Could not resolve the folder GUID: {0}", "フォルダーの GUID を取得できません：{0}");
            Add("无法获取或创建 Addressables 设置。请稍后重试，或先手工创建一次 Addressables 配置。",
                "Could not get or create Addressables settings. Try again later, or create the Addressables configuration manually first.",
                "Addressables 設定を取得または作成できません。しばらくしてから再試行するか、先に手動で Addressables 設定を作成してください。");
            Add("已经登记好了，无需改动。（分组「{0}」，地址「{1}」）",
                "Already registered; nothing to change. (group \"{0}\", address \"{1}\")",
                "すでに登録済みのため変更は不要です。（グループ「{0}」、アドレス「{1}」）");
            Add("Addressables 没有默认分组。请先在 Addressables Groups 窗口里指定一个默认分组。",
                "Addressables has no default group. Set one in the Addressables Groups window first.",
                "Addressables に既定グループがありません。Addressables Groups ウィンドウで既定グループを指定してください。");
            Add("将把文件夹\n{0}\n登记到分组「{1}」，并把地址设为「{2}」。",
                "The folder\n{0}\nwill be registered in group \"{1}\" with the address \"{2}\".",
                "フォルダー\n{0}\nをグループ「{1}」に登録し、アドレスを「{2}」に設定します。");
            Add("该文件夹已登记在分组「{0}」，但地址是「{1}」。\n将把地址改为「{2}」（不移动分组）。",
                "The folder is already registered in group \"{0}\" but its address is \"{1}\".\nThe address will be changed to \"{2}\" (the group is left as is).",
                "このフォルダーはグループ「{0}」に登録済みですが、アドレスが「{1}」です。\nアドレスを「{2}」に変更します（グループは変更しません）。");
            Add("\n\n⚠ 另有 {0} 个资源此前被单独登记过，它们会保留各自的旧地址、不受文件夹条目影响：\n{1}",
                "\n\n⚠ {0} asset(s) were registered individually beforehand. They keep their own addresses and are unaffected by the folder entry:\n{1}",
                "\n\n⚠ 個別に登録済みのアセットが {0} 件あります。これらは元のアドレスを保持し、フォルダーエントリの影響を受けません：\n{1}");
            Add("已取消，未做任何改动。", "Cancelled; nothing was changed.", "キャンセルしました。変更はありません。");
            Add("创建 Addressables 条目失败。请检查 Addressables 配置是否正常。",
                "Failed to create the Addressables entry. Check that the Addressables configuration is healthy.",
                "Addressables エントリの作成に失敗しました。Addressables 設定が正常か確認してください。");
            Add("已登记：分组「{0}」，地址「{1}」。",
                "Registered: group \"{0}\", address \"{1}\".",
                "登録しました：グループ「{0}」、アドレス「{1}」。");
            Add("（{0} 个此前单独登记过的资源保留了原地址）",
                " ({0} individually registered asset(s) kept their original addresses)",
                "（個別登録済みの {0} 件は元のアドレスを保持しています）");
        }
    }
}
