using System;
using System.Collections.Generic;
using System.IO;

namespace Ale.VnFramework
{
    /// <summary>
    /// 一个会话的已读位图。索引即对话节点 ID，每个节点占 <b>2 bit</b>。
    /// </summary>
    public struct FVnReadRecord
    {
        /// <summary>会话 ID。</summary>
        public int ConversationId;

        /// <summary>容量 = 该会话的最大节点 ID + 1。位图按它开长度。</summary>
        public int Capacity;

        /// <summary>位图字节。长度恒为 <see cref="VnReadHistoryCodec.ByteLengthFor"/>。</summary>
        public byte[] Bits;
    }

    /// <summary>
    /// 已读记录的紧凑编解码器。**纯函数、不依赖 Unity 与 Dialogue System**，故可直接单测。
    ///
    /// <para><b>为什么不是「每节点一个 ID」。</b>一个节点 ID 占 4 字节，而它真正需要的信息只有
    /// 「未读 / 已显示 / 已提供」三态——2 bit 就够，密度差 16 倍。
    /// Dialogue System 自带的存档格式是 <c>Conversation[17].SimX="1;d;2;u;3;o"</c>
    /// 这样的字符串对，每节点约 <c>ID 位数 + 3</c> 字节，**而且把未读节点也一并写进去**，
    /// 体积正比于剧本总量而非阅读量；1 万行剧本约 60–80 KB。本编码同样规模约 2.5 KB。</para>
    ///
    /// <para><b>为什么整个输出是一个 Base64 大字符串，而不是一串 DTO。</b>
    /// 若做成 <c>List&lt;记录&gt;</c> 交给 JsonUtility，每条记录的字段名、括号、逗号加起来
    /// 会比位图本体还大（200 段对话时字段名开销就有几 KB），压缩收益基本被吃光。
    /// 一次性写成二进制再整体 Base64，开销与对话粒度无关。</para>
    ///
    /// <para><b>「整本统一状态」的塌缩</b>是按你的思路做的：一个会话里所有节点状态相同时
    /// 只存 <c>会话 ID + 容量 + 状态</c> 共 9 字节，不存位图。注意这里的判据是
    /// <b>状态完全一致</b>而不是「都已读」——后者会把「已显示」和「已提供」混为一谈，
    /// 而 Dialogue System 的 <c>emTagForOldResponses</c>（把选过的选项置灰）正是靠这个区别工作的。
    /// 实践中一段读完的对话往往两种状态并存，那就老实存位图，本来也不大。</para>
    /// </summary>
    public static class VnReadHistoryCodec
    {
        /// <summary>格式版本。解码时不匹配即整体丢弃，绝不半解析。</summary>
        public const ushort Version = 1;

        /// <summary>
        /// 单个会话的容量上限。Dialogue System 编辑器分配节点 ID 用 <c>max + 1</c> 且**不回收**，
        /// 反复增删的老剧本可能出现「只有 200 条却编到 5 万号」的空洞，位图会白白开到 12.5 KB。
        /// 超过此值就跳过该会话并告警，不让存档被一个畸形会话撑爆。
        /// </summary>
        public const int MaxCapacity = 100000;

        /// <summary>未读。</summary>
        public const byte StatusUntouched = 0;
        /// <summary>已显示（对应 DS 的 WasDisplayed）。</summary>
        public const byte StatusDisplayed = 1;
        /// <summary>已提供（对应 DS 的 WasOffered，选项被列出但未必被选）。</summary>
        public const byte StatusOffered = 2;

        /// <summary>给定容量所需的位图字节数（每字节装 4 个节点）。</summary>
        public static int ByteLengthFor(int capacity) => capacity <= 0 ? 0 : (capacity + 3) / 4;

        /// <summary>读取第 <paramref name="index"/> 个节点的状态。越界返回 <see cref="StatusUntouched"/>。</summary>
        public static byte GetStatus(byte[] bits, int index)
        {
            if (bits == null || index < 0) return StatusUntouched;
            int b = index >> 2;
            if (b >= bits.Length) return StatusUntouched;
            return (byte)((bits[b] >> ((index & 3) * 2)) & 0x3);
        }

        /// <summary>写入第 <paramref name="index"/> 个节点的状态。越界静默忽略。</summary>
        public static void SetStatus(byte[] bits, int index, byte status)
        {
            if (bits == null || index < 0) return;
            int b = index >> 2;
            if (b >= bits.Length) return;
            int shift = (index & 3) * 2;
            bits[b] = (byte)((bits[b] & ~(0x3 << shift)) | ((status & 0x3) << shift));
        }

