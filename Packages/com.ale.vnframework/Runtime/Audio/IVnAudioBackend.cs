namespace Ale.VnFramework
{
    /// <summary>
    /// 音频类别。用于让后端按类别路由（不同 Mixer 组、独立音量控制、语音打断策略等）。
    /// </summary>
    public enum EVnAudioCategory
    {
        /// <summary>背景音乐。按通道播放，同通道再次播放意味着换曲。</summary>
        Bgm = 0,
        /// <summary>环境音。</summary>
        Ambient = 1,
        /// <summary>音效。</summary>
        Sfx = 2,
        /// <summary>语音。</summary>
        Voice = 3,
    }

    /// <summary>
    /// VN 剧情演出的音频后端契约。**接入自己的音频系统只需实现本接口，然后赋值给
    /// <see cref="VnStoryAudio.Backend"/>**，不需要定义任何编译宏、也不需要改动本包源码。
    ///
    /// <para><b>只需实现这四个原语。</b>演出语义（解析 <c>Key|音量|音调|延迟</c>、延迟播放、
    /// 切换对话行时停掉上一行的音效、BGM 值为空即停该通道、跨行去重）全部由
    /// <c>VnStoryManager</c> 负责，后端不必也不应重复实现。</para>
    ///
    /// <para>调用时机全在主线程的对话行推进过程中；实现方无需考虑线程安全。
    /// 传入的 <c>audioKey</c> 直接来自剧本节点上配置的字符串，如何解析成实际资源由后端自行决定。</para>
    ///
    /// <example>
    /// <code>
    /// public sealed class MyAudioBackend : IVnAudioBackend { /* 四个方法 */ }
    ///
    /// [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    /// private static void Install() =&gt; VnStoryAudio.Backend = new MyAudioBackend();
    /// </code>
    /// </example>
    /// </summary>
    public interface IVnAudioBackend
    {
        /// <summary>
        /// 按通道播放。通道用于承载 BGM——<b>同一通道再次播放应当交叉淡入淡出到新曲目</b>，而不是叠加。
        /// </summary>
        /// <param name="category">音频类别。当前仅 <see cref="EVnAudioCategory.Bgm"/> 会走这条路径。</param>
        /// <param name="channelName">通道名。VN 侧以音频字段标题充当，一个字段一条通道。</param>
        /// <param name="audioKey">音频 Key，取自剧本节点的配置字符串。</param>
        /// <param name="volume">音量。</param>
        /// <param name="pitch">音调。通常同时影响播放速度与音高。</param>
        void PlayWithChannel(EVnAudioCategory category, string channelName, string audioKey, float volume, float pitch);

        /// <summary>停止指定通道的播放。</summary>
        void StopWithChannel(EVnAudioCategory category, string channelName);

        /// <summary>按 Key 播放（环境音 / 音效 / 语音）。可叠加，不做通道互斥。</summary>
        void Play(EVnAudioCategory category, string audioKey, float volume, float pitch);

        /// <summary>按 Key 停止。对未在播放的 Key 应当安静地忽略，不要报错。</summary>
        void Stop(EVnAudioCategory category, string audioKey);
    }
}
