using System;
using System.Collections;
using PixelCrushers.DialogueSystem;
using UnityEngine;

namespace Ale.VnFramework
{
    /// <summary>台词播放速度档位。枚举值即倍率，别改数值——存档里存的就是它。</summary>
    public enum EVnPlaybackSpeedTier
    {
        /// <summary>1 倍速（默认）。</summary>
        X1 = 1,
        /// <summary>2 倍速。</summary>
        X2 = 2,
        /// <summary>3 倍速。</summary>
        X3 = 3,
    }

    /// <summary>快进状态。</summary>
    public enum EVnFastForwardState
    {
        /// <summary>未按住。</summary>
        Off,
        /// <summary>按住且生效。</summary>
        Active,

        /// <summary>
        /// 按住了，但已被「新对话停止」打断——<b>必须松开再按</b>才能重新生效。
        ///
        /// <para>用三态而不是一个 bool，是因为「按着却不生效」这个状态必须能被看见：
        /// 否则玩家一路按住会在每个未读行抖动式地一停一走，而日志里什么都看不出来。</para>
        /// </summary>
        Suppressed,
    }

    /// <summary>
    /// 演出播放控制：自动播放、播放速度档位、快进、新对话停止。
    ///
    /// <para><b>挂在 <see cref="VnStoryManager"/> 所在的那个 GameObject 上</b>（Dialogue Manager 根）。
    /// 选项菜单的通知是 Dialogue System 用 <c>BroadcastMessage</c> 发到该物体的，挂别处收不到；
    /// 其余通知走 <c>DialogueManager.instance</c> 的 C# 事件，与挂在哪无关。
    /// <see cref="Start"/> 里会检查并告警。</para>
    ///
    /// <para><b>本组件不绑任何按键。</b>本包不引用 Input System，而旧版 <c>UnityEngine.Input</c>
    /// 在启用了新输入系统的工程里直接抛异常。UI 按钮走 EventSystem（见 <c>VnPlaybackButton</c>），
    /// 键盘由宿主自行绑到这里的公开方法上。</para>
    /// </summary>
    [DisallowMultipleComponent]
    public class VnPlaybackController : MonoBehaviour
    {
        #region 配置

        [Header("自动播放")]
        [Tooltip("自动播放 默认是否开启。需求要求默认关闭。")]
        [SerializeField] private bool autoPlayDefaultOn = false;
        [Tooltip("自动播放 停留时长（秒）：本行演完之后再等多久推进下一句。快进时会按倍率压缩。")]
        [SerializeField] private float autoPlayDelay = 1.0f;
        [Tooltip("等待打字机起播的宽限时长（秒）。见 CorAutoAdvance 的说明，一般不用改。")]
        [SerializeField] private float typewriterStartGrace = 0.5f;

        [Header("播放速度")]
        [Tooltip("默认速度档位。只影响台词打字机（字速与标点停顿）。")]
        [SerializeField] private EVnPlaybackSpeedTier defaultSpeedTier = EVnPlaybackSpeedTier.X1;

        [Header("快进")]
        [Tooltip("快进倍率。默认 5 倍。作用于打字机、补间、延时、角色动画、粒子与语音。")]
        [SerializeField] private float fastForwardRate = 5.0f;
        [Tooltip("新对话停止 默认是否开启。开启后，快进中遇到从未出现过的对话节点会中止快进。")]
        [SerializeField] private bool stopOnUnreadDefaultOn = true;

        #endregion

        #region 状态

        private bool _autoPlay;
        private EVnPlaybackSpeedTier _speedTier;
        private EVnFastForwardState _fastForwardState;
        private bool _stopOnUnread;

        private Coroutine _advanceRoutine;
        private bool _isResponseMenuOpen;
        private bool _isUiHidden;
        private bool _subscribed;

        /// <summary>自动播放开关。默认关闭。</summary>
        public bool AutoPlay
        {
            get => _autoPlay;
            set
            {
                if (_autoPlay == value) return;
                _autoPlay = value;
                RefreshAdvanceWatcher();
                RaiseStateChanged();
            }
        }

        /// <summary>台词播放速度档位。只影响打字机。</summary>
        public EVnPlaybackSpeedTier SpeedTier
        {
            get => _speedTier;
            set
            {
                if (_speedTier == value) return;
                _speedTier = value;
                ApplyRate();
                RaiseStateChanged();
            }
        }

