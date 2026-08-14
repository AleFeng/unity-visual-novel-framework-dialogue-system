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
    /// </summary>
    public sealed class FsVnAudioBackend : IVnAudioBackend
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
    }
}
#endif
