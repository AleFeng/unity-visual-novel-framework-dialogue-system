using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using PixelCrushers.DialogueSystem;

namespace Ale.VnFramework
{
    /// <summary>
    /// 剧情演出 播放组件。
    /// 通过 VnStoryManager.Instance 启动/停止某段剧情对话的演出，
    /// 可在 Inspector 直接配置对话名与自动播放时机，也可被 Button.OnClick 或其他脚本调用触发。
    ///
    /// <para>需要一段接一段播时用 <see cref="PlaySequence"/>，它把段间的时序细节
    /// （跨帧、收场方式、中途被打断）都在组件内部消化掉，调用方只管给一个名称列表。</para>
    /// </summary>
    public class VnStoryPlayer : MonoBehaviour
    {
        private void OnEnable()
        {
            // 自动播放 触发时机：在启用时
            if (autoPlayTiming == AutoPlayTiming.OnEnable)
            {
                ExecuteAutoPlay();
            }
        }

        private void Start()
        {
            // 自动播放 触发时机：在Start时
            if (autoPlayTiming == AutoPlayTiming.OnStart)
            {
                ExecuteAutoPlay();
            }
        }

        private void OnDisable()
        {
            // 自动取消 剧情演出
            if (_autoPlayRoutine != null)
            {
                StopCoroutine(_autoPlayRoutine);
                _autoPlayRoutine = null;
            }

            // 中止连播。组件停用时协程会被 Unity 一并停掉，队列留着也再无人推进；
            // 连播不跨启停，要续播由调用方重新登记。
            ClearSequence();

            if (stopOnDisable && IsPlaying)
            {
                Stop();
            }

            // 退订 结束事件
            Unsubscribe();

            // 退订之后就再也收不到「对话结束」，IsPlaying 若还挂着 true 便是一句永远不会被纠正的谎：
            // Play 开头那句「已在播放中则不重复播放」会把之后每一次播放请求都静默吞掉。
            // 宿主是 UI 界面时必踩——关一次界面就会 OnDisable，表现为剧情从此点不动、且毫无报错。
            // 这里只复位状态，不发 onPlayEnded：对象正在被停用，此刻广播「播完了」只会误导监听方。
            IsPlaying = false;
        }

        #region 播放与停止
        [Header("基本设置")]
        [Tooltip("剧情演出对话名称: 可在此配置，也可通过代码传入。")]
        [SerializeField] private string conversationName;
        
        /// <summary>
        /// 当前要播放的剧情对话名称。
        /// </summary>
        public string ConversationName
        {
            get => conversationName;
            set => conversationName = value;
        }
        
        /// <summary>
        /// 是否正在播放剧情演出。
        /// </summary>
        public bool IsPlaying { get; private set; }
        
        /// <summary>
        /// 使用当前配置的对话名称，开始播放剧情演出。
        /// 可绑定到 Button 的 OnClick 事件。
        /// </summary>
        public void Play()
        {
            if (string.IsNullOrEmpty(conversationName))
            {
                Debug.LogWarning("[VnStoryPlayer] Play >> 对话名称为空，无法播放剧情演出。请在组件上配置或通过代码传入。", this);
                return;
            }
            Play(conversationName);
        }

        /// <summary>
        /// 开始 剧情演出。
        /// </summary>
        /// <param name="conversationNamePlay">要播放的 剧情对话名称。</param>
        public void Play(string conversationNamePlay)
        {
            Play(conversationNamePlay, null);
        }

        /// <summary>
        /// 开始 剧情演出，并登记播放完成后的回调与收场方式。
        /// </summary>
        /// <param name="conversationNamePlay">要播放的 剧情对话名称。</param>
        /// <param name="onFinished">这段对话<b>播放完成后</b>的回调，直通
        /// <see cref="VnStoryManager.StartVnStory"/>：由 Manager 按「结束的正是这段」认领，
        /// 自然播完与中途停止都会触发。
        /// <para>与本组件的 <see cref="OnPlayEnded"/> 是两条路：后者由 Dialogue System 的
        /// <c>conversationEnded</c> 驱动，面向 Inspector 配置；前者面向调用方按次传入。</para></param>
        /// <param name="autoStopOnFinished">播放完成后是否自动 <c>StopVnStory</c>（淡出 UI/背景），
        /// 默认 <c>true</c>。传 <c>false</c> 用于一段接一段连播，由调用方决定何时收场。</param>
        /// <param name="skipStartEntryConditions">是否跳过入口节点的条件判定，默认 <c>false</c>。
        /// 详见 <see cref="VnStoryManager.StartVnStory"/> 的同名参数——用于「准入已由外部判定」的场景
        /// （如剧情回顾），避免会话首节点的条件把整段剧情静默拦下。</param>
        public void Play(string conversationNamePlay, Action onFinished, bool autoStopOnFinished = true,
            bool skipStartEntryConditions = false)
        {
            // 单段播放是一次全新的播放请求：先中止可能还排着队的连播，
            // 否则排在后面的那一段会在一帧后把这次请求顶掉（见 PlaySequence 的跨帧串联）。
            ClearSequence();

            PlayInternal(conversationNamePlay, onFinished, autoStopOnFinished, skipStartEntryConditions);
        }

