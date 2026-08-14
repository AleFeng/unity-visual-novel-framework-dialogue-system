using System;
using Ale.Condition;
using UnityEngine;

namespace Ale.VnFramework.Conditions
{
    /// <summary>
    /// 传给判定器的求值上下文。自身即内置的数值 / 标记数据源，转发到
    /// <see cref="VnConditionSources"/> 上宿主注册的 getter。
    ///
    /// <para>无状态、可复用，故只有一个共享实例：判定器约定是无状态单例，上下文也没有理由逐次新建。</para>
    /// </summary>
    internal sealed class VnConditionContext : IConditionContext, IConditionNumberSource, IConditionFlagSource
    {
        internal static readonly VnConditionContext Instance = new VnConditionContext();

        private VnConditionContext() { }

        public object Subject => VnConditionSources.Subject;

        /// <summary>
        /// 取服务。宿主经 <c>VnConditionSources.RegisterService&lt;T&gt;()</c> 注册的**优先**，
        /// 其次才回落到本类自带的数值 / 标记源 —— 这样宿主想整套换掉内置实现也做得到。
        /// </summary>
        public T GetService<T>() where T : class
        {
            var custom = VnConditionSources.GetService(typeof(T));
            if (custom != null) return custom as T;

            // T 若不是 IConditionNumberSource / IConditionFlagSource，这里自然得到 null。
            return this as T;
        }

        /// <summary>
        /// 取数值。未注册的 id 返回 <see cref="double.NaN"/>。
        ///
        /// <para>⚠️ 返回 NaN 而不是 0 是**刻意**的：<c>NumberCompareEvaluator</c> 的五个分支
        /// （&gt; / ≥ / == / ≤ / &lt;）对 NaN 全部为 false（含 <c>Math.Abs(NaN - x) &lt; 1e-9</c> 也是 false），
        /// 于是「忘了接线」表现为条件不成立。若返回 0，像 <c>时间段 ≤ 5</c> 这样的条件会**静默通过**，
        /// 剧情照跑但判定其实没生效 —— 那是最难查的一类 bug。</para>
        /// </summary>
        public double GetNumber(string id)
        {
            Func<double> getter;
            if (!VnConditionSources.TryGetNumberGetter(id, out getter))
            {
                VnConditionLog.WarnOnce("number:" + id,
                    $"数值「{id}」没有注册数据源，用到它的条件一律按不成立处理。" +
                    $"请在宿主里调用 VnConditionSources.RegisterNumber(\"{id}\", () => ...)。");
                return double.NaN;
            }

            try
            {
                return getter();
            }
            catch (Exception e)
            {
                VnConditionLog.WarnOnce("number-throw:" + id,
                    $"数值「{id}」的 getter 抛异常，按不成立处理。详见下一条异常日志。");
                Debug.LogException(e);
                return double.NaN;
            }
        }

        /// <summary>取标记。未注册的 id 返回 <c>false</c>（本就是失败即不成立，无需 NaN 那套）。</summary>
        public bool HasFlag(string flag)
        {
            Func<bool> getter;
            if (!VnConditionSources.TryGetFlagGetter(flag, out getter))
            {
                VnConditionLog.WarnOnce("flag:" + flag,
                    $"标记「{flag}」没有注册数据源，用到它的条件一律按不成立处理。" +
                    $"请在宿主里调用 VnConditionSources.RegisterFlag(\"{flag}\", () => ...)。");
                return false;
            }

            try
            {
                return getter();
            }
            catch (Exception e)
            {
                VnConditionLog.WarnOnce("flag-throw:" + flag,
                    $"标记「{flag}」的 getter 抛异常，按不成立处理。详见下一条异常日志。");
                Debug.LogException(e);
                return false;
            }
        }
    }
}