        /// <summary>快进状态。只读；用 <see cref="BeginFastForward"/> / <see cref="EndFastForward"/> 改。</summary>
        public EVnFastForwardState FastForwardState => _fastForwardState;

        /// <summary>快进是否正在生效（<see cref="EVnFastForwardState.Active"/>）。</summary>
        public bool IsFastForwarding => _fastForwardState == EVnFastForwardState.Active;

        /// <summary>新对话停止开关。默认开启。</summary>
        public bool StopOnUnread
        {
            get => _stopOnUnread;
            set
            {
                if (_stopOnUnread == value) return;
                _stopOnUnread = value;
                RaiseStateChanged();
            }
        }

        /// <summary>快进倍率。</summary>
        public float FastForwardRate
        {
            get => fastForwardRate;
            set { fastForwardRate = Mathf.Max(1f, value); ApplyRate(); }
        }

        /// <summary>自动播放的停留时长（秒）。</summary>
        public float AutoPlayDelay
        {
            get => autoPlayDelay;
            set => autoPlayDelay = Mathf.Max(0f, value);
        }

        /// <summary>任意状态变化时触发，供按钮刷新图标。</summary>
        public event Action StateChanged;

        /// <summary>快进被「新对话停止」打断时触发，供 UI 给个提示。</summary>
        public event Action FastForwardBlocked;

        #endregion

        #region 生命周期

        private void Awake()
        {
            _autoPlay = autoPlayDefaultOn;
            _speedTier = defaultSpeedTier;
            _stopOnUnread = stopOnUnreadDefaultOn;
            _fastForwardState = EVnFastForwardState.Off;
        }

        private void Start()
        {
            if (DialogueManager.hasInstance && DialogueManager.instance.gameObject != gameObject)
            {
                Debug.LogWarning("剧情演出 >> VnPlaybackController 没有挂在 Dialogue Manager 所在的物体上。" +
                                 "选项菜单的通知是 BroadcastMessage 发到那个物体的，挂在别处会收不到，" +
                                 "表现为「选项菜单弹出时快进不会自动停」。其余功能不受影响。");
            }

            Subscribe();
            ApplyRate();
        }

        private void OnEnable()
        {
            // Start 之前 DialogueManager 未必就绪，故订阅动作放在 Start；这里只补 OnDisable 之后的重订阅。
            if (_subscribed || !DialogueManager.hasInstance) return;
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
            StopAdvanceRoutine();
            // 组件被禁用时按住的快进必须收回：否则 IPointerUp 永远收不到，倍率会一直卡在 5。
            ForceStopFastForward();
        }

        private void OnDestroy() => Unsubscribe();

        private void Subscribe()
        {
            if (_subscribed || !DialogueManager.hasInstance) return;
            _subscribed = true;
            // 先减后加：静态单例上的事件，重复订阅会让一次通知触发多遍。
            DialogueManager.instance.preparingConversationLine -= OnPreparingConversationLine;
            DialogueManager.instance.preparingConversationLine += OnPreparingConversationLine;
            DialogueManager.instance.conversationLinePrepared -= OnConversationLinePrepared;
            DialogueManager.instance.conversationLinePrepared += OnConversationLinePrepared;
            DialogueManager.instance.conversationEnded -= OnConversationEndedEvent;
            DialogueManager.instance.conversationEnded += OnConversationEndedEvent;
        }

        private void Unsubscribe()
        {
            if (!_subscribed || !DialogueManager.hasInstance) return;
            _subscribed = false;
            DialogueManager.instance.preparingConversationLine -= OnPreparingConversationLine;
            DialogueManager.instance.conversationLinePrepared -= OnConversationLinePrepared;
            DialogueManager.instance.conversationEnded -= OnConversationEndedEvent;
        }

        #endregion

        #region 公开操作

        /// <summary>切换自动播放。可直接接到 Button.OnClick。</summary>
        public void ToggleAutoPlay() => AutoPlay = !_autoPlay;

        /// <summary>在 1x → 2x → 3x → 1x 之间循环。可直接接到 Button.OnClick。</summary>
        public void CycleSpeedTier()
        {
            switch (_speedTier)
            {
                case EVnPlaybackSpeedTier.X1: SpeedTier = EVnPlaybackSpeedTier.X2; break;
                case EVnPlaybackSpeedTier.X2: SpeedTier = EVnPlaybackSpeedTier.X3; break;
                default: SpeedTier = EVnPlaybackSpeedTier.X1; break;
            }
        }

