using System.Collections.Generic;
using System.Text;
using PixelCrushers.DialogueSystem;
using UnityEngine;

namespace Ale.VnFramework
{
    /// <summary>
    /// 已读记录。**运行时不自己造轮子**——判定「这一行读过没有」直接用 Dialogue System 的
    /// <c>SimStatus</c>（逐节点三态，由 <c>ConversationModel</c> 在行准备时自动打标，我们零维护成本）。
    /// 本类只负责两件 DS 不管的事：<b>紧凑持久化</b>与<b>把存档写回 Lua</b>。
    ///
    /// <para><b>状态住在哪。</b>已水合的会话，权威在 Lua（DS 自己也在往里写）；
    /// 尚未水合的会话，权威在 <see cref="_pending"/>。读档时不一次性灌 Lua，
    /// 而是等到某段对话真正开始时才水合那一段——一次性灌 1 万条要走 DS 的多层 LuaTable 建表，
    /// 是几十到几百毫秒的读档卡顿，而按会话摊开之后每次只有几十条。</para>
    ///
    /// <para>因为有这两个来源，<see cref="Encode"/> 必须分别取：已水合的读 Lua，未水合的读
    /// <see cref="_pending"/>。只读 Lua 的话，「读档后立刻存档」会把尚未水合的会话全存成未读。</para>
    /// </summary>
    public sealed class VnReadHistory
    {
        // 会话 ID -> 位图。只装**尚未水合进 Lua** 的会话；水合一个就搬走一个。
        private readonly Dictionary<int, byte[]> _pending = new Dictionary<int, byte[]>();
        // 已经把状态推进 Lua 的会话 ID。
        private readonly HashSet<int> _hydrated = new HashSet<int>();

        private static bool _warnedSimStatusOff;

        #region SimStatus 开关

        /// <summary>
        /// 确保 <c>SimStatus</c> 处于开启状态，必要时运行时补开并告警一次。
        ///
        /// <para><b>不开会 fail-open 成全量误报。</b><c>DialogueLua.GetSimStatus</c> 在开关关闭时
        /// **对任何节点都返回 <c>Untouched</c>**，于是每一行都被判成未读——「新对话停止」会在
        /// 快进刚按下的第一行就触发，表现为「快进按下去立刻就停」。</para>
        ///
        /// <para><b>必须在 <c>Start</c> 或更晚调用，不能在 <c>Awake</c>。</b>
        /// <c>DialogueSystemController.Awake</c> 里有一句
        /// <c>DialogueLua.includeSimStatus = includeSimStatus || visuallyMarkOldResponses;</c>，
        /// 而它与 <c>VnStoryManager</c> 在同一个 GameObject 上、两者 <c>Awake</c> 的先后未定义——
        /// 在 Awake 里设的值可能当场被它覆盖回 false。</para>
        ///
        /// <para><b>顺带把 DS 自己的存档关掉。</b><c>PersistentDataManager.includeSimStatus</c>
        /// 一旦为 true，DS 的 <c>GetSaveData()</c> 会为每个节点再写一份
        /// <c>Conversation[N].SimX="..."</c> 的 Lua 文本——与本类的位图是同一份数据的两次存储，
        /// 而且是最啰嗦的那种编码。已读记录由本包负责，DS 那边不必再存。</para>
        /// </summary>
        public static void EnsureSimStatusEnabled()
        {
            if (!DialogueManager.hasInstance) return;

            if (!DialogueManager.instance.includeSimStatus || !DialogueLua.includeSimStatus)
            {
                if (!_warnedSimStatusOff)
                {
                    _warnedSimStatusOff = true;
                    Debug.LogWarning("剧情演出 >> Dialogue Manager 的 Include SimStatus 未勾选，已在运行时补开。" +
                                     "「新对话停止」与已读记录都依赖它——不开时 GetSimStatus 对任何节点都返回 Untouched，" +
                                     "表现为快进一按就停。建议直接在预制体上勾上。本提示只出现一次。");
                }
                DialogueManager.instance.includeSimStatus = true;
                DialogueLua.includeSimStatus = true;
            }

            // 与上面无关，每次都压一遍：DialogueSystemController.Awake 会把它跟着 includeSimStatus 打开。
            PersistentDataManager.includeSimStatus = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _warnedSimStatusOff = false;

        #endregion

        #region 查询

        /// <summary>
        /// 该节点是否**从未出现过**。「新对话停止」的判据。
        ///
        /// <para><c>id == 0</c> 是会话的 START 结构节点，不是一句台词，恒返回 false——
        /// 否则每段对话刚开始就会被判成「遇到新内容」。</para>
        /// </summary>
        public bool IsUnread(DialogueEntry entry)
        {
            if (entry == null || entry.id == 0) return false;
            return DialogueLua.GetSimStatus(entry) == DialogueLua.Untouched;
        }

        #endregion

        #region 水合

        /// <summary>
        /// 把某段对话的已读状态推进 Lua。对话开始时调用，重复调用是空操作。
        ///
        /// <para>是**覆盖**语义：存档里没有的节点会被显式置为 <c>Untouched</c>，
        /// 而不是留着读档之前的残留状态。</para>
        /// </summary>
        public void HydrateConversation(int conversationId)
        {
            if (_hydrated.Contains(conversationId)) return;

            var conversation = DialogueManager.masterDatabase != null
                ? DialogueManager.masterDatabase.GetConversation(conversationId)
                : null;
            if (conversation == null) return;

            _hydrated.Add(conversationId);
            byte[] bits;
            _pending.TryGetValue(conversationId, out bits);
            _pending.Remove(conversationId);

            var entries = conversation.dialogueEntries;
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null || entry.id == 0) continue;
                DialogueLua.MarkDialogueEntry(entry, ToDialogueLuaStatus(VnReadHistoryCodec.GetStatus(bits, entry.id)));
            }
        }

