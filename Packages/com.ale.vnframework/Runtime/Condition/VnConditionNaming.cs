using System.Text;

namespace Ale.VnFramework.Conditions
{
    /// <summary>
    /// 判定器键 ↔ Lua 函数名 / 条件向导菜单路径的映射规则。
    /// 运行时桥（<see cref="VnConditionBridge"/>）与编辑器的登记资产生成器**必须**共用本类，
    /// 否则资产里写的函数名会与实际注册的对不上，向导选出来的条件运行期直接报「找不到函数」。
    ///
    /// <para>⚠️ 本类产出的函数名会被写进使用者的对话库（<c>DialogueEntry.conditionsString</c>）。
    /// 规则一旦发布就**不能再改** —— 改了等于让所有已存的条件静默失效。</para>
    /// </summary>
    public static class VnConditionNaming
    {
        /// <summary>Lua 函数名前缀。避免与使用者自己的 Lua 全局函数、以及 Dialogue System 内置函数撞名。</summary>
        public const string LuaFunctionPrefix = "AleCond_";

        /// <summary>条件向导里的菜单根节点。</summary>
        public const string MenuRoot = "Ale 条件";

        /// <summary>
        /// 支持的最大参数个数。超过此数的判定器会被跳过并告警。
        /// 上限来自 <see cref="VnConditionLuaBinding"/> 那族定长方法 —— Dialogue System 的
        /// <c>LuaMethodFunction</c> 直接 <c>Method.Invoke</c>，不做变参展开，只能按元数逐个准备方法。
        /// </summary>
        public const int MaxParameterCount = 8;

        /// <summary>
        /// 判定器键 → Lua 函数名。前缀 + 键，键里非 <c>[A-Za-z0-9_]</c> 的字符一律换成 <c>_</c>。
        /// 例：<c>Condition.NumberCompare</c> → <c>AleCond_Condition_NumberCompare</c>。
        ///
        /// <para>刻意**保留完整的键**而不截掉分类前缀求短：截短规则依赖「当前有哪些判定器」这个全集，
        /// 日后第三方新增一个判定器就可能与旧的撞名，迫使已发布的名字改写，届时使用者对话库里
        /// 存好的条件会静默失效。难看但永不漂移，比短名重要。</para>
        ///
        /// <para>同理刻意**只保留 ASCII**：这个字符串既是向导菜单的叶子标签，也是真实的 Lua 标识符，
        /// 中文标识符能否被解释器正确词法分析不值得赌。</para>
        /// </summary>
        public static string ToLuaFunctionName(string evaluatorKey)
        {
            if (string.IsNullOrEmpty(evaluatorKey)) return null;

            var sb = new StringBuilder(LuaFunctionPrefix.Length + evaluatorKey.Length);
            sb.Append(LuaFunctionPrefix);
            foreach (var c in evaluatorKey)
            {
                bool ascii = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_';
                sb.Append(ascii ? c : '_');
            }
            return sb.ToString();
        }

        /// <summary>
        /// 「这个判定器要不要接、接成什么名字、几个参数」的**唯一**决策点。
        /// 运行时桥（<see cref="VnConditionBridge"/>）与编辑器的登记资产生成器共用本方法。
        ///
        /// <para>⚠️ 两边共用是必须的：一旦规则漂移，登记资产就会列出桥没注册的函数，
        /// 使用者在向导里选了它、运行期却报「attempt to call nil」，而且编辑期毫无征兆。</para>
        ///
        /// <para>不含撞名检查 —— 那需要跨判定器的全局状态，由调用方各自维护。</para>
        /// </summary>
        /// <returns>可接入返回 <c>true</c>；否则 <c>false</c> 且 <paramref name="skipReason"/> 给出人话原因。</returns>
        public static bool TryPlan(Ale.Condition.IConditionEvaluator evaluator,
            out string luaFunctionName, out int parameterCount, out string skipReason)
        {
            luaFunctionName = null;
            parameterCount = 0;
            skipReason = null;

            if (evaluator == null || string.IsNullOrEmpty(evaluator.Key))
            {
                skipReason = "判定器或其 Key 为空";
                return false;
            }

            luaFunctionName = ToLuaFunctionName(evaluator.Key);
            if (string.IsNullOrEmpty(luaFunctionName))
            {
                skipReason = "无法由 Key 生成合法的 Lua 函数名";
                return false;
            }

            var schema = evaluator.ParamSchema;
            parameterCount = schema == null ? 0 : schema.Count;
            if (parameterCount > MaxParameterCount)
            {
                skipReason = $"有 {parameterCount} 个参数，超过上限 {MaxParameterCount}";
                return false;
            }

            return true;
        }

        /// <summary>
        /// 判定器分类 + Lua 函数名 → 条件向导的菜单路径。
        ///
        /// <para>⚠️ Dialogue System 用 <c>/</c> 分子菜单，但生成调用语句时只取**最后一段**当函数名
        /// （<c>LuaConditionWizard</c> → <c>Tools.GetAllAfterSlashes</c>）。所以叶子段必须正好是真实的
        /// Lua 函数名，分类只能塞在前面几段 —— 这也是菜单里显示的是 <c>AleCond_*</c> 而非中文显示名的原因。</para>
        /// </summary>
        public static string ToMenuPath(string category, string luaFunctionName)
        {
            if (string.IsNullOrEmpty(luaFunctionName)) return null;

            // 分类里若自带 '/' 会多分出一级子菜单，把叶子段挤走，必须消掉。
            var safeCategory = string.IsNullOrEmpty(category) ? null : category.Replace('/', '_');
            return string.IsNullOrEmpty(safeCategory)
                ? MenuRoot + "/" + luaFunctionName
                : MenuRoot + "/" + safeCategory + "/" + luaFunctionName;
        }
    }
}
