using System;
using System.Collections.Generic;
using Ale.Toolkit.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace Ale.VnFramework
{
    /// <summary>
    /// <see cref="ToolkitTween"/> 的倍率感知包装。演出层的补间与延时**一律走这里**，
    /// 只有菜单 chrome（如 <see cref="VnResponseButton"/> 的选项染色）才直接用 <see cref="ToolkitTween"/>——
    /// 选项菜单弹出时快进已被强制关闭，倍率恒为 1，走哪个都一样，分开是为了让这条边界清晰。
    ///
    /// <para><b>签名与 <see cref="ToolkitTween"/> 逐字一致</b>——参数名、顺序、默认值全同。
    /// 所以改造既有调用点是纯文本替换 <c>ToolkitTween.</c> → <c>VnTween.</c>，不必动实参。
    /// 唯一差别：时长类参数在转发前除以 <see cref="VnPlaybackRate.Playback"/>。</para>
    ///
    /// <para><b>在途补间会被重定时。</b><see cref="ToolkitTween"/> 没有改写在途作业速率的途径
    /// （作业字段是 internal，句柄只有 IsActive / Kill / Complete），所以倍率变化时本类的做法是
    /// <b>杀掉重起</b>：<c>Kill(complete: false)</c> 不触发完成回调，随后按「剩余时长 ÷ 新倍率」
    /// 重新起一条。各通道的起始值都是 <see cref="ToolkitTween"/> 在<b>调用时</b>从目标身上读的，
    /// 所以重起会自动接上当前值，本类不需要逐通道取值。</para>
    ///
    /// <para><b>绝不能用 <c>Complete()</c> 代替 <c>Kill(false)</c>。</b>
    /// <c>Complete()</c> 是<b>同步</b>触发完成回调的，而演出链路上至少两个回调不能在任意时刻触发：
    /// <c>FadeOutUI</c> 的回调里挂着 <c>StopStoryConversation()</c>（一个「进入快进」的动作会把对话直接停掉），
    /// <c>LoadActorPrefab</c> 的延时回调是 <c>InitActorPrefab</c>（角色会提前一整拍出场）。</para>
    /// </summary>
    public static class VnTween
    {
        #region 登记表

        // 通道。只覆盖演出层实际用到的那些。ToolkitTween 的 To(...) 没有可读的「当前值」，
        // 重起时接不上进度，故不提供包装——需要时直接用 ToolkitTween 并接受它不跟随倍率。
        private enum EChannel
        {
            CanvasGroupAlpha,
            GraphicAlpha,
            GraphicColor,
            SpriteRendererAlpha,
            TransformPosition,
            TransformEulerAngles,
            TransformLocalScale,
            Delay,
        }

        // 一条在途作业的重起所需信息。End 用 Vector4 承载各通道的终值：
        // alpha 通道只用 x，颜色通道用四个分量，Transform 通道用 xyz，Delay 通道不用。
        private sealed class Entry
        {
            public ToolkitTweenHandle Handle;
            public EChannel Channel;
            public UnityEngine.Object Target;   // Delay 通道下是 owner，可能为 null
            public Vector4 End;
            public float ScaledDuration;        // 当前这一段的实际时长（已除过倍率）
            public float StartTime;             // 起始时刻，按 Unscaled 取 Time.time / Time.unscaledTime
            public float RateAtStart;           // 起始时的倍率，用于把剩余时长折回「倍率 1 下的秒数」
            public EToolkitEase Ease;
            public bool Unscaled;
            public Action OnComplete;           // 调用方原始回调（不含本类包装）
            public bool Dead;
        }

        private static readonly List<Entry> _entries = new List<Entry>();

        // 关闭 Reload Domain 时静态表会跨播放会话存活，留着上一次运行的已销毁目标。
        // 与 VnStoryAudio.ResetStatics / VnConditionSources.ResetOnPlay 同一约定。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _entries.Clear();

        private static float Rate => VnPlaybackRate.Playback;

        private static float Now(bool unscaled) => unscaled ? Time.unscaledTime : Time.time;

        /// <summary>把调用方给的「倍率 1 下的时长」换算成当前倍率下的实际时长。</summary>
        private static float Scale(float duration) => duration <= 0f ? duration : duration / Rate;

        // 摘掉已经结束（自然完成、被 Kill、被池复用）的条目。
        // 调用方可能绕过本类直接 ToolkitTween.Kill(target) 或 handle.Complete()，
        // 那时本类收不到通知——靠 IsActive 惰性自愈，不需要额外接线。
        private static void Prune()
        {
            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                var e = _entries[i];
                if (!e.Dead && e.Handle.IsActive) continue;
                _entries.RemoveAt(i);
            }
        }

        // 包装调用方回调：作业正常完成时先给条目打死标记，再执行原回调。
        // 不能只靠 Prune 的 IsActive 兜底——原回调里若又起新补间，会先看到一张还没清理的表。
        private static Action Wrap(Entry e) => () =>
        {
            e.Dead = true;
            e.OnComplete?.Invoke();
        };

        #endregion

        #region 倍率变更

        /// <summary>倍率变化时重定时全部在途作业。由 <see cref="VnPlaybackRate.Set"/> 调用。</summary>
        internal static void ApplyRateChange(float oldRate, float newRate)
        {
            if (oldRate <= 0f || newRate <= 0f) return;
            if (Mathf.Approximately(oldRate, newRate)) return;

            Prune();
            if (_entries.Count == 0) return;

            // 快照后再遍历：重起会调 ToolkitTween，时长 ≤ 0 的快路径会**同步**触发完成回调，
            // 而回调里可能又起新补间、往 _entries 里追加——直接遍历原表会撞上集合被修改。
            var snapshot = _entries.ToArray();
            foreach (var e in snapshot)
            {
                if (e.Dead || !e.Handle.IsActive) continue;

                float remainingScaled = e.ScaledDuration - (Now(e.Unscaled) - e.StartTime);
                if (remainingScaled <= 0f) continue;   // 本帧就要结束了，交给它自然完成

                // 折回「倍率 1 下的秒数」再按新倍率缩放。用 RateAtStart 而不是 oldRate：
                // 条目可能是在上一次倍率变更之后才起的，它的基准倍率未必等于本次的 oldRate。
                float newScaled = remainingScaled * e.RateAtStart / newRate;

                // 必须 complete: false —— 见类注释里那两个不能被提前触发的回调。
                e.Handle.Kill(false);
                Restart(e, newScaled);
            }

            Prune();
        }

        // 按剩余时长重起一条既有作业。条目已在 _entries 里，此处只更新它。
        private static void Restart(Entry e, float scaledDuration)
        {
            e.ScaledDuration = scaledDuration;
            e.StartTime = Now(e.Unscaled);
            e.RateAtStart = Rate;

            var cb = Wrap(e);
            switch (e.Channel)
            {
                case EChannel.CanvasGroupAlpha:
                    e.Handle = ToolkitTween.FadeCanvasGroup((CanvasGroup)e.Target, e.End.x, scaledDuration, e.Ease, e.Unscaled, cb);
                    break;
                case EChannel.GraphicAlpha:
                    e.Handle = ToolkitTween.FadeGraphic((Graphic)e.Target, e.End.x, scaledDuration, e.Ease, e.Unscaled, cb);
                    break;
                case EChannel.GraphicColor:
                    e.Handle = ToolkitTween.TintGraphic((Graphic)e.Target, e.End, scaledDuration, e.Ease, e.Unscaled, cb);
                    break;
                case EChannel.SpriteRendererAlpha:
                    e.Handle = ToolkitTween.FadeSpriteRenderer((SpriteRenderer)e.Target, e.End.x, scaledDuration, e.Ease, e.Unscaled, cb);
                    break;
                case EChannel.TransformPosition:
                    e.Handle = ToolkitTween.MoveTransform((Transform)e.Target, e.End, scaledDuration, e.Ease, e.Unscaled, cb);
                    break;
                case EChannel.TransformEulerAngles:
                    // 重起会让 ToolkitTween 重新读一次 eulerAngles 并重算最短弧。单次四元数往返的
                    // 精度损失可以忽略，且「从当前角度走最短弧到目标角度」本就是想要的语义。
                    e.Handle = ToolkitTween.RotateTransform((Transform)e.Target, e.End, scaledDuration, e.Ease, e.Unscaled, cb);
                    break;
                case EChannel.TransformLocalScale:
                    e.Handle = ToolkitTween.ScaleTransform((Transform)e.Target, e.End, scaledDuration, e.Ease, e.Unscaled, cb);
                    break;
                case EChannel.Delay:
                    e.Handle = ToolkitTween.DelayedCall(scaledDuration, cb, e.Unscaled, e.Target);
                    break;
                default:
                    e.Dead = true;
                    return;
            }

            // 重起后立刻失效（目标已销毁 / 时长被压到 0）：打死标记，交给 Prune 摘掉。
            if (!e.Handle.IsActive) e.Dead = true;
        }

        #endregion

        #region 补间（签名与 ToolkitTween 对齐）

        /// <inheritdoc cref="ToolkitTween.FadeCanvasGroup"/>
        public static ToolkitTweenHandle FadeCanvasGroup(
            CanvasGroup target, float endAlpha, float duration,
            EToolkitEase ease = EToolkitEase.OutQuad, bool unscaled = true,
            Action onComplete = null)
        {
            float d = Scale(duration);
            var e = NewEntry(EChannel.CanvasGroupAlpha, target, new Vector4(endAlpha, 0f, 0f, 0f), d, ease, unscaled, onComplete);
            return Commit(e, ToolkitTween.FadeCanvasGroup(target, endAlpha, d, ease, unscaled, Wrap(e)));
        }

        /// <inheritdoc cref="ToolkitTween.FadeGraphic"/>
        public static ToolkitTweenHandle FadeGraphic(
            Graphic target, float endAlpha, float duration,
            EToolkitEase ease = EToolkitEase.OutQuad, bool unscaled = true,
            Action onComplete = null)
        {
            float d = Scale(duration);
            var e = NewEntry(EChannel.GraphicAlpha, target, new Vector4(endAlpha, 0f, 0f, 0f), d, ease, unscaled, onComplete);
            return Commit(e, ToolkitTween.FadeGraphic(target, endAlpha, d, ease, unscaled, Wrap(e)));
        }

        /// <inheritdoc cref="ToolkitTween.TintGraphic"/>
        public static ToolkitTweenHandle TintGraphic(
            Graphic target, Color endColor, float duration,
            EToolkitEase ease = EToolkitEase.OutQuad, bool unscaled = true,
            Action onComplete = null)
        {
            float d = Scale(duration);
            var e = NewEntry(EChannel.GraphicColor, target, endColor, d, ease, unscaled, onComplete);
            return Commit(e, ToolkitTween.TintGraphic(target, endColor, d, ease, unscaled, Wrap(e)));
        }

        /// <inheritdoc cref="ToolkitTween.FadeSpriteRenderer"/>
        public static ToolkitTweenHandle FadeSpriteRenderer(
            SpriteRenderer target, float endAlpha, float duration,
            EToolkitEase ease = EToolkitEase.OutQuad, bool unscaled = true,
            Action onComplete = null)
        {
            float d = Scale(duration);
            var e = NewEntry(EChannel.SpriteRendererAlpha, target, new Vector4(endAlpha, 0f, 0f, 0f), d, ease, unscaled, onComplete);
            return Commit(e, ToolkitTween.FadeSpriteRenderer(target, endAlpha, d, ease, unscaled, Wrap(e)));
        }

        /// <inheritdoc cref="ToolkitTween.MoveTransform"/>
        public static ToolkitTweenHandle MoveTransform(
            Transform target, Vector3 endPosition, float duration,
            EToolkitEase ease = EToolkitEase.OutQuad, bool unscaled = true,
            Action onComplete = null)
        {
            float d = Scale(duration);
            var e = NewEntry(EChannel.TransformPosition, target, endPosition, d, ease, unscaled, onComplete);
            return Commit(e, ToolkitTween.MoveTransform(target, endPosition, d, ease, unscaled, Wrap(e)));
        }

        /// <inheritdoc cref="ToolkitTween.RotateTransform"/>
        public static ToolkitTweenHandle RotateTransform(
            Transform target, Vector3 endEulerAngles, float duration,
            EToolkitEase ease = EToolkitEase.OutQuad, bool unscaled = true,
            Action onComplete = null)
        {
            float d = Scale(duration);
            var e = NewEntry(EChannel.TransformEulerAngles, target, endEulerAngles, d, ease, unscaled, onComplete);
            return Commit(e, ToolkitTween.RotateTransform(target, endEulerAngles, d, ease, unscaled, Wrap(e)));
        }

        /// <inheritdoc cref="ToolkitTween.ScaleTransform"/>
        public static ToolkitTweenHandle ScaleTransform(
            Transform target, Vector3 endScale, float duration,
            EToolkitEase ease = EToolkitEase.OutQuad, bool unscaled = true,
            Action onComplete = null)
        {
            float d = Scale(duration);
            var e = NewEntry(EChannel.TransformLocalScale, target, endScale, d, ease, unscaled, onComplete);
            return Commit(e, ToolkitTween.ScaleTransform(target, endScale, d, ease, unscaled, Wrap(e)));
        }

        /// <inheritdoc cref="ToolkitTween.DelayedCall"/>
        public static ToolkitTweenHandle DelayedCall(
            float delay, Action onComplete,
            bool unscaled = true, UnityEngine.Object owner = null)
        {
            float d = Scale(delay);
            var e = NewEntry(EChannel.Delay, owner, default, d, EToolkitEase.Linear, unscaled, onComplete);
            return Commit(e, ToolkitTween.DelayedCall(d, Wrap(e), unscaled, owner));
        }

        /// <summary>
        /// 打断该目标上全部在途作业，转发给 <see cref="ToolkitTween.Kill"/> 并摘掉本类登记。
        /// 语义与之完全一致（按引用相等匹配，已销毁目标依然能清理自己的作业）。
        /// </summary>
        public static int Kill(UnityEngine.Object target, bool complete = false)
        {
            if (!ReferenceEquals(target, null))
            {
                for (int i = _entries.Count - 1; i >= 0; i--)
                {
                    if (ReferenceEquals(_entries[i].Target, target)) _entries[i].Dead = true;
                }
            }

            int killed = ToolkitTween.Kill(target, complete);
            Prune();
            return killed;
        }

        #endregion

        #region 内部

        // 起作业**之前**先建好条目：ToolkitTween 在时长 ≤ 0 时会同步触发完成回调，
        // 而回调（Wrap）要能立刻在这个条目上打死标记，所以它必须先存在。
        private static Entry NewEntry(
            EChannel channel, UnityEngine.Object target, Vector4 end,
            float scaledDuration, EToolkitEase ease, bool unscaled, Action onComplete)
        {
            Prune();
            return new Entry
            {
                Channel = channel,
                Target = target,
                End = end,
                ScaledDuration = scaledDuration,
                StartTime = Now(unscaled),
                RateAtStart = Rate,
                Ease = ease,
                Unscaled = unscaled,
                OnComplete = onComplete,
            };
        }

        // 句柄有效才入表。无效有三种情形：目标为空、时长 ≤ 0 的快路径（已同步完成）、runner 不可用。
        // 三者都不需要重定时，登记进去只会变成永不清理的僵尸条目。
        private static ToolkitTweenHandle Commit(Entry e, ToolkitTweenHandle handle)
        {
            if (!handle.IsActive)
            {
                e.Dead = true;
                return handle;
            }

            e.Handle = handle;
            _entries.Add(e);
            return handle;
        }

        #endregion
    }
}
