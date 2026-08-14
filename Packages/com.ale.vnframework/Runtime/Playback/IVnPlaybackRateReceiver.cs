namespace Ale.VnFramework
{
    /// <summary>
    /// 可选契约：挂在角色 / 特效预制体上的组件实现本接口后，会在演出倍率变化时收到通知，
    /// 自行决定怎么缩放自己的后端。<b>不实现只是不响应快进，不会报错</b>——
    /// 与 <see cref="IVnAudioBackend"/> 是同一套「可选能力」路数。
    ///
    /// <para>本包的 <see cref="VnActorAnimator"/> 已经实现了它（Unity <c>Animator.speed</c> 与
    /// 粒子 <c>simulationSpeed</c>）。没有挂 <see cref="VnActorAnimator"/> 的纯图片 / 纯粒子预制体
    /// 由 <see cref="VnStoryManager"/> 直接兜底处理，判据与
    /// <c>FadeInActorsAndEffects</c> 的降级分支一致。</para>
    ///
    /// <para><b>已知缺口</b>：Spine / Live2D 的<b>状态动画</b>吃不到倍率。
    /// <c>AnimatorBase</c> 没有实时时间缩放 API——速度在起播时就被烘进了
    /// <c>TrackEntry.TimeScale</c>，之后改不动。补齐的正确位置在
    /// <c>com.ale.animsimulatorsystem</c>（另一个仓库）加一个时间缩放属性，不在本包范围内。</para>
    /// </summary>
    public interface IVnPlaybackRateReceiver
    {
        /// <summary>
        /// 演出倍率变化时调用。
        /// </summary>
        /// <param name="rate">新倍率。1 表示常速，快进时为快进倍率（默认 5）。</param>
        void OnVnPlaybackRateChanged(float rate);
    }
}
