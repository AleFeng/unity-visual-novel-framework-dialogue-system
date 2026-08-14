using System;

namespace Ale.VnFramework
{
    /// <summary>
    /// 播放控制的**配置**部分（玩家在按钮条上调的那些）。
    ///
    /// <para><b>速度档位存 int 而不是枚举。</b>枚举一旦改名或调整数值，老存档就会解析成别的档位；
    /// 存整数则只依赖「1/2/3」这个约定，与 C# 类型无关。</para>
    /// </summary>
    [Serializable]
    public sealed class VnPlaybackSettingsData
    {
        /// <summary>自动播放开关。</summary>
        public bool autoPlay;
        /// <summary>自动播放的停留时长（秒）。</summary>
        public float autoPlayDelay = 1.0f;
        /// <summary>速度档位：1 / 2 / 3。</summary>
        public int speedTier = 1;
        /// <summary>快进倍率。</summary>
        public float fastForwardRate = 5.0f;
        /// <summary>新对话停止开关。</summary>
        public bool stopOnUnread = true;

        /// <summary>深拷贝。</summary>
        public VnPlaybackSettingsData Clone() => new VnPlaybackSettingsData
        {
            autoPlay = autoPlay,
            autoPlayDelay = autoPlayDelay,
            speedTier = speedTier,
            fastForwardRate = fastForwardRate,
            stopOnUnread = stopOnUnread,
        };
    }

    /// <summary>
    /// VN 演出系统需要持久化的全部状态。**本包只提供 Get / Set，不做 Load / Save**——
    /// 落盘由宿主的存档系统负责：读档时取出来 <c>Set</c> 进来，存档时 <c>Get</c> 出去写进存档槽。
    ///
    /// <para>纯字段 + <c>[Serializable]</c>，JsonUtility / ES3 / 自定义二进制都能直接吃。</para>
    ///
    /// <para><b>配置不编码，已读记录编码。</b>配置就五个值，做成不透明串只会妨碍排查；
    /// 已读记录则可能上万条，必须压（见 <see cref="VnReadHistoryCodec"/>）。</para>
    /// </summary>
    [Serializable]
    public sealed class VnStorySaveData
    {
        /// <summary>DTO 版本。字段增删时用它做迁移判断。</summary>
        public int version = 1;

        /// <summary>播放控制配置。为 null 时载入方跳过配置、只处理已读记录。</summary>
        public VnPlaybackSettingsData settings;

        /// <summary>已读记录（<see cref="VnReadHistoryCodec"/> 产出的 Base64 串）。空串表示什么都没读过。</summary>
        public string readHistory;

        /// <summary>
        /// 存档时的剧本结构指纹。载入时与当前剧本比对，不一致说明剧本被整库重导入过、
        /// 会话与节点 ID 已重编号，已读记录会被丢弃并告警——静默错位比丢记录更糟。
        /// 见 <see cref="VnReadHistory.BuildStamp"/>。
        /// </summary>
        public string readHistoryStamp;
    }
}
