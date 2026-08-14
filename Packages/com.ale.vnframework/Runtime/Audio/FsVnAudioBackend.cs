#if VNS_FS_GAMEFRAMEWORK
using UnityEngine;
using Fs.GameFramework.Common.AudioSystem;

namespace Ale.VnFramework
{
    /// <summary>
    /// FsGameFramework 的音频后端实现。由 <c>VNS_FS_GAMEFRAMEWORK</c> 宏门控——
    /// 宏关闭时整个文件不参与编译，本包对 Fs 零依赖。宏在欢迎窗口勾选
    /// （<c>Tools &gt; Ale Toolkit &gt; VN Framework &gt; Welcome</c> → 插件支持）。
    ///
    /// <para>本类同时也是<b>接入自定义音频系统的范例</b>：实现四个方法 + 启动时注册，仅此而已。</para>
    ///
    /// <para>Fs 的 <c>AudioManager</c> 本身按通道 / 按 Key 组织，不区分音频类别，
    /// 故 <c>category</c> 在这里被忽略；换成需要分类路由的后端时可直接用上。</para>
    ///
    /// <para>本类同时实现了可选的 <see cref="IVnAudioPlaybackInfo"/>，因此自动播放能精确等到
    /// 语音播完、快进也能带上语音。它依赖 Fs <c>AudioManager</c> 的 <c>IsPlaying(key)</c> 与
    /// <c>SetPitch(key, pitch)</c>——这两个方法是随本次接入一并加到 Fs 的
    /// （<c>com.fs.gameframework</c> ≥ 0.9.4）。<b>Fs 版本过旧会编译不过</b>，
    /// 报 <c>AudioManager</c> 没有这两个方法，升级 Fs 即可。</para>
    /// </summary>
    public sealed class FsVnAudioBackend : IVnAudioBackend, IVnAudioPlaybackInfo
    {
        /// <summary>
        /// 启动时自动注册。与 Ale Toolkit 的 <c>AddressableAssetLoader</c> 是同一套做法：
        /// 可选能力由独立文件在 <c>BeforeSceneLoad</c> 时替换掉默认实现。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            // 已被宿主工程换成别的后端时不覆盖——显式赋值优先于自动注册。
            if (VnStoryAudio.IsAvailable) return;
            VnStoryAudio.Backend = new FsVnAudioBackend();
        }

        /// <summary>
        /// 取 Fs 的音频管理器，取不到则返回 null。
        ///
        /// <para>场景里没有 <c>AudioManager</c> 是完全可能的（还没搭好、或某个场景不需要音频）。
        /// 而音频接缝的契约是「不报错、不出声、不抛异常」——演出流程不该因为没配音频就断掉。
        /// 裸解引用 <c>Instance</c> 会让宏一开就每行对话 NRE，正好违反这条契约。</para>
        /// </summary>
        private static AudioManager Manager => AudioManager.Instance ? AudioManager.Instance : null;

        // 只传这四个参数，后端的 fadeDuration / replaySameOne / playIntervalMin 保持其默认值。
        public void PlayWithChannel(EVnAudioCategory category, string channelName, string audioKey, float volume, float pitch)
        {
            var manager = Manager;
            if (manager) manager.PlayWithChannel(channelName, audioKey, volume, pitch);
        }

        public void StopWithChannel(EVnAudioCategory category, string channelName)
        {
            var manager = Manager;
            if (manager) manager.StopWithChannel(channelName);
        }

        public void Play(EVnAudioCategory category, string audioKey, float volume, float pitch)
        {
            var manager = Manager;
            if (manager) manager.Play(audioKey, volume, pitch);
        }

        public void Stop(EVnAudioCategory category, string audioKey)
        {
            var manager = Manager;
            if (manager) manager.Stop(audioKey);
        }

        #region 可选能力：播放状态与倍速
        /// <summary>
        /// 该 Key 是否仍在播放。取不到管理器时返回 false——契约是「查不到就当已播完」，
        /// 让演出继续往前走，而不是把自动播放永远卡住。
        /// </summary>
        public bool IsPlaying(EVnAudioCategory category, string audioKey)
        {
            var manager = Manager;
            return manager && manager.IsPlaying(audioKey);
        }

        /// <summary>
        /// 设置该 Key 的播放倍速。Fs 侧经 <c>AudioSource.pitch</c> 实现，故<b>会同时改变音高</b>。
        /// </summary>
        public void SetPlaybackRate(EVnAudioCategory category, string audioKey, float rate)
        {
            var manager = Manager;
            if (manager) manager.SetPitch(audioKey, rate);
        }
        #endregion
    }
}
#endif