        /// <summary>切换新对话停止。可直接接到 Button.OnClick。</summary>
        public void ToggleStopOnUnread() => StopOnUnread = !_stopOnUnread;

        /// <summary>
        /// 进入快进（按下时调用）。从 <see cref="EVnFastForwardState.Suppressed"/> 进入是允许的——
        /// 那正是「松开后重新按」的语义。
        /// </summary>
        public void BeginFastForward()
        {
            if (_fastForwardState == EVnFastForwardState.Active) return;
            SetFastForwardState(EVnFastForwardState.Active);
        }

        /// <summary>退出快进（松开时调用）。</summary>
        public void EndFastForward()
        {
            if (_fastForwardState == EVnFastForwardState.Off) return;
            SetFastForwardState(EVnFastForwardState.Off);
        }

        /// <summary>强制收回快进（对话结束、弹出菜单、组件禁用等）。</summary>
        public void ForceStopFastForward() => EndFastForward();

        #endregion

        #region 存档

        /// <summary>取当前配置的**深拷贝**。调用方持有并序列化它的期间，玩家继续改设置不会影响这份快照。</summary>
        public VnPlaybackSettingsData GetSettings() => new VnPlaybackSettingsData
        {
            AutoPlay = _autoPlay,
            AutoPlayDelay = autoPlayDelay,
            SpeedTier = (int)_speedTier,
            FastForwardRate = fastForwardRate,
            StopOnUnread = _stopOnUnread,
        };

        /// <summary>
        /// 按存档覆盖配置。<b>不触发 <see cref="StateChanged"/></b>——这是
        /// <c>ISaveable</c> 的契约：批量替换状态后由调用方自行刷新界面。
        /// 刷新用 <see cref="NotifyStateChanged"/>。
        ///
        /// <para>接受 <c>null</c> 与脏数据：档位越界会被夹回 1~3，时长与倍率会被夹到合法范围，
        /// 而不是抛异常——一份坏存档不该让整个演出系统起不来。</para>
        /// </summary>
        public void ApplySettings(VnPlaybackSettingsData data)
        {
            if (data == null) return;

            _autoPlay = data.AutoPlay;
            autoPlayDelay = Mathf.Max(0f, data.AutoPlayDelay);
            fastForwardRate = Mathf.Max(1f, data.FastForwardRate);
            _stopOnUnread = data.StopOnUnread;

            var tier = Mathf.Clamp(data.SpeedTier, 1, 3);
            _speedTier = (EVnPlaybackSpeedTier)tier;

            // 快进态不进存档：它是「按住」这个瞬时动作的产物，读档时必须回到未按住。
            _fastForwardState = EVnFastForwardState.Off;

            ApplyRate();
            RefreshAdvanceWatcher();
        }

        /// <summary>恢复到 Inspector 上配置的默认值（开新游戏）。同样不触发变更事件。</summary>
        public void ResetToDefaults()
        {
            _autoPlay = autoPlayDefaultOn;
            _speedTier = defaultSpeedTier;
            _stopOnUnread = stopOnUnreadDefaultOn;
            _fastForwardState = EVnFastForwardState.Off;

            ApplyRate();
            RefreshAdvanceWatcher();
        }

        /// <summary>主动广播一次状态变更，让按钮刷新图标。读档 / 重置之后由调用方调用。</summary>
        public void NotifyStateChanged() => RaiseStateChanged();

        /// <summary>
        /// 由 <see cref="VnUiHider"/> 告知界面已隐藏 / 已恢复。
        /// 隐藏期间暂停自动推进——玩家藏起 UI 是为了看画面，这时候把台词翻过去就白藏了。
        /// </summary>
        public void SetUiHidden(bool hidden)
        {
            if (_isUiHidden == hidden) return;
            _isUiHidden = hidden;
            if (hidden) ForceStopFastForward();
            RefreshAdvanceWatcher();
        }

        #endregion

        #region 倍率合成

        private void SetFastForwardState(EVnFastForwardState state)
        {
            if (_fastForwardState == state) return;
            _fastForwardState = state;

            ApplyRate();

            if (state == EVnFastForwardState.Active)
            {
                // 进入快进的第一件事是把当前行的打字机收掉。
                // 一是因为「快进」本就该立刻看到整行；二是 Dialogue System 的打字循环里
                // delay = 1 / charactersPerSecond **只在起播时算一次**，中途提速不会平滑加速，
                // 而是变成「每 delay 秒吐一大把字」的顿挫。收掉再推进，字速就永远只在
                // 「没有行正在打字」时被改写，跳字与顿挫都不会发生。
                StopTypewritersNow();
            }

            RefreshAdvanceWatcher();
            RaiseStateChanged();

            if (state == EVnFastForwardState.Suppressed) FastForwardBlocked?.Invoke();
        }

