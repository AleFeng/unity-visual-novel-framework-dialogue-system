using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Ale.Condition;
using PixelCrushers.DialogueSystem;
using UnityEngine;

namespace Ale.VnFramework.Conditions
{
    /// <summary>
    /// 把 Ale Toolkit 的条件判定器桥接成 Dialogue System 的 Lua 条件函数。
    ///
    /// <para>每个判定器注册为一个 Lua 全局函数（命名规则见 <see cref="VnConditionNaming"/>），
    /// 于是节点的 Conditions 里可以直接写
    /// <c>AleCond_Condition_NumberCompare("时间段", "大于等于", 3) == true</c>，
    /// 并与既有的 <c>Variable["选项序号"] == 1</c> 用 <c>and</c> / <c>or</c> 自由组合。</para>
    ///
    /// <para>要让这些函数出现在条件向导的 <b>Custom</b> 下拉里，还需要工程内有一份登记资产
    /// （<c>CustomLuaFunctionInfo</c>）—— 向导扫的是资产，不是 Lua 环境。
    /// 该资产由编辑器侧的生成器产出，见菜单 <c>Tools/Ale/VN Framework</c>。</para>
    /// </summary>
    public static class VnConditionBridge
    {
        private static readonly List<string> RegisteredNames = new List<string>();

        /// <summary>
        /// 求值时传给判定器的上下文。数值 / 标记取自宿主经 <see cref="VnConditionSources"/> 注册的实时 getter。
        /// </summary>
        internal static IConditionContext Context => VnConditionContext.Instance;

        /// <summary>已注册的 Lua 条件函数名（只读快照，供自检与测试）。</summary>
        public static IReadOnlyList<string> RegisteredFunctionNames => RegisteredNames;

        /// <summary>
        /// 安装桥。
        ///
        /// <para>⚠️ 时机必须晚于 <c>SubsystemRegistration</c>：Dialogue System 自己在那个时机调
        /// <c>Lua.ResetLuaEnvironment()</c>，会**连同所有已注册函数一起清空**
        /// （<c>Lua.cs</c> 的 <c>InitStaticVariables</c>，仅编辑器内生效）。
        /// <c>BeforeSceneLoad</c> 排在其后，注册得以存活。</para>
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            RegisterAll();
        }

        /// <summary>
        /// 枚举全部判定器并注册为 Lua 函数。重复调用安全（先注销再注册）。
        /// </summary>
        public static void RegisterAll()
        {
            UnregisterAll();
            VnConditionLog.Reset();

            var registry = ConditionRegistry.Default;

            // 无条件扫一次：toolkit 的 ConditionRuntime 也在 BeforeSceneLoad 填这张表，
            // 而两个同时机的 [RuntimeInitializeOnLoadMethod] 之间**没有顺序保证**，
            // 不能假设它已经跑过。AutoRegisterFromAssemblies 同键覆盖，重复调用无副作用。
            registry.AutoRegisterFromAssemblies();

            // Lua 函数名 → 判定器键，用于撞名自检。全键命名下正常不会撞，撞了必须说出来而不是静默丢弃。
            var claimed = new Dictionary<string, string>();

            foreach (var evaluator in registry.All)
            {
                if (evaluator == null || string.IsNullOrEmpty(evaluator.Key)) continue;

                var luaName = VnConditionNaming.ToLuaFunctionName(evaluator.Key);
                if (string.IsNullOrEmpty(luaName)) continue;

                var schema = evaluator.ParamSchema;
                var parameterCount = schema == null ? 0 : schema.Count;
                if (parameterCount > VnConditionNaming.MaxParameterCount)
                {
                    Debug.LogWarning($"{VnConditionLog.Prefix} 判定器「{evaluator.Key}」有 {parameterCount} 个参数，" +
                                     $"超过上限 {VnConditionNaming.MaxParameterCount}，已跳过 —— 它不会出现在" +
                                     "Dialogue System 的条件下拉里。");
                    continue;
                }

                string owner;
                if (claimed.TryGetValue(luaName, out owner))
                {
                    Debug.LogWarning($"{VnConditionLog.Prefix} 判定器「{evaluator.Key}」与「{owner}」映射出同一个 Lua " +
                                     $"函数名 {luaName}，已跳过前者。请改掉其中一个判定器的 Key。");
                    continue;
                }

                var method = typeof(VnConditionLuaBinding).GetMethod(
                    "Eval" + parameterCount.ToString(CultureInfo.InvariantCulture),
                    BindingFlags.Public | BindingFlags.Instance);
                if (method == null)
                {
                    Debug.LogWarning($"{VnConditionLog.Prefix} 找不到 {parameterCount} 元的绑定方法，" +
                                     $"判定器「{evaluator.Key}」已跳过。");
                    continue;
                }

                // 先注销再注册：Lua.RegisterFunction **不是覆盖语义**，同名已存在时它只打一条
                // 「已注册」告警并保留旧绑定。关掉「重新加载域」后进播放模式尤其需要这一步。
                Lua.UnregisterFunction(luaName);
                Lua.RegisterFunction(luaName, new VnConditionLuaBinding(evaluator, luaName), method);

                claimed[luaName] = evaluator.Key;
                RegisteredNames.Add(luaName);
            }
        }

        /// <summary>注销本桥注册过的全部 Lua 函数。</summary>
        public static void UnregisterAll()
        {
            for (int i = 0; i < RegisteredNames.Count; i++)
            {
                Lua.UnregisterFunction(RegisteredNames[i]);
            }
            RegisteredNames.Clear();
        }
    }
}
