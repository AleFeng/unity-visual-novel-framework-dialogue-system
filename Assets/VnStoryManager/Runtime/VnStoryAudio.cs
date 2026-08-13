using UnityEngine;

#if VNS_FS_GAMEFRAMEWORK
using Fs.GameFramework.Common.AudioSystem;
#endif

namespace Ale.VnFramework
{
    /// <summary>
    /// VN 剧情演出的音频接缝。
    ///
    /// <para>演出侧只需要四件事：按通道播放 / 停止（承载 BGM），按 Key 播放 / 停止（承载环境音、音效、语音）。
    /// 具体由哪个音频系统实现与演出逻辑无关，因此本类把这层依赖收敛到<b>单个文件</b>，
    /// 由 <c>VNS_FS_GAMEFRAMEWORK</c> 宏开关：开启时接 FsGameFramework 的 <c>AudioManager</c>；
    /// 关闭时全部为空操作——不报错、不出声，演出流程照常推进。</para>
    ///
    /// <para>宏由后续的 VnStory 欢迎窗口统一开关。<b>不要在模块的其他地方直接引用音频后端的类型</b>，
    /// 否则这个接缝就失去意义了。</para>
    /// </summary>
    internal static class VnStoryAudio
    {
        /// <summary>是否已接入音频后端。为 false 时下列方法全部是空操作。</summary>
        public static bool IsAvailable
        {
#if VNS_FS_GAMEFRAMEWORK
            get => true;
#else
            get => false;
#endif
        }

        /// <summary>
        /// 按通道播放。通道用于承载 BGM——同一通道再次播放会交叉淡入淡出到新曲目。
        /// </summary>
        /// <param name="channelName">通道名。VN 侧以音频字段标题充当，一个字段一条通道。</param>
        /// <param name="audioKey">音频 Key。</param>
        /// <param name="volume">音量。</param>
        /// <param name="pitch">音调。同时影响播放速度与音高。</param>
        public static void PlayWithChannel(string channelName, string audioKey, float volume, float pitch)
        {
#if VNS_FS_GAMEFRAMEWORK
            // 只传这四个参数，后端的 fadeDuration / replaySameOne / playIntervalMin 保持其默认值
            AudioManager.Instance.PlayWithChannel(channelName, audioKey, volume, pitch);
#else
            WarnOnce();
#endif
        }

        /// <summary>停止指定通道的播放。</summary>
        /// <param name="channelName">通道名。</param>
        public static void StopWithChannel(string channelName)
        {
#if VNS_FS_GAMEFRAMEWORK
            AudioManager.Instance.StopWithChannel(channelName);
#else
            WarnOnce();
#endif
        }

        /// <summary>按 Key 播放音频（环境音 / 音效 / 语音）。</summary>
        /// <param name="audioKey">音频 Key。</param>
        /// <param name="volume">音量。</param>
        /// <param name="pitch">音调。同时影响播放速度与音高。</param>
        public static void Play(string audioKey, float volume, float pitch)
        {
#if VNS_FS_GAMEFRAMEWORK
            AudioManager.Instance.Play(audioKey, volume, pitch);
#else
            WarnOnce();
#endif
        }

        /// <summary>按 Key 停止音频。</summary>
        /// <param name="audioKey">音频 Key。</param>
        public static void Stop(string audioKey)
        {
#if VNS_FS_GAMEFRAMEWORK
            AudioManager.Instance.Stop(audioKey);
#else
            WarnOnce();
#endif
        }

#if !VNS_FS_GAMEFRAMEWORK
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
                "如需接入 FsGameFramework 的 AudioManager，请定义 VNS_FS_GAMEFRAMEWORK 宏。");
        }
#endif
    }
}
