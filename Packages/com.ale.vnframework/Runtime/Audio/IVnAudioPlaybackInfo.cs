namespace Ale.VnFramework
{
    /// <summary>
    /// <b>可选</b>的音频后端附加能力：查询播放状态与设置播放倍速。
    /// 后端实现 <see cref="IVnAudioBackend"/> 之外<b>再</b>实现本接口即可，
    /// 不实现不会报错、也不影响任何既有功能——所以给 <see cref="IVnAudioBackend"/> 新增能力时
    /// 走这条路而不是往那四个原语里塞方法，既有实现不必改一行就能继续编译。
    ///
    /// <para><b>谁需要它。</b>两处：</para>
    /// <list type="number">
    ///   <item><b>自动播放</b>要判断「本行语音是否播完」。后端没实现本接口时，
    ///         自动播放退化为「打字机结束 + 停留时长」，并在首次需要时告警一次。</item>
    ///   <item><b>快进</b>要让语音跟着倍速走。没实现时语音按常速播，其余照常倍速。</item>
    /// </list>
    ///
    /// <para><b>为什么按 Key 而不是按类别。</b>类别是 VN 侧的概念，多数音频系统
    /// （包括 Fs 的 <c>AudioManager</c>）内部只按 Key 组织，给不出「把所有语音都调速」这种操作。
    /// 而演出层本来就知道当前这行播了哪些 Key，逐个调用即可。<c>category</c> 仍然传，
    /// 供需要分类路由的后端使用。</para>
    /// </summary>
    public interface IVnAudioPlaybackInfo
    {
        /// <summary>
        /// 该 Key 当前是否仍在播放。
        /// <para>对没在播、不存在、已被停止的 Key 应当安静地返回 false，不要报错。</para>
        /// </summary>
        bool IsPlaying(EVnAudioCategory category, string audioKey);

        /// <summary>
        /// 设置该 Key 的播放倍速。
        /// <para>通常经 <c>AudioSource.pitch</c> 实现，因此<b>会同时改变音高</b>——
        /// 这是 Unity 音频的固有行为，不做变速不变调处理。</para>
        /// <para>该 Key 没在播放时应当安静地忽略。</para>
        /// </summary>
        /// <param name="rate">倍速。1 为常速。</param>
        void SetPlaybackRate(EVnAudioCategory category, string audioKey, float rate);
    }
}
