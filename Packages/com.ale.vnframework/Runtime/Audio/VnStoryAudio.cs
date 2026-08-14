using UnityEngine;

namespace Ale.VnFramework
{
    /// <summary>
    /// VN 剧情演出的音频门面。演出侧只需要四件事：按通道播放 / 停止（承载 BGM），
    /// 按 Key 播放 / 停止（承载环境音、音效、语音）。
    ///
    /// <para>具体由哪个音频系统实现与演出逻辑无关，因此本类把这层依赖收敛到<b>单个可替换的后端</b>
    /// （<see cref="IVnAudioBackend"/>）。默认是 <see cref="NullVnAudioBackend"/> 全空操作——
    /// 不报错、不出声，演出流程照常推进。</para>
    ///
    /// <para><b>接入自己的音频系统</b>：实现 <see cref="IVnAudioBackend"/> 后赋值给
    /// <see cref="Backend"/> 即可，不需要定义任何编译宏、也不需要改动本包源码。
    /// 内置的 FsGameFramework 支持（<c>FsVnAudioBackend</c>）就是这么接的，由
    /// <c>VNS_FS_GAMEFRAMEWORK</c> 宏门控并在启动时自动注册。</para>
    ///
    /// <para><b>不要在模块的其他地方直接引用音频后端的类型</b>，否则这个接缝就失去意义了。</para>
    /// </summary>
    public static class VnStoryAudio
    {
        private static IVnAudioBackend _backend;

        /// <summary>
        /// 每次进入播放前把后端清空。
        ///
        /// <para>关闭 Enter Play Mode Options 的「Reload Domain」后，静态字段不会随退出播放而重置：
        /// 上一次会话赋的后端会活到下一次，而它若由 MonoBehaviour 支撑，此时已指向被销毁的对象；
        /// 更麻烦的是 <c>FsVnAudioBackend.Install</c> 靠 <see cref="IsAvailable"/> 判断「是否已有人接管」，
        /// 残留的后端会让自动注册**永久跳过**。</para>
        ///
        /// <para><c>SubsystemRegistration</c> 早于所有 <c>BeforeSceneLoad</c> 的注册方法，
        /// 因此清空一定发生在各后端自动注册之前，不会把它们刚装好的实例抹掉。
        /// 与所依赖的 <c>com.ale.toolkit</c> 里 <c>ToolkitSingletonRegistry</c> 是同一套做法。</para>
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _backend = null;
            // 告警去重标记同样要清：不清的话，上一次会话已经提醒过，这一次就再也不提醒了，
            // 而后端可能刚好在这一次换成了不支持查询的实现。
            _warnedNoPlaybackInfo = false;
        }

        /// <summary>
        /// 当前音频后端。未赋值时返回 <see cref="NullVnAudioBackend"/>，故本属性<b>永不为 null</b>，
        /// 调用方无需判空。赋 <c>null</c> 表示卸载后端、回到空操作。
        /// </summary>
        public static IVnAudioBackend Backend
        {
            get => _backend ?? NullVnAudioBackend.Instance;
            set => _backend = value;
        }

        /// <summary>是否已接入真实的音频后端。为 false 时下列方法全部是空操作。</summary>
        public static bool IsAvailable => _backend != null;

        /// <summary>
        /// 按通道播放。通道用于承载 BGM——同一通道再次播放会交叉淡入淡出到新曲目。
        /// </summary>
        /// <param name="category">音频类别。</param>
        /// <param name="channelName">通道名。VN 侧以音频字段标题充当，一个字段一条通道。</param>
        /// <param name="audioKey">音频 Key。</param>
        /// <param name="volume">音量。</param>
        /// <param name="pitch">音调。同时影响播放速度与音高。</param>
        public static void PlayWithChannel(EVnAudioCategory category, string channelName, string audioKey, float volume, float pitch)
            => Backend.PlayWithChannel(category, channelName, audioKey, volume, pitch);

        /// <summary>停止指定通道的播放。</summary>
        public static void StopWithChannel(EVnAudioCategory category, string channelName)
            => Backend.StopWithChannel(category, channelName);

        /// <summary>按 Key 播放音频（环境音 / 音效 / 语音）。</summary>
        public static void Play(EVnAudioCategory category, string audioKey, float volume, float pitch)
            => Backend.Play(category, audioKey, volume, pitch);

        /// <summary>按 Key 停止音频。</summary>
        public static void Stop(EVnAudioCategory category, string audioKey)
            => Backend.Stop(category, audioKey);

        #region 可选能力：播放状态与倍速
        // 「后端不支持播放查询」只值得说一次。每行对话都提醒会把控制台淹掉。
        private static bool _warnedNoPlaybackInfo;

        /// <summary>
        /// 当前后端是否额外实现了 <see cref="IVnAudioPlaybackInfo"/>。
        /// 为 false 时 <see cref="IsPlaying"/> 恒返回 false、<see cref="SetPlaybackRate"/> 是空操作。
        /// </summary>
        public static bool SupportsPlaybackInfo => Backend is IVnAudioPlaybackInfo;

        /// <summary>
        /// 该 Key 是否仍在播放。后端未实现 <see cref="IVnAudioPlaybackInfo"/> 时返回 <c>false</c> 并告警一次。
        ///
        /// <para><b>返回 false 的含义对调用方是「不用再等了」</b>——自动播放据此退化为
        /// 「打字机结束 + 停留时长」。这个方向是有意选的：查不到就当已经播完，
        /// 演出继续往前走；反过来（查不到当作还在播）会让自动播放永远卡住。</para>
        /// </summary>
        public static bool IsPlaying(EVnAudioCategory category, string audioKey)
        {
            if (string.IsNullOrEmpty(audioKey)) return false;

            if (Backend is IVnAudioPlaybackInfo info) return info.IsPlaying(category, audioKey);

            if (!_warnedNoPlaybackInfo)
            {
                _warnedNoPlaybackInfo = true;
                Debug.LogWarning("[VnStoryAudio] 当前音频后端没有实现 IVnAudioPlaybackInfo，" +
                                 "无法判断语音是否播完。自动播放将退化为「打字机结束 + 停留时长」，" +
                                 "快进时语音也不会跟着倍速。本提示只出现一次。");
            }
            return false;
        }

        /// <summary>
        /// 设置该 Key 的播放倍速（通常经 pitch，会同时改变音高）。后端未实现时为空操作，不告警——
        /// <see cref="IsPlaying"/> 已经提醒过一次，快进时每行再刷一遍没有意义。
        /// </summary>
        public static void SetPlaybackRate(EVnAudioCategory category, string audioKey, float rate)
        {
            if (string.IsNullOrEmpty(audioKey)) return;
            if (Backend is IVnAudioPlaybackInfo info) info.SetPlaybackRate(category, audioKey, rate);
        }
        #endregion
    }
}
