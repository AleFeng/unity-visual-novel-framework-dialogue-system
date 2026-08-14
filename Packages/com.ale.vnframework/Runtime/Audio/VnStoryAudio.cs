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
    }
}
