using System;
using UnityEngine;

namespace Ale.VnFramework
{
    /// <summary>
    /// 演出倍率的唯一权威。由 <see cref="VnStoryManager"/> 独占写入，其余各处只读。
    ///
    /// <para><b>为什么是两条倍率而不是一条。</b>「播放速度」档位按需求只管台词打字机
    /// （见 README 的播放控制章节），而「快进」要管所有演出内容。混成一个数会导致
    /// 3 档速 + 5 倍快进得到 15 倍，或者反过来让快进把用户选的档位吞掉。所以分开：</para>
    /// <list type="bullet">
    ///   <item><see cref="Typewriter"/> —— 打字机字速与标点停顿时长。</item>
    ///   <item><see cref="Playback"/> —— 补间、延时、角色动画、粒子、语音。</item>
    /// </list>
    ///
    /// <para><b>快进与档位用 Max 合成而不是相乘</b>（合成在 <see cref="VnStoryManager"/> 侧做）：
    /// 相乘会让 3 档速用户拿到 15 倍；纯覆盖又会在「快进倍率 2、档位 3」时反而变慢——
    /// 按下快进结果更慢是明显的 bug。Max 保证单调不减。</para>
    /// </summary>
    public static class VnPlaybackRate
    {
        /// <summary>倍率下限。0 会让补间时长变成除零，负数会让插值倒放。</summary>
        public const float MinRate = 0.1f;

        /// <summary>倍率上限。挡住配置写错（比如把 500 当成百分比填进去）。</summary>
        public const float MaxRate = 100f;

        /// <summary>演出倍率：补间、延时、角色动画、粒子、语音。默认 1。</summary>
        public static float Playback { get; private set; } = 1f;

        /// <summary>打字机倍率：字速与标点停顿。默认 1。</summary>
        public static float Typewriter { get; private set; } = 1f;

        /// <summary>
        /// <see cref="Playback"/> 变化后触发，参数为新倍率。供角色 / 粒子等自行跟随。
        /// <para>只在 <see cref="Playback"/> 真的变了时触发；只改 <see cref="Typewriter"/> 不触发。</para>
        /// </summary>
        public static event Action<float> PlaybackChanged;

        /// <summary>
        /// 设置倍率。<b>只应由 <see cref="VnStoryManager"/> 调用</b>，故为 internal。
        /// 两个值都会被钳到 [<see cref="MinRate"/>, <see cref="MaxRate"/>]。
        /// </summary>
        internal static void Set(float playback, float typewriter)
        {
            playback = Mathf.Clamp(playback, MinRate, MaxRate);
            typewriter = Mathf.Clamp(typewriter, MinRate, MaxRate);

            Typewriter = typewriter;

            if (Mathf.Approximately(Playback, playback)) return;

            var previous = Playback;
            Playback = playback;

            // 先重定时在途补间，再通知外部。顺序是有意的：外部回调里若又起了新补间，
            // 那条新补间应当已经按新倍率计时，而不是被紧接着的重定时再动一次。
            VnTween.ApplyRateChange(previous, playback);

            try
            {
                PlaybackChanged?.Invoke(playback);
            }
            catch (Exception e)
            {
                // 单个订阅者抛异常不应该让倍率切换半途而废（后面的订阅者收不到通知，
                // 演出会停在「一半快进一半没快进」的状态）。吞掉并报出来。
                Debug.LogError("[VnPlaybackRate] 倍率变更回调抛出异常：" + e);
            }
        }

        // 本工程关闭了 Reload Domain，静态字段会跨播放会话存活。
        // 若上一次退出播放时正处于快进（倍率 5），下一次进入播放时所有补间会以 5 倍速跑完，
        // 而界面上没有任何「快进中」的提示——与 VnStoryAudio.ResetStatics 防的是同一类问题。
        // 事件也必须清：订阅者是上一次运行的对象，已经不存在了。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Playback = 1f;
            Typewriter = 1f;
            PlaybackChanged = null;
        }
    }
}