        /// <summary>把全部会话一次性水合。宿主若不介意读档时的一次性开销，可以直接调它。</summary>
        public void HydrateAll()
        {
            var db = DialogueManager.masterDatabase;
            if (db == null) return;
            for (int i = 0; i < db.conversations.Count; i++) HydrateConversation(db.conversations[i].id);
        }

        #endregion

        #region 编解码

        /// <summary>
        /// 导出为紧凑 Base64 串。已水合的会话从 Lua 取，未水合的从待水合表取——
        /// 只读 Lua 的话「读档后立刻存档」会把还没访问过的会话全存成未读。
        /// </summary>
        public string Encode()
        {
            var db = DialogueManager.masterDatabase;
            if (db == null) return string.Empty;

            var records = new List<FVnReadRecord>(db.conversations.Count);
            for (int i = 0; i < db.conversations.Count; i++)
            {
                var conversation = db.conversations[i];
                if (conversation == null) continue;

                int capacity = CapacityOf(conversation);
                if (capacity <= 0) continue;
                if (capacity > VnReadHistoryCodec.MaxCapacity)
                {
                    Debug.LogWarning($"剧情演出 >> 会话 {conversation.id} 的最大节点 ID 达到 {capacity - 1}，" +
                                     $"超过 {VnReadHistoryCodec.MaxCapacity} 上限，其已读记录不会被存档。" +
                                     "这通常意味着该会话被反复增删过（DS 分配节点 ID 用 max+1 且不回收）。");
                    continue;
                }

                byte[] bits;
                if (_hydrated.Contains(conversation.id))
                {
                    bits = new byte[VnReadHistoryCodec.ByteLengthFor(capacity)];
                    var entries = conversation.dialogueEntries;
                    for (int e = 0; e < entries.Count; e++)
                    {
                        var entry = entries[e];
                        if (entry == null || entry.id == 0) continue;
                        VnReadHistoryCodec.SetStatus(bits, entry.id, FromDialogueLuaStatus(DialogueLua.GetSimStatus(entry)));
                    }
                }
                else if (!_pending.TryGetValue(conversation.id, out bits))
                {
                    continue;   // 既没水合、也没存档数据 = 整本未读，不必写
                }

                records.Add(new FVnReadRecord { conversationId = conversation.id, capacity = capacity, bits = bits });
            }

            return VnReadHistoryCodec.Encode(records);
        }

        /// <summary>
        /// 载入存档串。**覆盖**语义：先丢弃当前全部已读状态，再按存档重建。
        /// 解码失败时返回 false 并把状态清空——残缺的已读记录比没有记录更糟，
        /// 玩家会看到本该未读的剧情被当作已读跳过。
        /// </summary>
        public bool Load(string encoded, out string error)
        {
            ClearAll();

            List<FVnReadRecord> records;
            if (!VnReadHistoryCodec.TryDecode(encoded, out records, out error)) return false;

            for (int i = 0; i < records.Count; i++) _pending[records[i].conversationId] = records[i].bits;
            return true;
        }

