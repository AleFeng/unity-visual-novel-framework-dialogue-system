using System.Collections.Generic;
using Ale.Condition;

namespace Ale.VnFramework.Conditions
{
    /// <summary>
    /// 判定器：某个分支点的<b>选择序号</b>是否满足比较。键 <c>Vn.StoryChoiceIs</c>。
    ///
    /// <para>典型用途是把「剧情走向」变成可配置的门槛：章节树上某个结局节点的解锁条件
    /// ＝「在 2010086 选了第 2 项 且 在 2010094 选了第 2 项」。经
    /// <see cref="VnConditionBridge"/> 桥接后，同一个判定器也能直接写进对话条件。</para>
    ///
    /// <para>选择结果取自宿主注册的 <see cref="IVnStoryChoiceSource"/>——不是 Dialogue System 的
    /// 对话变量。变量只是运行时副本（回放会污染、切场景会重置），权威值在宿主存档里。
    /// 没注册数据源时 <b>fail-closed 返回 false</b> 并告警一次，而不是当作「没选过」放行。</para>
    ///
    /// <para><b>⚠️ 键与参数已冻结</b>：<c>Key</c>、参数的 <c>id</c>／顺序／个数一旦发布就不能再改——
    /// 它们序列化进了各方已配置的条件资产，也决定了 Lua 桥接的实参顺序。需要新语义请另开一个键。</para>
    /// </summary>
    [ConditionEvaluator("Vn.StoryChoiceIs")]
    public sealed class VnStoryChoiceIsEvaluator : IConditionEvaluator
    {
        /// <summary>分支点对话编号参数的 id。</summary>
        public const string ParamDialogueNumber = "dialogue";

        /// <summary>选项序号参数的 id。</summary>
        public const string ParamIndex = "index";

        // 判定器实例被注册表长期缓存复用（等价单例），schema 必须是无状态的静态只读数据。
        private static readonly ConditionParamDef[] Schema =
        {
            new ConditionParamDef(ParamDialogueNumber, ConditionParamType.String, false, "对话编号"),
            ConditionCompare.CreateOpParam(),
            new ConditionParamDef(ParamIndex, ConditionParamType.Int, false, "选项序号"),
        };

        public string Key => "Vn.StoryChoiceIs";
        public string DisplayName => "剧情选项序号";
        public string Category => "Vn";
        public IReadOnlyList<ConditionParamDef> ParamSchema => Schema;

        public bool Evaluate(IReadOnlyList<ConditionParam> parameters, IConditionContext ctx)
        {
            var source = ctx?.GetService<IVnStoryChoiceSource>();
            if (source == null)
            {
                VnConditionLog.WarnOnce("Vn.StoryChoiceIs",
                    "未注册 IVnStoryChoiceSource，剧情选项条件一律不成立。" +
                    "请在宿主用 VnConditionSources.RegisterService<IVnStoryChoiceSource>(…) 接上数据源。");
                return false;
            }

            var dialogueNumber = parameters.Find(ParamDialogueNumber)?.GetString();
            if (string.IsNullOrEmpty(dialogueNumber)) return false;

            // 数据源约定：未选择返回 0，因此「等于 0」正好表达「这个分支点还没做过选择」。
            var choice = source.GetChoice(dialogueNumber);
            var expected = parameters.Find(ParamIndex)?.GetInt() ?? 0L;
            return ConditionCompare.Compare(choice, expected, ConditionCompare.ReadOp(parameters));
        }
    }
}