        /// <summary>
        /// 开始 剧情演出。<b>不触碰连播队列</b>——连播的每一段都经由这里，
        /// 公开的 <see cref="Play(string, Action, bool, bool)"/> 则会先中止在排的队。
        /// </summary>
        private void PlayInternal(string conversationNamePlay, Action onFinished, bool autoStopOnFinished,
            bool skipStartEntryConditions)
        {
            if (VnStoryManager.Instance == false)
            {
                Debug.LogWarning("[VnStoryPlayer] Play >> 场景中不存在 VnStoryManager 实例，无法播放剧情演出。");
                return;
            }

            if (string.IsNullOrEmpty(conversationNamePlay))
            {
                Debug.LogWarning("[VnStoryPlayer] Play >> 对话名称为空，无法播放剧情演出。请在组件上配置或通过代码传入。", this);
                return;
            }

            // 如果已经在播放中，则不重复播放
            if (IsPlaying) return;

            // 记录本次播放的对话名称
            conversationName = conversationNamePlay;

            // 订阅 结束事件，用于驱动 IsPlaying 与 OnPlayEnded
            Subscribe();

            // 开始剧情对话
            VnStoryManager.Instance.StartVnStory(conversationNamePlay, onFinished, autoStopOnFinished,
                skipStartEntryConditions);

            IsPlaying = true;
            onPlayStarted.Invoke();
        }

        /// <summary>
        /// 停止当前正在播放的剧情演出。
        /// 仅停止由本组件播放的剧情：当前正在播放的对话名称需与 conversationName 相同。
        /// </summary>
        public void Stop()
        {
            // 中止连播。Stop 的语义是「别再播了」，与此刻是否正巧夹在两段之间无关：
            // 段间那一帧里 IsPlaying 已被复位、对话也已结束，下面的守卫会全部早退，
            // 不在这里清队列的话，下一段会在一帧后凭空开播。
            ClearSequence();

            // ToolkitMonoSingleton 的 Instance 在退出播放时不会转为 null，而 OnDisable 会在退出时
            // 触发本方法 → StopVnStory() → 各种淡出，落到正在被拆掉的对象上。故先看退出标记。
            if (VnStoryManager.IsQuitting || VnStoryManager.Instance == false) return;

            // 仅停止 由本组件播放的剧情
            if (!IsPlaying) return;
            // 校验当前正在播放的对话是否与本组件相同
            if (!DialogueManager.IsConversationActive) return;
            if (DialogueManager.lastConversationStarted != conversationName) return;
            
            // Vn故事系统淡出（UI/背景淡出）
            VnStoryManager.Instance.StopVnStory();
        }
        #endregion

        #region 连播
        // Des：
        // 一段接一段地播。两个坑都在这里消化掉，调用方不必知道：
        // 1) 段与段之间必须跨一帧。某段的完成回调跑在 Dialogue System 的
        //    ConversationController.Close() 广播里，此时 IsPlaying 还没被 conversationEnded
        //    事件复位，直接播下一段会被 Play 开头那句「已在播放中则不重复播放」静默吞掉；
        //    在 DS 的收尾调用栈里重入开新对话本身也不稳。等一帧两件事一起解决。
        // 2) 完成回调「自然播完」与「中途被停止」都会触发，框架不区分。所以中止只能由
        //    Stop / OnDisable 主动清队列来表达，否则玩家一返回，下一段照样在一帧后开播。

        // 待播的各段。非空即表示正在连播。
        private readonly List<string> _sequence = new List<string>();
        // 当前播到第几段。
        private int _sequenceIndex;
        // 整队播完后的回调，由调用方传入，兑现一次。
        private Action _sequenceOnFinished;
        // 整队的收场方式，只作用于最后一段。
        private bool _sequenceAutoStopOnFinished;
        private bool _sequenceSkipStartEntryConditions;
        // 跨帧播下一段的协程。
        private Coroutine _sequenceNextRoutine;

        /// <summary>
        /// 是否正在连播（<see cref="PlaySequence"/> 启动的多段播放尚未走完）。
        /// </summary>
        public bool IsPlayingSequence => _sequence.Count > 0;