        /// <summary>
        /// 把当前状态合成为两条倍率推给 <see cref="VnStoryManager"/>。
        ///
        /// <para><b>快进与档位取 Max，不是相乘。</b>相乘会让 3 档速的玩家按下快进拿到 15 倍；
        /// 而纯覆盖又会在「快进倍率 2、档位 3」时反而变慢——按下快进结果更慢是明显的 bug。
        /// Max 保证单调不减，且默认值（5 > 3）下等价于覆盖。</para>
        ///
        /// <para>演出倍率<b>不含</b>档位：档位按需求只管台词打字机。</para>
        /// </summary>
        private void ApplyRate()
        {
            var manager = VnStoryManager.Instance;
            if (!manager) return;

            float tier = (int)_speedTier;
            bool ff = _fastForwardState == EVnFastForwardState.Active;

            float typewriter = ff ? Mathf.Max(tier, fastForwardRate) : tier;
            float playback = ff ? fastForwardRate : 1f;

            manager.SetPlaybackRate(playback, typewriter);
        }

        private void StopTypewritersNow()
        {
            var manager = VnStoryManager.Instance;
            if (manager) manager.StopAllTypewriters();
        }

        private void RaiseStateChanged()
        {
            try
            {
                StateChanged?.Invoke();
            }
            catch (Exception e)
            {
                // 一个按钮的刷新回调抛异常，不该让后面的按钮全部停在旧图标上。
                Debug.LogError("[VnPlayback] 状态变更回调抛出异常：" + e);
            }
        }

        #endregion

        #region Dialogue System 回调

        // 行准备阶段，**早于** SimStatus 被打标——这是全流程里唯一能读到「更新前已读状态」的时刻。
        private void OnPreparingConversationLine(DialogueEntry entry)
        {
            if (!_stopOnUnread) return;
            if (_fastForwardState != EVnFastForwardState.Active) return;

            var manager = VnStoryManager.Instance;
            if (!manager || !manager.ReadHistory.IsUnread(entry)) return;

            // 中止快进。注意是 Suppressed 而不是 Off：按钮多半还按着，
            // 置 Off 的话下一帧的按住状态会让它立刻恢复，表现为在每个未读行抖动式地一停一走。
            SetFastForwardState(EVnFastForwardState.Suppressed);
        }

        // 一行准备就绪、即将显示。注意此刻打字机**还没起播**，见 CorAutoAdvance 的宽限窗说明。
        private void OnConversationLinePrepared(Subtitle subtitle)
        {
            _isResponseMenuOpen = false;
            RefreshAdvanceWatcher();
        }

        private void OnConversationEndedEvent(Transform actor)
        {
            _isResponseMenuOpen = false;
            StopAdvanceRoutine();
            ForceStopFastForward();
        }

        /// <summary>
        /// 选项菜单弹出。由 Dialogue System 经 <c>BroadcastMessage</c> 调用，
        /// 故本组件必须与 Dialogue Manager 同物体（<see cref="Start"/> 里有检查）。
        /// </summary>
        [UnityEngine.Scripting.Preserve]
        public virtual void OnConversationResponseMenu(Response[] responses)
        {
            _isResponseMenuOpen = true;
            StopAdvanceRoutine();
            // 与 DS 自己的 stopSkipAllOnResponseMenu 默认行为对齐：选项要玩家自己选，不能快进过去。
            ForceStopFastForward();
        }

        #endregion

        #region 自动推进

        // 需要盯着本行、到点替玩家点继续吗？
        // 自动播放开着，或正在快进——快进必须隐含自动推进，否则一个还要逐句点击的「快进」不成立。
        private bool ShouldWatch => _autoPlay || _fastForwardState == EVnFastForwardState.Active;

        private void RefreshAdvanceWatcher()
        {
            StopAdvanceRoutine();
            if (!isActiveAndEnabled) return;
            if (!ShouldWatch) return;
            if (_isResponseMenuOpen) return;
            if (_isUiHidden) return;
            if (!DialogueManager.isConversationActive) return;

            _advanceRoutine = StartCoroutine(CorAutoAdvance());
        }

