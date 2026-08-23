namespace Ale.VnFramework.Conditions
{
    /// <summary>
    /// 剧情选择数据源：告诉判定器「玩家在某个分支点选了第几项」。由宿主实现并注册。
    ///
    /// <para><b>为什么要一个接口而不是直接读 Dialogue System 的变量</b>：对话变量只是运行时的一份
    /// 工作副本——回放/试玩会污染它、场景切换会重置它、读档要靠推送才同步。哪一次选择「算数」
    /// 是宿主的存档说了算，所以由宿主把权威值交出来。</para>
    ///
    /// <para><b>约定（实现方必须遵守）</b>：</para>
    /// <list type="bullet">
    /// <item>入参是<b>分支点的对话编号本身</b>（剧本里的那串数字，如 <c>2010086</c>），
    /// 不含任何变量名前缀；宿主变量若叫 <c>选项_2010086</c>，拼前缀是实现方的事。</item>
    /// <item>返回<b>选中项的序号，从 1 开始</b>。</item>
    /// <item><b>没选过 / 查不到一律返回 0</b>，不要返回 -1 或抛异常。</item>
    /// </list>
    ///
    /// <para>注册方式见 <see cref="VnConditionSources.RegisterService{T}"/>；
    /// 若同一份数据还要喂给别的系统（如节点树的解锁条件），注册同一个实现对象即可。</para>
    /// </summary>
    public interface IVnStoryChoiceSource
    {
        /// <summary>取某个分支点的选择结果。</summary>
        /// <param name="dialogueNumber">分支点的对话编号（不含变量名前缀）。</param>
        /// <returns>选中项序号（1 起）；未选择或查不到返回 0。</returns>
        int GetChoice(string dialogueNumber);
    }
}
