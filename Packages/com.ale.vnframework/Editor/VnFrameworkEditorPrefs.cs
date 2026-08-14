namespace Ale.VnFramework.Editor
{
    /// <summary>
    /// 本包编辑器侧的偏好键。
    ///
    /// <para>分工遵循生态惯例：每人自定的偏好走 <c>EditorPrefs</c>（不入库），
    /// 每会话一次性的标记走 <c>SessionState</c>（重启 Unity 后重置）。
    /// 需要随仓库共享的项目级设定才用 <c>ScriptableSingleton</c> + <c>[FilePath]</c>——本包暂时不需要。</para>
    /// </summary>
    public static class VnFrameworkEditorPrefs
    {
        /// <summary>欢迎窗口是否在启动时自动显示（EditorPrefs，每人自定，默认 true）。</summary>
        public const string WelcomeAutoShow = "VNF_WelcomeAutoShow";

        /// <summary>欢迎窗口本会话是否已显示过（SessionState 键，重启 Unity 后重置）。</summary>
        public const string WelcomeShownThisSession = "VNF_WelcomeShownThisSession";
    }
}
