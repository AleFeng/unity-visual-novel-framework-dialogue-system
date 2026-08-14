using UnityEditor;

namespace Ale.VnFramework.Editor
{
    /// <summary>
    /// 本包编辑器界面的英 / 日译表登记入口。中文是源语言、同时也是查表的 Key，故译表只登记英、日两栏。
    ///
    /// <para>通用面板文案（警告 / 确定 / 取消 / ✓ 已安装 / ⏳ 等待重新编译 / 启动时自动显示 /
    /// 插件支持（编译宏）/ 查看文档 / 文档未找到）已由 Ale Toolkit 自己的译表登记，本包不重复。</para>
    /// </summary>
    [InitializeOnLoad]
    internal static partial class VnFrameworkEditorL10NTables
    {
        static VnFrameworkEditorL10NTables()
        {
            RegisterWelcome();
        }

        // 各区域译表在对应的分部文件中实现；未实现者编译期消除，可增量补充。
        static partial void RegisterWelcome();
    }
}