        private void StopAdvanceRoutine()
        {
            if (_advanceRoutine == null) return;
            StopCoroutine(_advanceRoutine);
            _advanceRoutine = null;
        }

        /// <summary>
        /// 盯着当前这一行，满足条件后替玩家点一次继续。
        ///
        /// <para><b>为什么不用 <c>OnConversationLineEnd</c> 当「本行播完」的信号。</b>
        /// DS 的 <c>FinishSubtitle()</c> 开头就是 <c>if (!waitForContinue)</c>，
        /// 而本工程 <c>continueButton = Always</c> 意味着每行 <c>waitForContinue</c> 恒为 true，
        /// 于是那个消息要等玩家点了继续才发——正好是我们要产生的动作，用它会成环。</para>
        ///
        /// <para><b>为什么不能直接 <c>while (isPlaying)</c>。</b>调用顺序是
        /// <c>NotifyParticipantsOnConversationLine</c>（本协程在此被拉起）→ <c>ShowSubtitle</c>
        /// → <c>SetContent</c> → <c>StartTyping</c>，<b>我们跑在打字机起播之前</b>，
        /// 此刻 <c>isPlaying</c> 还是 false，直接轮询会当场判定「已播完」而整行跳过。
        /// 故用「观察到在播」作闩，配合宽限窗兜底——没有打字机组件、本行文本为空、
        /// 或面板配了 <c>delayTypewriterUntilOpen</c> 都靠宽限窗退出。</para>
        /// </summary>
        private IEnumerator CorAutoAdvance()
        {
            var manager = VnStoryManager.Instance;
            if (!manager) yield break;

            // ① 等打字机起播并结束
            float graceEnd = Time.time + Mathf.Max(0f, typewriterStartGrace);
            bool seenPlaying = false;
            while (true)
            {
                bool playing = manager.IsAnyTypewriterPlaying();
                if (playing) seenPlaying = true;
                else if (seenPlaying || Time.time >= graceEnd) break;
                yield return null;
            }

            // ② 等语音播完。快进时不等：语音本就被压成一闪而过，等它反而把快进拖慢；
            //    后端没实现 IVnAudioPlaybackInfo 时 IsLineVoicePlaying 只剩「延迟未到期」一路判断，
            //    自动播放自然退化为「打字机结束 + 停留时长」。
            if (_fastForwardState != EVnFastForwardState.Active)
            {
                while (manager.IsLineVoicePlaying()) yield return null;
            }

            // ③ 停留计时。快进时按倍率压缩。
            //    用 Time.deltaTime（受 timeScale 影响）而不是 unscaled：与包内 19 处补间的
            //    unscaled:false 保持一致，宿主 timeScale = 0 开菜单时自动播放应当停住。
            float remain = _fastForwardState == EVnFastForwardState.Active
                ? autoPlayDelay / Mathf.Max(0.01f, VnPlaybackRate.Playback)
                : autoPlayDelay;
            while (remain > 0f)
            {
                if (!CanAdvanceNow(manager))
                {
                    // 门关着时冻结计时而不是清零：资源加载完之后接着走完剩下的停留时长即可。
                    yield return null;
                    continue;
                }
                remain -= Time.deltaTime;
                yield return null;
            }

            // ④ 末次复核后推进
            if (!CanAdvanceNow(manager)) { _advanceRoutine = null; yield break; }

            _advanceRoutine = null;
            var ui = DialogueManager.standardDialogueUI as AbstractDialogueUI;
            if (ui != null) ui.OnContinueConversation();
        }

        // 现在可以推进吗。任何一条不满足都只是「再等等」，不是错误。
        private bool CanAdvanceNow(VnStoryManager manager)
        {
            if (!ShouldWatch) return false;
            if (_isResponseMenuOpen) return false;
            if (_isUiHidden) return false;
            if (!DialogueManager.isConversationActive) return false;

            // DS 认定「本行已展示、正在等继续」的唯一权威信号。它同时也天然防重入：
            // OnContinueConversation 之后它立刻变 false。
            var view = DialogueManager.conversationView;
            if (view == null || !view.isWaitingForContinue) return false;

            // 演出资源还在加载时不能推进，否则会跳过尚未加载完的演出。
            if (manager.IsLoadingAssets) return false;

            return true;
        }

        #endregion
    }
}