        /// <summary>
        /// 编码为 Base64。<paramref name="records"/> 为空或全为未读时返回空字符串
        /// （空字符串是合法输入，解码回来就是「什么都没读过」）。
        /// </summary>
        public static string Encode(IList<FVnReadRecord> records)
        {
            if (records == null || records.Count == 0) return string.Empty;

            var full = new List<KeyValuePair<FVnReadRecord, byte>>();
            var partial = new List<FVnReadRecord>();

            foreach (var r in records)
            {
                if (r.Capacity <= 0 || r.Capacity > MaxCapacity || r.Bits == null) continue;

                byte uniform;
                if (IsUniform(r, out uniform))
                {
                    // 全未读的会话不必存：解码方拿不到条目时本就当作未读。
                    if (uniform == StatusUntouched) continue;
                    full.Add(new KeyValuePair<FVnReadRecord, byte>(r, uniform));
                }
                else
                {
                    partial.Add(r);
                }
            }

            if (full.Count == 0 && partial.Count == 0) return string.Empty;

            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                w.Write(Version);

                w.Write(full.Count);
                foreach (var kv in full)
                {
                    w.Write(kv.Key.ConversationId);
                    w.Write(kv.Key.Capacity);
                    w.Write(kv.Value);
                }

                w.Write(partial.Count);
                foreach (var r in partial)
                {
                    w.Write(r.ConversationId);
                    w.Write(r.Capacity);
                    int len = Math.Min(r.Bits.Length, ByteLengthFor(r.Capacity));
                    w.Write(len);
                    w.Write(r.Bits, 0, len);
                }

                w.Flush();
                return Convert.ToBase64String(ms.ToArray());
            }
        }

        /// <summary>
        /// 解码。失败时返回 false 并给出中文原因，<paramref name="records"/> 为空列表——
        /// **绝不半解析**：残缺的已读记录比没有记录更糟（玩家会看到本该未读的剧情被当作已读跳过）。
        /// </summary>
        public static bool TryDecode(string base64, out List<FVnReadRecord> records, out string error)
        {
            records = new List<FVnReadRecord>();
            error = null;

            if (string.IsNullOrEmpty(base64)) return true;   // 空 = 什么都没读过，是合法状态

            byte[] raw;
            try
            {
                raw = Convert.FromBase64String(base64);
            }
            catch (FormatException)
            {
                error = "不是合法的 Base64 字符串";
                return false;
            }

            try
            {
                using (var ms = new MemoryStream(raw))
                using (var r = new BinaryReader(ms))
                {
                    var version = r.ReadUInt16();
                    if (version != Version)
                    {
                        error = $"格式版本不匹配（存档 {version}，当前 {Version}）";
                        return false;
                    }

                    int fullCount = r.ReadInt32();
                    if (fullCount < 0) { error = "整本已读段数为负"; return false; }
                    for (int i = 0; i < fullCount; i++)
                    {
                        int id = r.ReadInt32();
                        int capacity = r.ReadInt32();
                        byte status = r.ReadByte();
                        if (capacity <= 0 || capacity > MaxCapacity) { error = $"会话 {id} 的容量非法（{capacity}）"; return false; }

                        var bits = new byte[ByteLengthFor(capacity)];
                        for (int e = 0; e < capacity; e++) SetStatus(bits, e, status);
                        records.Add(new FVnReadRecord { ConversationId = id, Capacity = capacity, Bits = bits });
                    }

                    int partialCount = r.ReadInt32();
                    if (partialCount < 0) { error = "部分已读段数为负"; return false; }
                    for (int i = 0; i < partialCount; i++)
                    {
                        int id = r.ReadInt32();
                        int capacity = r.ReadInt32();
                        int len = r.ReadInt32();
                        if (capacity <= 0 || capacity > MaxCapacity) { error = $"会话 {id} 的容量非法（{capacity}）"; return false; }
                        if (len < 0 || len > ByteLengthFor(capacity)) { error = $"会话 {id} 的位图长度非法（{len}）"; return false; }

                        var bits = new byte[ByteLengthFor(capacity)];
                        var read = r.ReadBytes(len);
                        if (read.Length != len) { error = $"会话 {id} 的位图被截断"; return false; }
                        Buffer.BlockCopy(read, 0, bits, 0, len);
                        records.Add(new FVnReadRecord { ConversationId = id, Capacity = capacity, Bits = bits });
                    }
                }
            }
            catch (EndOfStreamException)
            {
                records.Clear();
                error = "数据被截断";
                return false;
            }

            return true;
        }

        // 会话内所有节点状态是否完全一致。一致才能塌缩成「容量 + 状态」而不丢信息。
        private static bool IsUniform(FVnReadRecord record, out byte status)
        {
            status = GetStatus(record.Bits, 0);
            for (int i = 1; i < record.Capacity; i++)
            {
                if (GetStatus(record.Bits, i) != status) return false;
            }
            return true;
        }
    }
}
