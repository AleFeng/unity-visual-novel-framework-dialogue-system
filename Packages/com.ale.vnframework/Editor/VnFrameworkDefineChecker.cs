using UnityEditor;
using UnityEngine;
using static Ale.Toolkit.Editor.ToolkitEditorL10n;

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
            CheckRuntimeConsistency();
            CheckWelcomeWindow();
        }

        /// <summary>
        /// 检查「宏开着，但对应的运行时不在」这种不一致，并给出可读告警。
        ///
        /// <para>不修正、只提示——理由同本类的类注释。没有这条的话，使用者拿到的是
        /// <c>FsVnAudioBackend.cs</c> 里一堆 <c>CS0246: 找不到类型 AudioManager</c>，
        /// 完全看不出「是我自己勾了个宏」。</para>
        /// </summary>
        private static void CheckRuntimeConsistency()
        {
            if (!VnFrameworkDefines.IsFsGameFrameworkEnabled()) return;
            if (VnFrameworkDefines.IsFsGameFrameworkInstalled()) return;

            Debug.LogWarning(Fmt(
                "[VN Framework] 已启用宏 {0}，但工程中找不到 Fs GameFramework 的音频系统（{1}）。\n" +
                "FsVnAudioBackend 将无法编译。请安装 {2}，或在欢迎窗口取消勾选 Fs Game Framework" +
                "（Tools > Ale Toolkit > VN Framework > Welcome）。",
                VnFrameworkDefines.FsGameFramework,
                VnFrameworkDefines.NsFsAudio,
                VnFrameworkDefines.PackageFs));
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