        /// <summary>
        /// 按顺序连播多段剧情演出。
        /// </summary>
        /// <param name="conversationNamePlays">要依次播放的 剧情对话名称。空项会被跳过；
        /// 只有一段时等价于 <see cref="Play(string, Action, bool, bool)"/>。</param>
        /// <param name="onFinished"><b>整队</b>结束后的回调，只兑现一次。
        /// <para>⚠️ 与单段一致：自然播完与中途停止（如宿主界面被关闭）<b>都会</b>触发，框架不区分这两者。</para></param>
        /// <param name="autoStopOnFinished">整队播完后是否自动 <c>StopVnStory</c>（淡出 UI/背景），默认 <c>true</c>。
        /// <para>只作用于最后一段；前面各段一律不收场，好让 UI 与背景留在场上等下一段接手。
        /// 于是「连播一队」与「播一段」的收尾表现完全一致。</para></param>
        /// <param name="skipStartEntryConditions">是否跳过入口节点的条件判定，默认 <c>false</c>。
        /// 对队列里的每一段都生效，含义见 <see cref="VnStoryManager.StartVnStory"/> 的同名参数。</param>
        public void PlaySequence(IList<string> conversationNamePlays, Action onFinished = null,
            bool autoStopOnFinished = true, bool skipStartEntryConditions = false)
        {
            // 先清掉可能还在排的上一队，再收集本次的有效段。
            // 顺手滤掉空项：调用方常常从配置表取列表，中间留空行是常态。
            ClearSequence();
            if (conversationNamePlays != null)
            {
                for (int i = 0; i < conversationNamePlays.Count; i++)
                {
                    if (!string.IsNullOrEmpty(conversationNamePlays[i]))
                        _sequence.Add(conversationNamePlays[i]);
                }
            }

            if (_sequence.Count == 0)
            {
                Debug.LogWarning("[VnStoryPlayer] PlaySequence >> 没有有效的对话名称，无法播放剧情演出。", this);
                return;
            }

            // 单段没有「连」可言，走普通播放，不留下任何队列状态。
            if (_sequence.Count == 1)
            {
                string only = _sequence[0];
                _sequence.Clear();
                Play(only, onFinished, autoStopOnFinished, skipStartEntryConditions);
                return;
            }

            _sequenceIndex = 0;
            _sequenceOnFinished = onFinished;
            _sequenceAutoStopOnFinished = autoStopOnFinished;
            _sequenceSkipStartEntryConditions = skipStartEntryConditions;

            PlaySequenceCurrent();
        }

        /// <summary>
        /// 播放队列中当前这一段。
        /// </summary>
        private void PlaySequenceCurrent()
        {
            bool isLast = _sequenceIndex >= _sequence.Count - 1;

            // 走 PlayInternal 而不是 Play：后者会把队列当成「上一队」清掉。
            PlayInternal(_sequence[_sequenceIndex], OnSequenceSegmentFinished,
                isLast && _sequenceAutoStopOnFinished, _sequenceSkipStartEntryConditions);

            // Play 有若干早退分支（管理器缺席、名称为空、已在播放中…）。没播起来就别把队列挂在那，
            // 否则它永远等不到完成回调，IsPlayingSequence 会一直是 true。
            if (!IsPlaying) ClearSequence();
        }

        /// <summary>
        /// 队列中某一段播放完成（自然播完或中途停止）。
        /// </summary>
        private void OnSequenceSegmentFinished()
        {
            // 队列已被 Stop / OnDisable 清空 → 整队作废。调用方的回调仍要兑现一次，
            // 与单段「停止也触发」的语义保持一致。
            if (_sequence.Count == 0)
            {
                InvokeSequenceFinished();
                return;
            }

            _sequenceIndex++;
            if (_sequenceIndex >= _sequence.Count)
            {
                // 最后一段的收场已经交给 StartVnStory 了，这里只负责收状态与回调。
                ClearSequence();
                InvokeSequenceFinished();
                return;
            }

            if (_sequenceNextRoutine != null) StopCoroutine(_sequenceNextRoutine);
            _sequenceNextRoutine = StartCoroutine(PlayNextSequenceRoutine());
        }

        /// <summary>
        /// 等一帧再播下一段。理由见本 region 顶部的说明。
        /// </summary>
        private IEnumerator PlayNextSequenceRoutine()
        {
            yield return null;
            _sequenceNextRoutine = null;

            // 这一帧里可能已被 Stop / OnDisable 中止。
            if (_sequence.Count == 0) yield break;

            PlaySequenceCurrent();
        }

