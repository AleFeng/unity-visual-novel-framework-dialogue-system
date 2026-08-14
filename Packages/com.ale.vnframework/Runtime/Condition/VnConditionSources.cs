using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ale.VnFramework.Conditions
{
    /// <summary>
    /// 条件判定器的**宿主数据源**注册表：把「数值 / 标记」的取值权交给宿主系统（时间、好感度、任务…）。
    ///
    /// <para>本框架的 Toolkit 条件<b>不读 Dialogue System 的 Variable</b>，而是在求值当刻回调宿主。
    /// 这样绕开了「变量只在每段对话开始时推一次」（见 <c>VnStoryManager.StartStoryConversation</c>
    /// 里的 <c>SetAllVariablesToDialogueSystem()</c>）导致的对话中途取到旧值的问题。
    /// 既有的 <c>Variable["…"] == …</c> 写法完全不受影响，两者可以在同一个 Conditions 表达式里
    /// 用 <c>and</c> / <c>or</c> 组合。</para>
    ///
    /// <para>典型接线（在宿主组件的 <c>OnEnable</c> / <c>OnDisable</c> 里成对调用）：</para>
    /// <code>
    /// VnConditionSources.RegisterNumber("时间段", () =&gt; timeSystem.CurrentHour);
    /// VnConditionSources.RegisterFlag("已见过面", () =&gt; flags.Has("met"));
    /// </code>
    /// </summary>
    public static class VnConditionSources
    {
        private static readonly Dictionary<string, Func<double>> NumberGetters = new Dictionary<string, Func<double>>();
        private static readonly Dictionary<string, Func<bool>> FlagGetters = new Dictionary<string, Func<bool>>();
        private static readonly Dictionary<Type, object> CustomServices = new Dictionary<Type, object>();

        /// <summary>
        /// 判定主体，透传给 <c>IConditionContext.Subject</c>。默认 <c>null</c>；
        /// 只有当自定义判定器确实需要「被判定的对象」时才需要设置。
        /// </summary>
        public static object Subject { get; set; }

        /// <summary>
        /// 每次进入播放模式时清空。
        ///
        /// <para>关掉「重新加载域」（Enter Play Mode Options）后静态表会跨播放存活，
        /// 里头的 getter 闭包却捕获着上一轮已销毁的 MonoBehaviour —— 不清就会在下一轮求值时抛异常。
        /// 用 <c>SubsystemRegistration</c> 是因为它早于一切 <c>Awake</c>，
        /// 不会误删宿主本轮刚注册的项。</para>
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => Clear();

        // ── 数值 ────────────────────────────────────────────────────────────────────

        /// <summary>注册一个数值来源。<paramref name="getter"/> 在每次条件求值时被调用，取到的永远是当前值。</summary>
        public static void RegisterNumber(string id, Func<double> getter)
        {
            if (string.IsNullOrEmpty(id))
            {
                Debug.LogWarning($"{VnConditionLog.Prefix} RegisterNumber >> id 不能为空。");
                return;
            }
            if (getter == null)
            {
                Debug.LogWarning($"{VnConditionLog.Prefix} RegisterNumber >> 注册「{id}」时 getter 不能为 null。");
                return;
            }
            if (NumberGetters.ContainsKey(id))
            {
                Debug.LogWarning($"{VnConditionLog.Prefix} RegisterNumber >> 数值「{id}」已经注册过了。将覆盖之前的注册。");
            }
            NumberGetters[id] = getter;
        }

        /// <summary>注销数值来源。返回是否确有其项。</summary>
        public static bool UnregisterNumber(string id)
            => !string.IsNullOrEmpty(id) && NumberGetters.Remove(id);

        /// <summary>是否已注册该数值来源。</summary>
        public static bool HasNumber(string id)
            => !string.IsNullOrEmpty(id) && NumberGetters.ContainsKey(id);

        // ── 标记 ────────────────────────────────────────────────────────────────────

        /// <summary>注册一个标记来源。</summary>
        public static void RegisterFlag(string id, Func<bool> getter)
        {
            if (string.IsNullOrEmpty(id))
            {
                Debug.LogWarning($"{VnConditionLog.Prefix} RegisterFlag >> id 不能为空。");
                return;
            }
            if (getter == null)
            {
                Debug.LogWarning($"{VnConditionLog.Prefix} RegisterFlag >> 注册「{id}」时 getter 不能为 null。");
                return;
            }
            if (FlagGetters.ContainsKey(id))
            {
                Debug.LogWarning($"{VnConditionLog.Prefix} RegisterFlag >> 标记「{id}」已经注册过了。将覆盖之前的注册。");
            }
            FlagGetters[id] = getter;
        }

        /// <summary>注销标记来源。返回是否确有其项。</summary>
        public static bool UnregisterFlag(string id)
            => !string.IsNullOrEmpty(id) && FlagGetters.Remove(id);

        /// <summary>是否已注册该标记来源。</summary>
        public static bool HasFlag(string id)
            => !string.IsNullOrEmpty(id) && FlagGetters.ContainsKey(id);

        // ── 自定义服务 ──────────────────────────────────────────────────────────────

        /// <summary>
        /// 注册一个领域服务，供第三方判定器经 <c>ctx.GetService&lt;T&gt;()</c> 取用。
        ///
        /// <para>也可以用它<b>顶掉</b>内置的 <c>IConditionNumberSource</c> / <c>IConditionFlagSource</c>：
        /// 自定义服务优先级高于上面两张 getter 表。</para>
        /// </summary>
        public static void RegisterService<T>(T service) where T : class
        {
            if (service == null)
            {
                Debug.LogWarning($"{VnConditionLog.Prefix} RegisterService >> 「{typeof(T).Name}」的实例不能为 null。");
                return;
            }
            CustomServices[typeof(T)] = service;
        }

        /// <summary>注销领域服务。返回是否确有其项。</summary>
        public static bool UnregisterService<T>() where T : class => CustomServices.Remove(typeof(T));

        /// <summary>清空所有数值 / 标记 / 服务注册与 <see cref="Subject"/>。</summary>
        public static void Clear()
        {
            NumberGetters.Clear();
            FlagGetters.Clear();
            CustomServices.Clear();
            Subject = null;
        }

        // ── 供 VnConditionContext 取值 ──────────────────────────────────────────────

        internal static bool TryGetNumberGetter(string id, out Func<double> getter)
        {
            if (!string.IsNullOrEmpty(id)) return NumberGetters.TryGetValue(id, out getter);
            getter = null;
            return false;
        }

        internal static bool TryGetFlagGetter(string id, out Func<bool> getter)
        {
            if (!string.IsNullOrEmpty(id)) return FlagGetters.TryGetValue(id, out getter);
            getter = null;
            return false;
        }

        internal static object GetService(Type serviceType)
        {
            object service;
            return CustomServices.TryGetValue(serviceType, out service) ? service : null;
        }
    }
}
