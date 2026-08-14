using UnityEditor;

namespace Ale.VnFramework.Editor
{
    /// <summary>
    /// 域加载后的一次性检查：按需自动弹出欢迎窗口。
    ///
    /// <para><b>只提示，绝不改写 PlayerSettings。</b>宏的增删一律由用户在欢迎窗口显式操作——
    /// 加载时自动改写会与别的插件对同名宏的管理逻辑互相覆盖，而且每次写入触发一次重编译，
    /// 编辑器会陷入「Compiling Scripts」死循环。</para>
    /// </summary>
    [InitializeOnLoad]
    public static class VnFrameworkDefineChecker
    {
        static VnFrameworkDefineChecker()
        {
            // 延迟到编辑器完全就绪后执行，避免在域初始化期间操作 UI。
            EditorApplication.delayCall += OnDelayedInit;
        }

        private static void OnDelayedInit()
        {
            EditorApplication.delayCall -= OnDelayedInit;
            CheckWelcomeWindow();
        }

        /// <summary>判断是否需要自动弹出欢迎窗口并弹出。</summary>
        private static void CheckWelcomeWindow()
        {
            // 本会话已经显示过则跳过（SessionState 在重启 Unity 后重置）。
            if (SessionState.GetBool(VnFrameworkEditorPrefs.WelcomeShownThisSession, false))
                return;

            // 先置位再判断偏好：否则同一会话内的域重载会重复弹窗。
            SessionState.SetBool(VnFrameworkEditorPrefs.WelcomeShownThisSession, true);

            if (!EditorPrefs.GetBool(VnFrameworkEditorPrefs.WelcomeAutoShow, true))
                return;

            VnFrameworkWelcomeWindow.Open();
        }
    }
}
