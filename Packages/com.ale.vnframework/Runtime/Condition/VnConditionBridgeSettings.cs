using System.Collections.Generic;
using UnityEngine;

namespace Ale.VnFramework.Conditions
{
    /// <summary>
    /// 条件桥的项目级设置：哪些判定器**不**桥接进对话库。
    ///
    /// <para>放在工程里任一 <c>Resources/</c> 目录下、资产名为 <see cref="ResourceName"/>，运行时桥与编辑器的登记资产生成器都从这里读；
    /// 没有这份资产时一切照旧（全部判定器都桥接）。</para>
    ///
    /// <para>典型用途：只在某个系统内部才有意义的判定器 —— 例如求值主体是「正在派遣的任务」的，
    /// 对话里没有那个主体、恒判不成立。列进来后它既不注册成 Lua 函数，也不出现在条件向导的下拉里，杜绝误用。</para>
    /// </summary>
    [CreateAssetMenu(fileName = ResourceName, menuName = "Ale Toolkit/VN Framework/Condition Bridge Settings")]
    public sealed class VnConditionBridgeSettings : ScriptableObject
    {
        public const string ResourceName = "VnConditionBridgeSettings";

        [Tooltip("不桥接的判定器键前缀（区分大小写）。例：填 Dispatch. 排除全部 Dispatch.* 判定器。")]
        public List<string> excludedKeyPrefixes = new List<string>();

        /// <summary>从 <c>Resources</c> 读设置；工程里没有时返回 null。</summary>
        public static VnConditionBridgeSettings Load() => Resources.Load<VnConditionBridgeSettings>(ResourceName);

        /// <summary>判定器键是否命中排除前缀。<paramref name="settings"/> 为 null 时一律不排除。</summary>
        public static bool IsExcluded(string evaluatorKey, VnConditionBridgeSettings settings)
        {
            if (settings == null || string.IsNullOrEmpty(evaluatorKey) || settings.excludedKeyPrefixes == null) return false;
            foreach (var prefix in settings.excludedKeyPrefixes)
                if (!string.IsNullOrEmpty(prefix) && evaluatorKey.StartsWith(prefix, System.StringComparison.Ordinal)) return true;
            return false;
        }
    }
}
