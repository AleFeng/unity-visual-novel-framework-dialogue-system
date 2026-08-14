using UnityEngine;

namespace Ale.VnFramework
{
    /// <summary>
    /// 默认后端：全部为空操作。未接入任何音频系统时由 <see cref="VnStoryAudio"/> 使用，
    /// 保证演出流程照常推进——不报错、不出声、不抛异常。
    ///
    /// <para>仅在编辑器内提示一次，避免逐条对话刷日志；打包后连这条提示也会被编译掉。</para>
    /// </summary>
    public sealed class NullVnAudioBackend : IVnAudioBackend
    {
        /// <summary>共享实例。无状态，可安全复用。</summary>
        public static readonly NullVnAudioBackend Instance = new NullVnAudioBackend();

        private NullVnAudioBackend() { }

        public void PlayWithChannel(EVnAudioCategory category, string channelName, string audioKey, float volume, float pitch) => WarnOnce();
        public void StopWithChannel(EVnAudioCategory category, string channelName) => WarnOnce();
        public void Play(EVnAudioCategory category, string audioKey, float volume, float pitch) => WarnOnce();
        public void Stop(EVnAudioCategory category, string audioKey) => WarnOnce();

        // 一次会话只提示一次：没有后端时演出照样会逐条对话调用这些接口，逐次刷日志没有意义。
        private static bool _warned;

        // 仅编辑器内提示；打包后的构建不需要这条噪音。
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private static void WarnOnce()
        {
            if (_warned) return;
            _warned = true;
            Debug.LogWarning(
                "[VnStoryAudio] 未接入音频后端，剧情演出的 BGM 与音效都不会播放。\n" +
                "接入方式：实现 IVnAudioBackend 并赋值给 VnStoryAudio.Backend。\n" +
                "若使用 FsGameFramework 的 AudioManager，在欢迎窗口勾选 Fs Game Framework 即可" +
                "（Tools > Ale Toolkit > VN Framework > Welcome）。");
        }
    }
}