        /// <summary>
        /// 兑现整队完成回调。先取出再清空最后才调用：回调里若又登记了新的一队，不会被这次清空抹掉。
        /// </summary>
        private void InvokeSequenceFinished()
        {
            Action onFinished = _sequenceOnFinished;
            _sequenceOnFinished = null;
            onFinished?.Invoke();
        }

        /// <summary>
        /// 中止并清空连播队列。不动 <see cref="_sequenceOnFinished"/>——那是
        /// <see cref="InvokeSequenceFinished"/> 的职责，中止时回调仍需兑现。
        /// </summary>
        private void ClearSequence()
        {
            _sequence.Clear();
            _sequenceIndex = 0;
            if (_sequenceNextRoutine != null)
            {
                StopCoroutine(_sequenceNextRoutine);
                _sequenceNextRoutine = null;
            }
        }
        #endregion
        
        #region 自动播放
        [Header("自动播放")]
        [Tooltip("自动播放 类型: Manual-手动触发。OnStart-在Start时自动播放。OnEnable-在OnEnable时自动播放。")]
        [SerializeField] private AutoPlayTiming autoPlayTiming = AutoPlayTiming.Manual;
        [Tooltip("自动播放 延迟时间（秒）: 0 表示立即播放。")]
        [SerializeField] private float autoPlayDelay;
        [Tooltip("自动停止 组件被禁用或销毁时")]
        [SerializeField] private bool stopOnDisable = true;
        
        // 协程 延迟自动播放
        private Coroutine _autoPlayRoutine;
        
        /// <summary>
        /// 自动播放 触发时机。
        /// </summary>
        public enum AutoPlayTiming
        {
            /// <summary>手动触发。</summary>
            Manual,
            /// <summary>在Start时自动播放。</summary>
            OnStart,
            /// <summary>在OnEnable时自动播放。</summary>
            OnEnable,
        }
        
        /// <summary>
        /// 执行 自动播放。
        /// </summary>
        private void ExecuteAutoPlay()
        {
            if (_autoPlayRoutine != null)
            {
                StopCoroutine(_autoPlayRoutine);
            }
            _autoPlayRoutine = StartCoroutine(AutoPlayRoutine());
        }
        
        /// <summary>
        /// 协程 延迟自动播放。
        /// </summary>
        /// <returns></returns>
        private IEnumerator AutoPlayRoutine()
        {
            if (autoPlayDelay > 0f)
            {
                yield return new WaitForSeconds(autoPlayDelay);
            }
            _autoPlayRoutine = null;
            Play();
        }
        #endregion
        
        #region 播放事件
        [Header("播放事件")]
        [Tooltip("事件 剧情演出 开始时触发。")]
        [SerializeField] private UnityEvent onPlayStarted = new UnityEvent();
        [Tooltip("事件 剧情演出 结束时触发。")]
        [SerializeField] private UnityEvent onPlayEnded = new UnityEvent();
        
        /// <summary>
        /// 当剧情演出开始播放时触发。
        /// </summary>
        public UnityEvent OnPlayStarted => onPlayStarted;
        /// <summary>
        /// 当剧情演出结束时触发。
        /// </summary>
        public UnityEvent OnPlayEnded => onPlayEnded;
        
        // 是否 已订阅 DialogueManager播放事件
        private bool _isSubscribed;
        
        /// <summary>
        /// 订阅 剧情对话结束事件。
        /// </summary>
        private void Subscribe()
        {
            if (_isSubscribed) return;
            if (!DialogueManager.instance) return;

            DialogueManager.instance.conversationEnded += OnConversationEnded;
            _isSubscribed = true;
        }

        /// <summary>
        /// 退订 剧情对话结束事件。
        /// </summary>
        private void Unsubscribe()
        {
            if (!_isSubscribed) return;
            if (DialogueManager.instance)
            {
                DialogueManager.instance.conversationEnded -= OnConversationEnded;
            }
            _isSubscribed = false;
        }
        
        /// <summary>
        /// 当 剧情对话 结束。
        /// </summary>
        /// <param name="actor"></param>
        private void OnConversationEnded(Transform actor)
        {
            if (!IsPlaying) return;

            // 校验是不是本组件那段剧情结束了。DialogueManager.conversationEnded 对**任何**对话都触发，
            // 包括别的播放器启动的、以及嵌套 / 联动的对话；不校验的话，别人结束会误清本组件的 IsPlaying
            // 并误发 onPlayEnded。Stop() 一直是这么校验的，这里补齐。
            if (!string.IsNullOrEmpty(conversationName) &&
                DialogueManager.lastConversationStarted != conversationName) return;

            IsPlaying = false;
            Unsubscribe();
            onPlayEnded.Invoke();
        }
        #endregion
    }
}