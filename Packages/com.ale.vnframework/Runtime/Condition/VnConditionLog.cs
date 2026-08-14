using System.Collections.Generic;
using UnityEngine;

namespace Ale.VnFramework.Conditions
{
    /// <summary>
    /// 条件桥的去重告警。
    ///
    /// <para>为什么必须去重：条件是在 <b>链接评估</b> 时求值的，同一个节点、同一条件在一次菜单刷新里
    /// 就可能被算好几遍，逐次打日志会瞬间淹没控制台。但又不能不打 —— 条件判 false 的表现是
    /// 「节点莫名其妙进不去」，没有日志根本无从查起。</para>
    /// </summary>
    internal static class VnConditionLog
    {
        /// <summary>日志前缀，与包内其它模块的 <c>[VnStoryManager]</c> 风格一致。</summary>
        internal const string Prefix = "[VnCondition]";

        private static readonly HashSet<string> Warned = new HashSet<string>();

        /// <summary>同一个 <paramref name="key"/> 只告警一次。</summary>
        internal static void WarnOnce(string key, string message)
        {
            if (!Warned.Add(key)) return;
            Debug.LogWarning(Prefix + " " + message);
        }

        /// <summary>
        /// 清空去重记录。每次重新注册（即每次进入播放模式）时调用，
        /// 否则关掉「重新加载域」后上一轮压下的告警在这一轮不会再出现。
        /// </summary>
        internal static void Reset() => Warned.Clear();
    }
}
