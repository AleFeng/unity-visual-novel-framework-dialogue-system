using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace PixelCrushers.DialogueSystem.VNStoryFramework
{
    /// <summary>
    /// 剧情演出 播放组件。
    /// 通过 VNStoryManager.Instance 启动/停止某段剧情对话的演出，
    /// 可在 Inspector 直接配置对话名与自动播放时机，也可被 Button.OnClick 或其他脚本调用触发。
    /// </summary>
    public class VNStoryPlayer : MonoBehaviour
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
            if (stopOnDisable && IsPlaying)
            {
                Stop();
            }

            // 退订 结束事件
            Unsubscribe();
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
                Debug.LogWarning("[VNStoryPlayer] Play >> 对话名称为空，无法播放剧情演出。请在组件上配置或通过代码传入。", this);
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
            if (VNStoryManager.Instance == false)
            {
                Debug.LogWarning("[VNStoryPlayer] Play >> 场景中不存在 VNStoryManager 实例，无法播放剧情演出。");
                return;
            }

            if (string.IsNullOrEmpty(conversationNamePlay))
            {
                Debug.LogWarning("[VNStoryPlayer] Play >> 对话名称为空，无法播放剧情演出。请在组件上配置或通过代码传入。", this);
                return;
            }

            // 记录本次播放的对话名称
            conversationName = conversationNamePlay;

            // 订阅 结束事件，用于驱动 IsPlaying 与 OnPlayEnded
            Subscribe();

            // 开始剧情对话
            VNStoryManager.Instance.StartVnStory(conversationNamePlay);

            IsPlaying = true;
            onPlayStarted.Invoke();
        }

        /// <summary>
        /// 停止当前正在播放的剧情演出。
        /// 仅停止由本组件播放的剧情：当前正在播放的对话名称需与 conversationName 相同。
        /// </summary>
        public void Stop()
        {
            if (VNStoryManager.Instance == false) return;

            // 仅停止 由本组件播放的剧情
            if (!IsPlaying) return;
            // 校验当前正在播放的对话是否与本组件相同
            if (!DialogueManager.IsConversationActive) return;
            if (DialogueManager.lastConversationStarted != conversationName) return;
            
            // VN故事系统淡出（UI/背景淡出）
            VNStoryManager.Instance.StopVnStory();
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

            IsPlaying = false;
            Unsubscribe();
            onPlayEnded.Invoke();
        }
        #endregion
    }
}