        /// <summary>
        /// 清空全部已读状态：待水合表清掉，已水合的会话在 Lua 里逐条置回 <c>Untouched</c>。
        /// 开新游戏 / 读档前清场用。
        /// </summary>
        public void ClearAll()
        {
            _pending.Clear();

            var db = DialogueManager.masterDatabase;
            if (db != null && _hydrated.Count > 0)
            {
                for (int i = 0; i < db.conversations.Count; i++)
                {
                    var conversation = db.conversations[i];
                    if (conversation == null || !_hydrated.Contains(conversation.id)) continue;
                    var entries = conversation.dialogueEntries;
                    for (int e = 0; e < entries.Count; e++)
                    {
                        if (entries[e] != null && entries[e].id != 0) DialogueLua.MarkDialogueEntryUntouched(entries[e]);
                    }
                }
            }
            _hydrated.Clear();
        }

        #endregion

        #region 剧本指纹

        /// <summary>
        /// 剧本结构指纹。存进存档，读档时不匹配就丢弃已读记录并告警。
        ///
        /// <para><b>为什么需要它。</b>本编码以「会话 ID + 节点 ID」为下标，而这两个 ID
        /// 会随剧本的整库重导入（Chat Mapper / articy / CSV）而重编号，届时存档里的已读位
        /// 会**静默错位**——玩家看到没读过的剧情被当作已读跳过，比丢掉记录更糟。</para>
        ///
        /// <para>日常的「追加剧情 / 删节点」不会触发：DS 编辑器分配节点 ID 用 <c>max + 1</c>
        /// 且不回收，已有 ID 的含义不变。所以指纹只统计**会话 ID 与节点数**，
        /// 对追加内容不敏感——那种情况靠位图长度自愈（多出来的节点读作未读，正确的方向）。</para>
        /// </summary>
        public static string BuildStamp()
        {
            var db = DialogueManager.masterDatabase;
            if (db == null) return string.Empty;

            // FNV-1a。要的是「变了能发现」，不是密码学强度。
            const uint offset = 2166136261, prime = 16777619;
            uint hash = offset;
            System.Action<int> feed = v =>
            {
                for (int i = 0; i < 4; i++) { hash ^= (byte)(v >> (i * 8)); hash *= prime; }
            };

            var ids = new List<int>(db.conversations.Count);
            for (int i = 0; i < db.conversations.Count; i++)
            {
                if (db.conversations[i] != null) ids.Add(i);
            }
            ids.Sort((a, b) => db.conversations[a].id.CompareTo(db.conversations[b].id));

            foreach (var i in ids)
            {
                feed(db.conversations[i].id);
                feed(db.conversations[i].dialogueEntries != null ? db.conversations[i].dialogueEntries.Count : 0);
            }

            var sb = new StringBuilder();
            sb.Append(ids.Count).Append('-').Append(hash.ToString("X8"));
            return sb.ToString();
        }

        #endregion

        #region 内部

        // 容量 = 最大节点 ID + 1。用最大 ID 而不是节点数：删过节点的会话 ID 是有空洞的。
        private static int CapacityOf(Conversation conversation)
        {
            var entries = conversation.dialogueEntries;
            if (entries == null || entries.Count == 0) return 0;

            int max = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i] != null && entries[i].id > max) max = entries[i].id;
            }
            return max + 1;
        }

        private static string ToDialogueLuaStatus(byte status)
        {
            switch (status)
            {
                case VnReadHistoryCodec.StatusDisplayed: return DialogueLua.WasDisplayed;
                case VnReadHistoryCodec.StatusOffered: return DialogueLua.WasOffered;
                default: return DialogueLua.Untouched;
            }
        }

        private static byte FromDialogueLuaStatus(string status)
        {
            if (status == DialogueLua.WasDisplayed) return VnReadHistoryCodec.StatusDisplayed;
            if (status == DialogueLua.WasOffered) return VnReadHistoryCodec.StatusOffered;
            return VnReadHistoryCodec.StatusUntouched;
        }

        #endregion
    }
}
