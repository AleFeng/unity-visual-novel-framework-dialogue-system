using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Fs.GameFramework.Common.AudioSystem;
using Fs.GameFramework.Common.AssetSystem;
using Fs.Utility.Singleton;

#if HAS_LOCALIZATION
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
#endif

#if DOTWEEN
using DG.Tweening;
#endif

namespace PixelCrushers.DialogueSystem.VNStoryFramework
{
    /// <summary>
    /// 角色预制体加载数据。
    /// </summary>
    struct FActorPrefabLoadData
    {
        /// <summary>
        /// 角色预制体地址。
        /// </summary>
        public string AssetAddress;
        /// <summary>
        /// 角色预制体 资产。
        /// </summary>
        public GameObject PrefabAsset;
        /// <summary>
        /// 角色预制体 实例。
        /// </summary>
        public GameObject PrefabInstance;
        /// <summary>
        /// 角色动画组件。
        /// </summary>
        public VNActorAnimator ActorAnimator;
    }
    
    /// <summary>
    /// VN故事系统 管理器组件。
    /// </summary>
    public class VNStoryManager : MonoBehaviourSingleton<VNStoryManager>
    {
        protected override void Awake()
        {
            base.Awake();
            
            // 注册Lua函数-背景淡入淡出时间
            Lua.RegisterFunction("BackgroundFadeDuration", this, SymbolExtensions.GetMethodInfo(() => BackgroundFadeDuration(0)));
            // 注册持久化数据
            PersistentDataManager.RegisterPersistentData(gameObject);
            
            // 初始化 UI设置
            InitUI();
            // 初始化 背景
            InitBackground();
            
#if HAS_LOCALIZATION
            // 初始化 多语言
            AwakeLocalization();
#endif
            // 初始化 对话
            AwakeDialogue();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            
            // 注销持久化数据
            PersistentDataManager.UnregisterPersistentData(gameObject);
            
#if HAS_LOCALIZATION
            // 销毁 多语言
            OnDestroyLocalization();
#endif
        }
        
        #region 流程控制
        private bool _isVnStoryStarted; // VN故事系统 是否已经开始
        
        /// <summary>
        /// 开始 VN故事系统
        /// </summary>
        public void StartVnStory(string conversationName = null)
        {
            if (_isVnStoryStarted) return;
            _isVnStoryStarted = true;
            
            // 淡入 UI
            FadeInUI();
            // 淡入 背景;
            FadeInBackground();
            // 淡入 角色、特效
            FadeInActorsAndEffects();
            
            // 开始 指定的对话演出
            if (!string.IsNullOrEmpty(conversationName))
                StartStoryConversation(conversationName);
        }
        
        /// <summary>
        /// 停止 VN故事系统
        /// </summary>
        /// <param name="clearAllData">清除 所有数据</param>
        public void StopVnStory(bool clearAllData = true)
        {
            if (!_isVnStoryStarted) return;
            _isVnStoryStarted = false;
            
            // 淡出 UI
            FadeOutUI(() =>
            {
                if (clearAllData)
                {
                    // 停止 当前的对话演出。
                    StopStoryConversation();
                }
            });
            // 淡出 背景
            FadeOutBackground();
            // 淡出 角色、特效
            FadeOutActorsAndEffects();
        }
        
        /// <summary>
        /// 开始 对话。
        /// </summary>
        /// <param name="conversationName"></param>
        private void StartStoryConversation(string conversationName)
        {
            if (string.IsNullOrEmpty(conversationName)) return;
            
            // 停止 正在进行的对话
            // StopStoryConversation();
            // 同步 所有外部变量值到Dialogue System。
            SetAllVariablesToDialogueSystem();
            
            // 开始新的对话
            DialogueManager.StartConversation(conversationName);
        }
        
        /// <summary>
        /// 停止 当前对话。
        /// </summary>
        private void StopStoryConversation()
        {
            // 停止当前对话
            DialogueManager.StopConversation();
        }
        
        /// <summary>
        /// 获取所有 对话名称 列表。
        /// </summary>
        /// <returns></returns>
        public string[] GetAllConversationName()
        {
            // 获取所有对话名称列表
            var conversationNames = DialogueManager.MasterDatabase.conversations.ConvertAll(c => c.Title).ToArray();
            
            return conversationNames;
        }

        /// <summary>
        /// 当 对话 开始。
        /// </summary>
        /// <param name="actor"></param>
        private void OnConversationStart(Transform actor)
        {
            // TODO: 暂停当前BGM
        }
        
        /// <summary>
        /// 当 对话 结束。
        /// </summary>
        /// <param name="actor"></param>
        private void OnConversationEnd(Transform actor)
        {
            // 清除所有 对话 头像图像
            ClearAllDialogueHeadImage();
            // 清空并隐藏 背景图像组件
            ClearAllBackground();
            // 清除所有 角色 预制体。场景特效 也会全部清除。
            ClearAllActors();
            // 清除所有 背景音乐
            ClearAllBGM();
            // 清除所有 音效
            ClearAllSfx();

            // TODO: 恢复之前的BGM
        }
        
        /// <summary>
        /// 当 对话行 开始。
        /// </summary>
        /// <param name="subtitle"></param>
        private void OnConversationLine(Subtitle subtitle)
        {
            if (subtitle == null) return;
            
            // 处理 对话变化
            OnConversationLineDialogueChange(subtitle);
            // 处理 背景变化
            OnOnConversationLineBackgroundChange(subtitle);
            // 处理 角色变化
            OnConversationLineActorChange(subtitle);
            // 处理 特效变化
            OnConversationLineEffectChange(subtitle);
            // 处理 音频变化
            OnConversationLineAudioChange(subtitle);
            // 处理 玩法系统
            OnConversationLineGamePlaySystem(subtitle);
        }
        
        /// <summary>
        /// 当 对话行 结束。
        /// </summary>
        /// <param name="subtitle"></param>
        private void OnConversationLineEnd(Subtitle subtitle)
        {
            
        }
        #endregion
        
        #region UI设置
        [Header("UI设置")]
        [Tooltip("UI Canvas 组件")]
        [SerializeField] private Canvas uiCanvas;
        [Tooltip("UI CanvasGroup 组件（用于控制 UI淡入淡出）")]
        [SerializeField] private CanvasGroup uiCanvasGroup;

        // 当前是否正在淡入UI。用于控制在拖拽过程中 不切换动画动作播放器时，保持UI状态不变。
        private bool _isUiFadeIn;
        
        /// <summary>
        /// 初始化 UI设置
        /// </summary>
        private void InitUI()
        {
            // 初始化 UI设置
            // UI画布组 初始化非激活。等待其他系统 调用打开。
            if (uiCanvasGroup)
            {
                uiCanvasGroup.alpha = 0f;
                uiCanvasGroup.interactable = false; // 不可交互
                uiCanvasGroup.blocksRaycasts = false; // 不接收射线
            }
            _isUiFadeIn = false;
        }
        
        /// <summary>
        /// 淡入 UI
        /// </summary>
        private void FadeInUI()
        {
            if (_isUiFadeIn) return; // 已经是淡入状态则不重复执行
            _isUiFadeIn = true;
            
            // 淡入 UI
            if (uiCanvasGroup)
            {
#if DOTWEEN
                // DoTween 淡入动画
                uiCanvasGroup.DOFade(1f, 0.5f).OnComplete(() =>
                {
                    // uiCanvasGroup可用
                    uiCanvasGroup.interactable = true; // 可交互
                    uiCanvasGroup.blocksRaycasts = true; // 接收射线
                });
#else
                // 直接设置为完全不透明
                uiCanvasGroup.alpha = 1f;
#endif
            }
            else if (uiCanvas)
            {
                uiCanvas.gameObject.SetActive(true);
            }
        }
        
        /// <summary>
        /// 淡出 UI
        /// </summary>
        /// <param name="onComplete"></param>
        private void FadeOutUI(Action onComplete)
        {
            if (_isUiFadeIn == false) return; // 已经是淡出状态则不重复执行
            _isUiFadeIn = false;
            
            // 淡出 UI
            if (uiCanvasGroup)
            {
#if DOTWEEN
                // DialogueSystem需要uiCanvas保持激活状态，因此 不进行非激活设置。
                // uiCanvasGroup不可用
                uiCanvasGroup.interactable = false; // 不可交互
                uiCanvasGroup.blocksRaycasts = false; // 不接收射线
                // DoTween 淡出动画
                uiCanvasGroup.DOFade(0f, 0.5f).OnComplete(() =>
                {
                    // 完成回调
                    onComplete?.Invoke();
                });
#else
                // 直接设置为完全透明，并非激活 UI    
                uiCanvasGroup.alpha = 0f;
                // 完成回调
                onComplete?.Invoke();
#endif
            }
            else if (uiCanvas)
            {
                // uiCanvasGroup不可用
                uiCanvasGroup.interactable = false; // 不可交互
                uiCanvasGroup.blocksRaycasts = false; // 不接收射线
                // 完成回调
                onComplete?.Invoke();
            }
        }
        #endregion
        
        #region 资源加载与卸载
        // 加载中的资源 计数器。
        private readonly Dictionary<string, int> _dicLoadingAssetCounter = new Dictionary<string, int>();
        // 已加载的资源 计数器。
        private readonly Dictionary<string, int> _dicLoadedAssetCounter = new Dictionary<string, int>();
        // 已加载的资源表。
        private readonly Dictionary<string, UnityEngine.Object> _dicLoadedAssets = new Dictionary<string, UnityEngine.Object>();
        // 加载中的资源 请求卸载的计数器（加载完成后应立即卸载的次数）。
        private readonly Dictionary<string, int> _dicPendingUnloadAfterLoad = new Dictionary<string, int>();
        
        /// <summary>
        /// 加载资源。
        /// </summary>
        /// <param name="assetAddress"></param>
        /// <param name="onAssetLoaded"></param>
        /// <typeparam name="T"></typeparam>
        private void LoadAsset<T>(string assetAddress, Action<T> onAssetLoaded) where T : UnityEngine.Object
        {
            // 检查 资源是否已经加载
            if (_dicLoadedAssets.TryGetValue(assetAddress, out var loadedAsset))
            {
                // 增加 已加载资源计数器
                _dicLoadedAssetCounter[assetAddress] = _dicLoadedAssetCounter.GetValueOrDefault(assetAddress, 0) + 1;
                // 资源已经加载，直接调用回调
                onAssetLoaded?.Invoke(loadedAsset as T);
                return;
            }

            // 增加 资源加载计数器
            _dicLoadingAssetCounter[assetAddress] = _dicLoadingAssetCounter.GetValueOrDefault(assetAddress, 0) + 1;
            // 加载中，禁止跳过对话
            SetContinueButtonActive(false);
            Action<T> callback = (asset) =>
            {
                // 减少 加载中的资源的计数器
                var remainingLoading = _dicLoadingAssetCounter.GetValueOrDefault(assetAddress, 1) - 1;
                if (remainingLoading <= 0)
                    _dicLoadingAssetCounter.Remove(assetAddress);
                else
                    _dicLoadingAssetCounter[assetAddress] = remainingLoading;
                // 恢复允许跳过对话
                if (_dicLoadingAssetCounter.Count == 0)
                {
                    SetContinueButtonActive(true);
                }
                
                // 如果在加载过程中，有人请求卸载（pending unload），则立即卸载并不要把资源记录为已加载，也不要调用回调。
                var pendingCount = _dicPendingUnloadAfterLoad.GetValueOrDefault(assetAddress, 0);
                if (pendingCount > 0)
                {
                    // 消耗一个 pending 卸载请求
                    pendingCount -= 1;
                    if (pendingCount <= 0) _dicPendingUnloadAfterLoad.Remove(assetAddress);
                    else _dicPendingUnloadAfterLoad[assetAddress] = pendingCount;
                    // 卸载资源
                    AssetManager.Instance.UnloadAsset(assetAddress);

                    return;
                }

                // 增加 已加载资源计数器
                _dicLoadedAssetCounter[assetAddress] = _dicLoadedAssetCounter.GetValueOrDefault(assetAddress, 0) + 1;
                // 记录已加载的资源
                _dicLoadedAssets[assetAddress] = asset;
                
                // 调用原始回调
                onAssetLoaded?.Invoke(asset);
            };
            
            AssetManager.Instance.LoadAsset(assetAddress, callback);
        }
        
        /// <summary>
        /// 卸载资源。
        /// </summary>
        /// <param name="assetAddress"></param>
        private void UnloadAsset(string assetAddress)
        {
            if (string.IsNullOrEmpty(assetAddress)) return;
            
            // 检查 是否 已加载资源
            if (_dicLoadedAssets.TryGetValue(assetAddress, out var loadedAsset))
            {
                // 减少 已加载资源计数器
                _dicLoadedAssetCounter[assetAddress] = _dicLoadedAssetCounter.GetValueOrDefault(assetAddress, 1) - 1;
                if (_dicLoadedAssetCounter[assetAddress] <= 0)
                {
                    _dicLoadedAssetCounter.Remove(assetAddress);
                    // 卸载资源
                    AssetManager.Instance.UnloadAsset(assetAddress);
                    // 从已加载资源表中移除
                    _dicLoadedAssets.Remove(assetAddress);
                }
            }
            // 检查 是否 正在加载资源
            else if (_dicLoadingAssetCounter.TryGetValue(assetAddress, out var loadingCount))
            {
                // 减少 正在加载资源计数器（安全处理，防止出现负数）
                var newCount = Math.Max(0, loadingCount - 1);
                if (newCount <= 0)
                {
                    _dicLoadingAssetCounter.Remove(assetAddress);
                    // 标记为 pending 卸载：加载完成后应该立即卸载
                    _dicPendingUnloadAfterLoad[assetAddress] = _dicPendingUnloadAfterLoad.GetValueOrDefault(assetAddress, 0) + 1;
                }
                else
                {
                    _dicLoadingAssetCounter[assetAddress] = newCount;
                }
            }
        }
        #endregion
        
        #region 对话
        [Header("对话")]
        [Tooltip("对话-选项按钮。全局变量-是否已读-变量名称。会从 这个全局变量，获取 是否已读的状态，来设置 选项按钮的 UI显示状态。")]
        [SerializeField] private string conversationResponseButtonVariableIsReadFieldTitle = "ConversationResponseButtonVariableIsRead";
        
        /// <summary>
        /// 对话-选项按钮。全局变量-是否已读-变量名称。会从 这个全局变量，获取 是否已读的状态，来设置 选项按钮的 UI显示状态。
        /// </summary>
        public string ConversationResponseButtonVariableIsReadFieldTitle
        {
            get => conversationResponseButtonVariableIsReadFieldTitle;
        }
        
        /// <summary>
        /// 初始化 对话
        /// </summary>
        private void AwakeDialogue()
        {
            // 获取 StandardDialogueUI
            var dialogueUI = DialogueManager.dialogueUI as StandardDialogueUI;
            if (dialogueUI && dialogueUI.conversationUIElements != null)
            {
                var panels = dialogueUI.conversationUIElements.subtitlePanels;
                if (panels != null)
                {
                    foreach (var panel in panels)
                    {
                        if (!panel) continue;
                        // 获取 打字机组件
                        if (panel.subtitleText != null && panel.subtitleText.gameObject)
                        {
                            var typewriter = panel.subtitleText.gameObject.GetComponent<TextMeshProTypewriterEffect>();
                            if (typewriter)
                                _dialogueTypewriters.Add(typewriter);
                        }
                        else
                        {
                            Debug.LogWarning($"剧情演出 >> 对话UI的字幕文本组件未找到 TextMeshProTypewriterEffect 组件，无法设置打字机速度。请确保在对话UI的字幕文本组件上添加 TextMeshProTypewriterEffect 组件。");
                        }
                        // 获取 继续按钮
                        if (panel.continueButton)
                        {
                            _dialogueContinueButtons.Add(panel.continueButton);
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// 对话行 对话变化
        /// </summary>
        /// <param name="subtitle"></param>
        private void OnConversationLineDialogueChange(Subtitle subtitle)
        {
            // 打字机 速度倍率
            var dialogueTypewriterSpeed = Field.LookupValue(subtitle.dialogueEntry.fields, dialogueTypewriterSpeedFieldTitle);
            if (!string.IsNullOrEmpty(dialogueTypewriterSpeed))
            {
                if (float.TryParse(dialogueTypewriterSpeed, out var speed) && speed > 0f)
                {
                    // 设置 打字机速度
                    SetAllTypewritersSpeed(speed);
                }
            }
            else
            {
                // 恢复默认 打字机速度
                SetAllTypewritersSpeed();
            }
            
            // 对话框 头像
            var dialogueHead = Field.LookupValue(subtitle.dialogueEntry.fields, dialogueHeadFieldTitle);
            if (dialogueHead == "")
            {
                // 设置 为空头像
                SetDialogueHeadImage(null, subtitle.speakerInfo.nameInDatabase);
            }
            else if (!string.IsNullOrEmpty(dialogueHead))
            {
                // 设置 头像图像，传入当前说话者的数据库名称
                SetDialogueHeadImage(dialogueHead, subtitle.speakerInfo.nameInDatabase);
            }
        }

        #region 继续按钮
        // 继续按钮组件列表
        private readonly List<Button> _dialogueContinueButtons = new List<Button>();
        // 继续按钮 激活状态
        private bool _continueButtonisActive = true;
        
        /// <summary>
        /// 设置 继续按钮 激活状态
        /// </summary>
        /// <param name="active"></param>
        private void SetContinueButtonActive(bool active)
        {
            // 状态未变化时，直接返回
            if (_continueButtonisActive == active) return;
            _continueButtonisActive = active;
            
            foreach (var btn in _dialogueContinueButtons)
            {
                btn.gameObject.SetActive(active);
            }
        }
        #endregion
        
        #region 打字机
        [Header("对话-打字机")]
        [Tooltip("对话-打字机 速度 字段标题。默认为1.0倍速")]
        [SerializeField] private string dialogueTypewriterSpeedFieldTitle = "DialogueTypewriterSpeed";
        [Tooltip("对话-打字机 字符数/秒（默认10）")]
        [SerializeField] private float dialogueTypewriterPerSecond = 10f;
        
        // 如果你只需要快速访问所有的打字机组件
        private readonly List<TextMeshProTypewriterEffect> _dialogueTypewriters = new List<TextMeshProTypewriterEffect>();

        /// <summary>
        /// 设置 打字机的速度
        /// </summary>
        /// <param name="speed">速度倍率。默认为 1.0</param>
        private void SetAllTypewritersSpeed(float speed = 1.0f)
        {
            foreach (var tw in _dialogueTypewriters)
            {
                tw.charactersPerSecond = speed * dialogueTypewriterPerSecond;
            }
        }
        #endregion

        #region 头像切换
        [Header("对话-头像切换")]
#if USE_ADDRESSABLES
        [Tooltip("对话-头像切换 文件夹路径")]
        [SerializeField] private string dialogueHeadAddressableFolder = "Assets/Plugins/Pixel Crushers/VNStoryManager/Assets/ActorsHead/";
        [Tooltip("对话-头像切换 扩展名。通常为PNG格式")]
        [SerializeField] private string dialogueHeadExtension = ".png";
#endif
        [Tooltip("对话-头像切换 字段标题")]
        [SerializeField] private string dialogueHeadFieldTitle = "DialogueHead";

        // 当前对话头像资源地址（可能包含路径/扩展名）
        private string _dialogueHeadAssetName;
        // 上一个对话头像资源地址（用于卸载）
        private string _dialogueHeadAssetNameLast;
        
        /// <summary>
        /// 设置 对话 头像图像
        /// </summary>
        /// <param name="headImageName"></param>
        /// <param name="actorName">Actor database name (used for SetActorPortraitSprite)</param>
        private void SetDialogueHeadImage(string headImageName, string actorName)
        {
            if (string.IsNullOrEmpty(actorName))
            {
                Debug.LogWarning($"剧情演出 >> 设置对话头像时，actorName 为空，无法通过 DialogueManager 绑定头像。");
                return;
            }
            
            // 头像名称 为空时，表示清除头像
            if (string.IsNullOrEmpty(headImageName))
            {
                // 立刻卸载 上一个头像图片
                if (!string.IsNullOrEmpty(_dialogueHeadAssetNameLast))
                {
                    UnloadAsset(_dialogueHeadAssetNameLast);
                    _dialogueHeadAssetNameLast = null;
                }
                // 清空 当前头像地址
                _dialogueHeadAssetName = null;
                // 通过 DialogueManager 将 头像图片 绑定到 actor 的 portrait（持久生效）
                DialogueManager.instance.SetActorPortraitSprite(actorName, null);
                return;
            }
            
            // 记录上一个头像地址，构造当前的资源地址（如果使用Addressables会加上路径和扩展名）
            _dialogueHeadAssetNameLast = _dialogueHeadAssetName;
#if USE_ADDRESSABLES
            _dialogueHeadAssetName = $"{dialogueHeadAddressableFolder}{headImageName}{dialogueHeadExtension}";
#else
            _dialogueHeadAssetName = headImageName;
#endif
            Debug.Log($"剧情演出 >> 设置对话头像为 '{_dialogueHeadAssetName}'。");

            // 加载 头像图片
            LoadAsset<UnityEngine.Object>(_dialogueHeadAssetName, (asset) =>
            {
                // 处理Sprite或Texture2D两种情况
                var sprite = asset as Sprite;
                if (!sprite && asset is Texture2D)
                {
                    var texture = asset as Texture2D;
                    sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                    sprite.name = texture.name;
                }
                if (!sprite)
                {
                    Debug.LogWarning($"剧情演出 >> 无法加载对话头像 '{_dialogueHeadAssetName}'。请检查名称是否正确。");
                    return;
                }

                // 通过 DialogueManager 将 头像图片 绑定到 actor 的 portrait（持久生效）
                DialogueManager.instance.SetActorPortraitSprite(actorName, sprite);

                // 卸载 上一个头像图片
                if (!string.IsNullOrEmpty(_dialogueHeadAssetNameLast))
                {
                    UnloadAsset(_dialogueHeadAssetNameLast);
                    _dialogueHeadAssetNameLast = null;
                }
            });
        }
        
        /// <summary>
        /// 清除所有 对话 头像图像
        /// </summary>
        /// <returns></returns>
        private void ClearAllDialogueHeadImage()
        {
            // 立刻卸载 上一个头像图片
            if (!string.IsNullOrEmpty(_dialogueHeadAssetNameLast))
            {
                UnloadAsset(_dialogueHeadAssetNameLast);
                _dialogueHeadAssetNameLast = null;
            }
            // 卸载 当前头像图片
            if (!string.IsNullOrEmpty(_dialogueHeadAssetName))
            {
                UnloadAsset(_dialogueHeadAssetName);
                _dialogueHeadAssetName = null;
            }
        }
        #endregion
        #endregion
        
        #region 背景
        [Header("背景")]
        [Tooltip("背景图像组件 当前")]
        [SerializeField] private SpriteRenderer srBackgroundCurrent;
        [Tooltip("背景图像组件 上次（用于淡入淡出效果）")]
        [SerializeField] private SpriteRenderer srBackgroundLast;
        
#if USE_ADDRESSABLES
        [Tooltip("背景资产的文件夹路径")]
        [SerializeField] private string backgroundAddressableFolder = "Assets/Plugins/Pixel Crushers/VNStoryManager/Assets/Backgrounds/";
        [Tooltip("背景资产的扩展名。建议使用jpg以节省空间")]
        [SerializeField] private string backgroundAddressableExtension = ".jpg";
#endif
        [Tooltip("Conversation中，节点上配置的 字段标题 背景")]
        [SerializeField] private string backgroundFieldTitle = "Background";
        [Tooltip("Conversation中，节点上配置的 字段标题 背景淡入淡出时间")]
        [SerializeField] private string backgroundFadeDurationFieldTitle = "BackgroundFadeDuration";
        
        [Tooltip("背景淡入淡出时间（秒）")]
        [SerializeField] public float backgroundFadeDuration = 0.3f;
        
        private bool _isBackgroundFadeIn; // 当前背景是否是淡入状态
#if DOTWEEN
        // Tween 清除所有背景图像的
        private Tween _tweenClearAllBackground;
#endif
        
        /// <summary>
        /// 初始化 背景
        /// </summary>
        private void InitBackground()
        {
            // 初始化背景图像组件
            if (srBackgroundCurrent)
            {
                srBackgroundCurrent.sprite = null;
                srBackgroundCurrent.color = new Color(1f, 1f, 1f, 0f);
                srBackgroundCurrent.gameObject.SetActive(false);
            }
            if (srBackgroundLast)
            {
                srBackgroundLast.sprite = null;
                srBackgroundLast.color = new Color(1f, 1f, 1f, 0f);
                srBackgroundLast.gameObject.SetActive(false);
            }
            // 初始化背景状态
            _isBackgroundFadeIn = false;
        }
        
        /// <summary>
        /// 淡入 背景。
        /// </summary>
        private void FadeInBackground()
        {
            if (_isBackgroundFadeIn) return; // 已经是淡入状态则不重复执行
            _isBackgroundFadeIn = true;
            
            if (srBackgroundCurrent)
            {
                srBackgroundCurrent.gameObject.SetActive(true);
#if DOTWEEN
                srBackgroundCurrent.DOKill();
                srBackgroundCurrent.DOFade(1f, backgroundFadeDuration);
#endif
            }
            if (srBackgroundLast)
            {
                srBackgroundLast.gameObject.SetActive(true);
#if DOTWEEN
                srBackgroundLast.DOKill();
                srBackgroundLast.DOFade(0f, backgroundFadeDuration);
#endif
            }
        }
        
        /// <summary>
        /// 淡出 背景。
        /// </summary>
        private void FadeOutBackground()
        {
            if (!_isBackgroundFadeIn) return; // 已经是淡出状态则不重复执行
            _isBackgroundFadeIn = false;
            
            if (srBackgroundCurrent)
            {
#if DOTWEEN
                srBackgroundCurrent.DOKill();
                srBackgroundCurrent.DOFade(0f, backgroundFadeDuration).OnComplete(() =>
                {
                    srBackgroundCurrent.gameObject.SetActive(false);
                });
#endif
            }
            if (srBackgroundLast)
            {
#if DOTWEEN
                srBackgroundLast.DOKill();
                srBackgroundLast.DOFade(0f, backgroundFadeDuration).OnComplete(() =>
                {
                    srBackgroundLast.gameObject.SetActive(false);
                });
#endif
            }
        }
        
        /// <summary>
        /// 清除所有 背景图像
        /// </summary>
        /// <returns></returns>
        private void ClearAllBackground()
        {
            float delay = backgroundFadeDuration;
            
            // 淡出背景图像
            if (srBackgroundCurrent)
            {
#if DOTWEEN
                srBackgroundCurrent.DOKill();
                srBackgroundCurrent.DOFade(0f, delay);
#endif
            }
            if (srBackgroundLast)
            {
#if DOTWEEN
                srBackgroundLast.DOKill();
                srBackgroundLast.DOFade(0f, delay);
#endif
            }
#if DOTWEEN
            _tweenClearAllBackground = DOVirtual.DelayedCall(delay, () =>
            {
                // 卸载背景图像资源
                UnloadAsset(_backgroundAssetNameLast);
                _backgroundAssetNameLast = null;
                UnloadAsset(_backgroundAssetName);
                _backgroundAssetName = null;
                // 清空并隐藏背景图像组件
                if (srBackgroundCurrent)
                {
                    srBackgroundCurrent.sprite = null;
                    srBackgroundCurrent.gameObject.SetActive(false);
                }
                if (srBackgroundLast)
                {
                    srBackgroundLast.sprite = null;
                    srBackgroundLast.gameObject.SetActive(false);
                }
                
                _tweenClearAllBackground = null;
            });
#endif
        }
        
        #region 设置背景
        // 当前背景图像名称。
        private string _backgroundAssetName;
        // 上个背景图像名称。
        private string _backgroundAssetNameLast;
        
        /// <summary>
        /// 对话行 背景变化
        /// </summary>
        /// <param name="subtitle"></param>
        private void OnOnConversationLineBackgroundChange(Subtitle subtitle)
        {
            // 上次清除背景图像的Tween 未结束，强制完成
            if (_tweenClearAllBackground != null)
            {
                _tweenClearAllBackground.Complete();
                _tweenClearAllBackground = null;
            }
            
            // 尝试从 对话行字段 获取 背景图像名称
            var background = Field.LookupValue(subtitle.dialogueEntry.fields, backgroundFieldTitle);
            if (string.IsNullOrEmpty(background) == false)
            {
                // 设置背景图像
                SetBackgroundImage(background);
            }
        }
        
        /// <summary>
        /// 设置背景图像。
        /// </summary>
        /// <param name="backgroundName"></param>
        private void SetBackgroundImage(string backgroundName)
        {
            if (string.IsNullOrEmpty(backgroundName) || string.Equals(backgroundName, "nil")) return;

            // 记录上个背景图像名称
            _backgroundAssetNameLast = _backgroundAssetName;
            // 记录背景图像名称
            _backgroundAssetName = backgroundName;
#if USE_ADDRESSABLES
            // 使用Addressables时，添加文件夹路径
            _backgroundAssetName = $"{backgroundAddressableFolder}{backgroundName}{backgroundAddressableExtension}";
#endif
            // 日志输出
            Debug.Log($"剧情演出 >> 设置背景图像为 '{_backgroundAssetName}'。");
            
            // 加载图像资源
            LoadAsset<Sprite>(_backgroundAssetName, OnBackgroundAssetLoaded);
        }
        
        /// <summary>
        /// 资源加载完成 背景图像
        /// </summary>
        /// <param name="asset"></param>
        private void OnBackgroundAssetLoaded(UnityEngine.Object asset)
        {
            // 图片类型为Sprite时，直接使用
            var image = asset as Sprite;
            // 图片类型为Texture2D时，转换为Sprite
            if (!image && asset is Texture2D)
            {
                var texture = asset as Texture2D;
                image = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(texture.width * 0.5f, texture.height * 0.5f));
                image.name = texture.name;
            }
            // 如果图片加载失败，尝试使用Addressables加载
            if (!image)
            {
                Debug.LogWarning($"剧情演出 >> 无法加载背景图像 '{_backgroundAssetName}'。请检查名称是否正确。");
                return;
            }
            
            // 启动协程，设置背景图像
            StartCoroutine(SetBackgroundImageCoroutine(image));
        }
        
        /// <summary>
        /// 设置背景淡入淡出时间。
        /// </summary>
        /// <param name="duration"></param>
        private void BackgroundFadeDuration(double duration)
        {
            backgroundFadeDuration = (float)duration;
            // 设置Lua变量，保持同步
            DialogueLua.SetVariable(backgroundFadeDurationFieldTitle, duration);
        }
        #endregion

        #region 背景切换特效
        /// <summary>
        /// 设置背景图像的协程。
        /// </summary>
        /// <param name="image"></param>
        /// <returns></returns>
        private IEnumerator SetBackgroundImageCoroutine(Sprite image)
        {
            // 激活 当前背景图像组件
            if (srBackgroundCurrent.gameObject.activeSelf == false)
                srBackgroundCurrent.gameObject.SetActive(true);
            
            // 淡入淡出时间为0时，直接切换图像
            if (Mathf.Approximately(0, backgroundFadeDuration))
            {
                // 直接设置 当前背景图像
                srBackgroundCurrent.sprite = image;
                // 清空并隐藏 上个背景图像
                if (srBackgroundLast && srBackgroundLast.gameObject.activeSelf)
                {
                    srBackgroundLast.sprite = null;
                    srBackgroundLast.gameObject.SetActive(false);
                }
                yield break;
            }

            // 如果只有background Image，使用DialogueManager的场景切换管理器进行切换
            if (srBackgroundCurrent && !srBackgroundLast)
            {
                var sceneTransitionManager = DialogueManager.instance.GetComponentInChildren<StandardSceneTransitionManager>();
                if (sceneTransitionManager)
                {
                    // 使用场景切换管理器进行切换
                    // 离开场景，淡出
                    sceneTransitionManager.leaveSceneTransition.TriggerAnimation();
                    // 等待淡出动画完成和额外的淡入淡出时间
                    yield return new WaitForSeconds(sceneTransitionManager.leaveSceneTransition.animationDuration + backgroundFadeDuration);
                    // 切换图像
                    srBackgroundCurrent.sprite = image;
                    // 进入场景，淡入
                    sceneTransitionManager.enterSceneTransition.TriggerAnimation();
                }
                else
                {
                    // 无场景切换管理器，直接切换图像
                    srBackgroundCurrent.sprite = image;
                    yield break;
                }
            }
            // 有两个背景图像时，使用淡入淡出效果切换
            else if (srBackgroundCurrent && srBackgroundLast)
            {
                // BackgroundLast覆盖在BackgroundCurrent上。先激活 BackgroundLast。
                if (srBackgroundLast.gameObject.activeSelf == false)
                    srBackgroundLast.gameObject.SetActive(true);
                // 将旧背景图设置到BackgroundLast。设置为不透明。
                srBackgroundLast.sprite = srBackgroundCurrent.sprite;
                srBackgroundLast.color = new Color(1, 1, 1, 1);
                srBackgroundLast.enabled = true;
                // 将新背景图设置到BackgroundCurrent。设置为透明。
                srBackgroundCurrent.sprite = image;
                srBackgroundCurrent.color = new Color(1, 1, 1, 0);
                // 淡入淡出效果
                float elapsed = 0;
                while (elapsed < backgroundFadeDuration)
                {
                    var t = (elapsed / backgroundFadeDuration);
                    srBackgroundLast.color = new Color(1, 1, 1, 1 - t);
                    srBackgroundCurrent.color = new Color(1, 1, 1, t);
                    yield return null;
                    elapsed += Time.deltaTime;
                }
                // 动画结束后，设置新背景为不透明，隐藏旧背景
                srBackgroundLast.enabled = false;
                srBackgroundCurrent.color = new Color(1, 1, 1, 1);
            }
            
            // 卸载上个背景图像资源
            if (!string.IsNullOrEmpty(_backgroundAssetNameLast))
            {
                UnloadAsset(_backgroundAssetNameLast);
                _backgroundAssetNameLast = null;
            }
        }
        #endregion
        #endregion

        #region 角色
        [Header("角色")]
#if USE_ADDRESSABLES
        [Tooltip("角色资产 的文件夹路径")]
        [SerializeField] private string actorAddressableFolder = "Assets/Plugins/Pixel Crushers/VNStoryManager/Assets/Actors/";
        [Tooltip("角色资产 的扩展名。一般使用Prefab")]
        [SerializeField] private string actorAddressableExtension = ".prefab";
#endif
        [Tooltip("Conversation中，节点上配置的 字段标题 角色 ")]
        [SerializeField] private string[] actorFieldTitle = { "Actor1Prefab", "Actor2Prefab", "Actor3Prefab" };
        [Tooltip("Conversation中，节点上配置的 字段标题 角色位置。[世界坐标X|世界坐标Y|世界坐标Z|移动速度倍率]")]
        [SerializeField] private string[] actorPosFieldTitle = { "Actor1Pos", "Actor2Pos", "Actor3Pos" };
        [Tooltip("Conversation中，节点上配置的 字段标题 角色旋转。[旋转角度X|旋转角度Y|旋转角度Z|旋转速度倍率]")]
        [SerializeField] private string[] actorRotateFieldTitle = { "Actor1Rotate", "Actor2Rotate", "Actor3Rotate" };
        [Tooltip("Conversation中，节点上配置的 字段标题 角色缩放。[缩放倍率X|缩放倍率Y|缩放倍率Z|缩放速度倍率]")]
        [SerializeField] private string[] actorScaleFieldTitle = { "Actor1Scale", "Actor2Scale", "Actor3Scale" };
        [Tooltip("Conversation中，节点上配置的 字段标题 角色动画。[动画Key1|动画Key2|动画Key3|...]")]
        [SerializeField] private string[] actorAnimFieldTitle = { "Actor1Anim", "Actor2Anim", "Actor3Anim" };
        
        #region 角色 加载与卸载
        // 已加载的 角色预制体 表。key=字段标题，value=加载数据
        private readonly Dictionary<string, FActorPrefabLoadData> _mapActorAnimator = new Dictionary<string, FActorPrefabLoadData>();
#if DOTWEEN
        // 延迟激活的Actor的Tween列表
        private List<Tween> _actorInitDelayTweens = new List<Tween>();
#endif
        
        /// <summary>
        /// 对话行 角色变化
        /// </summary>
        /// <param name="subtitle"></param>
        private void OnConversationLineActorChange(Subtitle subtitle)
        {
            // 停止所有 延迟激活的Actor的Tween。
            StopAllActorDelayTweens();
            
            for (int i = 0; i < actorFieldTitle.Length; i++)
            {
                // 获取 角色预制体
                var actorFieldTitleName = this.actorFieldTitle[i];
                var actorPrefabParam = Field.LookupValue(subtitle.dialogueEntry.fields, actorFieldTitleName);
                if (actorPrefabParam == "")
                {
                    // 配置了字段标题，但是值为空，卸载该 角色预制体
                    UnloadActorPrefab(actorFieldTitleName);
                }
                else
                {
                    // 获取 角色预制体名称 和 延迟显示时间
                    ParseStringAndFloat(actorPrefabParam, out var actorAssetPrefab, out var delay);
#if USE_ADDRESSABLES
                    // 使用Addressables时，添加文件夹路径
                    var actorAssetPrefabAddress = $"{actorAddressableFolder}{actorAssetPrefab}{actorAddressableExtension}";
#else
                    var actorAssetPrefabAddress = actorAssetPrefab;
#endif
                    // 获取 角色位置
                    var actorPosParam = Field.LookupValue(subtitle.dialogueEntry.fields, actorPosFieldTitle[i]);
                    // 获取 角色位置 移动速度
                    var actorRotateParam = Field.LookupValue(subtitle.dialogueEntry.fields, actorRotateFieldTitle[i]);
                    // 获取 角色缩放
                    var actorScaleParam = Field.LookupValue(subtitle.dialogueEntry.fields, actorScaleFieldTitle[i]);
                    // 获取 角色动画
                    var actorAnimParam = Field.LookupValue(subtitle.dialogueEntry.fields, actorAnimFieldTitle[i]);
                    
                    // 加载角色预制体
                    LoadActorPrefab
                    (
                        actorFieldTitleName, actorAssetPrefab, actorAssetPrefabAddress, delay, 
                        actorPosParam, actorRotateParam, actorScaleParam, actorAnimParam
                    );
                }
            }
        }
        
        /// <summary>
        /// 停止所有 延迟激活的Actor的Tween。
        /// </summary>
        private void StopAllActorDelayTweens()
        {
            // 停止所有延迟激活的Actor的Tween
            foreach (var tween in _actorInitDelayTweens)
            {
                tween.Kill();
            }
            _actorInitDelayTweens.Clear();
        }
        
        /// <summary>
        /// 清除所有 角色 预制体。
        /// </summary>
        /// <returns></returns>
        private void ClearAllActors()
        {
            // 停止所有延迟激活的Actor的Tween
            StopAllActorDelayTweens();
            
            // 卸载 所有角色预制体
            var actorFieldTitleList = new List<string>(_mapActorAnimator.Keys);
            var actorPrefabLoadDataList = new List<FActorPrefabLoadData>(_mapActorAnimator.Values);
            foreach (var actorFieldTitleName in actorFieldTitleList)
            {
                UnloadActorPrefab(actorFieldTitleName);
            }
            // 销毁 所有角色实例
            foreach (var data in actorPrefabLoadDataList)
            {
                if (data.PrefabInstance) Destroy(data.PrefabInstance);
            }
        }
        
        /// <summary>
        /// 加载角色预制体。
        /// </summary>
        /// <param name="actorFieldTitleName">角色 字段标题。用于确定角色槽位。</param>
        /// <param name="actorAssetPrefab">角色 预制体名称</param>
        /// <param name="actorAssetPrefabAddress">角色 预制体资源地址</param>
        /// <param name="delay">延迟显示</param>
        /// <param name="actorPosParam">角色 位置</param>
        /// <param name="actorRotateParam">角色 旋转</param>
        /// <param name="actorScaleParam">角色 缩放</param>
        /// <param name="actorAnimParam">角色 动画组</param>
        private void LoadActorPrefab
        (
            string actorFieldTitleName, 
            string actorAssetPrefab,
            string actorAssetPrefabAddress,
            float delay,
            string actorPosParam, 
            string actorRotateParam,
            string actorScaleParam, 
            string actorAnimParam
        )
        {
            // 检查 是否已经加载过 该角色预制体
            if (_mapActorAnimator.TryGetValue(actorFieldTitleName, out var actorPrefabLoadData))
            {
                // 角色预制体指定为空，或者与已加载的相同，直接进行设置
                if (string.IsNullOrEmpty(actorAssetPrefab))
                {
                    // 已经加载过，进行设置
                    // 有 过渡动画
                    SetActorPrefab(actorPrefabLoadData, actorPosParam, actorRotateParam, actorScaleParam, actorAnimParam);
                    return;
                }
                else if (actorPrefabLoadData.AssetAddress == actorAssetPrefabAddress)
                {
                    // 已经加载过，立即设置
                    // 无 过渡动画
                    InitActorPrefab(actorPrefabLoadData, actorPosParam, actorRotateParam, actorScaleParam, actorAnimParam);
                    return;
                }
                else
                {
                    // 该槽位 已经加载了其他角色预制体
                    // 先卸载 旧的角色预制体
                    UnloadActorPrefab(actorFieldTitleName);
                    // 再加载 新的角色预制体
                }
            }
            
            // 角色预制体名称 为空时，直接返回
            if (string.IsNullOrEmpty(actorAssetPrefab)) return;

            // 日志输出
            Debug.Log($"剧情演出 >> 加载Actor预制体 '{actorAssetPrefab}'。");
            
            // 加载角色预制体
            Action<GameObject> onAssetLoaded = (asset) =>
            {
                // 预制体类型为GameObject时，直接使用
                var actorPrefab = asset;
                if (!actorPrefab)
                {
                    Debug.LogWarning($"剧情演出 >> Actor预制体 '{actorAssetPrefab}' 加载失败。请检查名称是否正确。");
                    return;
                }
                
                // 获取角色Spine动画组件
                var actorAnimator = actorPrefab.GetComponent<VNActorAnimator>();
                if (!actorAnimator)
                {
                    Debug.LogWarning($"剧情演出 >> Actor预制体 '{actorAssetPrefab}' 中未找到 SkeletonAnimation 组件。请检查预制体设置。");
                }
                
                // 实例化角色预制体
                var actorPrefabInstance = Instantiate(actorPrefab, this.transform);
                actorPrefabInstance.SetActive(false); // 设置非激活。等待组件初始化时激活
                // 记录 角色预制体
                var actorAnimsLoadData = new FActorPrefabLoadData
                {
                    AssetAddress = actorAssetPrefabAddress,
                    PrefabAsset = actorPrefab,
                    PrefabInstance = actorPrefabInstance,
                    ActorAnimator = actorPrefabInstance.GetComponent<VNActorAnimator>()
                };
                _mapActorAnimator[actorFieldTitleName] = actorAnimsLoadData;
                
                if (delay > 0f)
                {
#if DOTWEEN
                    // 延迟设置角色预制体
                    // 使用单元素持有器引用Tween，避免闭包捕获外部被修改的变量。
                    var tweenHolder = new Tween[1];
                    tweenHolder[0] = DOVirtual.DelayedCall(delay, () =>
                    {
                        // 设置角色预制体
                        InitActorPrefab(actorAnimsLoadData, actorPosParam, actorRotateParam, actorScaleParam, actorAnimParam);
                        // 从延迟Tween列表中移除
                        _actorInitDelayTweens.Remove(tweenHolder[0]);
                    });
                    _actorInitDelayTweens.Add(tweenHolder[0]);
#endif
                }
                else
                {
                    // 设置角色预制体
                    InitActorPrefab(actorAnimsLoadData, actorPosParam, actorRotateParam, actorScaleParam, actorAnimParam);
                }
            };
            
            // 加载 角色预制体资源
            LoadAsset(actorAssetPrefabAddress, onAssetLoaded);
        }
        
        /// <summary>
        /// 卸载角色预制体。
        /// </summary>
        /// <param name="actorFieldTitleName">角色 字段标题</param>
        private void UnloadActorPrefab(string actorFieldTitleName)
        {
            if (_mapActorAnimator.TryGetValue(actorFieldTitleName, out var actorPrefabLoadData))
            {
                // 销毁 角色预制体的实例
                // 延迟激活的Actor未被激活，会直接销毁。
                actorPrefabLoadData.ActorAnimator?.ExecuteDestroy(() =>
                {
                    // 卸载 角色预制体资源
                    UnloadAsset(actorPrefabLoadData.AssetAddress);
                });
                // 从映射表中移除
                _mapActorAnimator.Remove(actorFieldTitleName);
            }
        }
        #endregion
        
        #region 角色 设置参数
        /// <summary>
        /// 初始化角色预制体。
        /// </summary>
        /// <param name="actorPrefabLoadData"></param>
        /// <param name="actorPosParam"></param>
        /// <param name="actorRotateParam"></param>
        /// <param name="actorScaleParam"></param>
        /// <param name="actorAnimParam"></param>
        private void InitActorPrefab
        (
            FActorPrefabLoadData actorPrefabLoadData,
            string actorPosParam,
            string actorRotateParam,
            string actorScaleParam,
            string actorAnimParam
        )
        {
            // 设置 角色预制体 实例的位置、缩放、动画
            if (actorPrefabLoadData.ActorAnimator)
            {
                // 获取 角色预制体 实例的 Transform
                var actorTrans = actorPrefabLoadData.PrefabInstance.transform;
                
                // 角色位置。默认 为 预制体 当前位置
                ParseVector3AndFloat(actorPosParam, out var toPos, out var _, actorTrans.position);
                ParseVector3AndFloat(actorRotateParam, out var toRot, out var _, actorTrans.rotation.eulerAngles);
                ParseVector3AndFloat(actorScaleParam, out var toScale, out var _, actorTrans.localScale);
                ParseStringArray(actorAnimParam, out var toStateArray);
                
                // 初始化 位置、缩放、动画
                actorPrefabLoadData.PrefabInstance.SetActive(false); // 先禁用，等待后续启用
                actorPrefabLoadData.ActorAnimator.ExecuteInit(toPos, toRot, toScale, toStateArray);
            }
        }
        
        /// <summary>
        /// 设置角色预制体。
        /// </summary>
        /// <param name="actorPrefabLoadData"></param>
        /// <param name="actorPosParam"></param>
        /// <param name="actorRotateParam"></param>
        /// <param name="actorScaleParam"></param>
        /// <param name="actorAnimParam"></param>
        private void SetActorPrefab
        (
            FActorPrefabLoadData actorPrefabLoadData,  
            string actorPosParam, 
            string actorRotateParam,
            string actorScaleParam, 
            string actorAnimParam
        )
        {
            // 设置 角色预制体 实例的位置、缩放、动画
            if (actorPrefabLoadData.ActorAnimator)
            {
                // 完成之前的 位置、缩放 插值动画
                actorPrefabLoadData.ActorAnimator.CompleteDoTweenTransform();
                
                // 获取 角色预制体 实例的 Transform
                var actorTrans = actorPrefabLoadData.PrefabInstance.transform;
                // 角色位置。默认 为 预制体 当前位置
                if (actorPosParam != null)
                {
                    // 位置变化速度
                    ParseVector3AndFloat(actorPosParam, out var toPos, out var posSpeed, actorTrans.position, 1f);
                    actorPrefabLoadData.ActorAnimator.SetToPosition(toPos, posSpeed);
                }
                // 角色旋转。默认 为 预制体 当前旋转
                if (actorRotateParam != null)
                {
                    // 旋转变化速度
                    ParseVector3AndFloat(actorRotateParam, out var toRot, out var rotSpeed, actorTrans.rotation.eulerAngles, 1f);
                    actorPrefabLoadData.ActorAnimator.SetToRotation(toRot, rotSpeed);
                }
                // 角色缩放。默认 为 预制体 当前缩放
                if (actorScaleParam != null)
                {
                    // 缩放变化速度
                    ParseVector3AndFloat(actorScaleParam, out var toScale, out var scaleSpeed, actorTrans.localScale, 1f);
                    actorPrefabLoadData.ActorAnimator.SetToScale(toScale, scaleSpeed);
                }
                
                // 角色动画组
                if (actorAnimParam != null)
                {
                    // 切换动画组
                    ParseStringArray(actorAnimParam, out var toStateArray);
                    actorPrefabLoadData.ActorAnimator.SwitchStateArray(toStateArray);
                }
            }
        }
        #endregion
        
        #region 角色、特效 淡入淡出
        /// <summary>
        /// 淡出 所有角色和特效。
        /// 若预制体带有 VNActorAnimator 则执行淡出动画；
        /// 否则为普通预制体，直接设置非激活（可能只是暂时被隐藏，之后还需恢复）。
        /// </summary>
        private void FadeOutActorsAndEffects()
        {
            foreach (var kvp in _mapActorAnimator)
            {
                var data = kvp.Value;
                if (data.ActorAnimator)
                {
                    // 有 VNActorAnimator，尝试淡出
                    bool handled = data.ActorAnimator.FadeOut();
                    if (!handled && data.PrefabInstance)
                    {
                        // VNActorAnimator 无法淡出（无Spine/粒子），降级为设置非激活
                        data.PrefabInstance.SetActive(false);
                    }
                }
                else if (data.PrefabInstance)
                {
                    // 普通预制体，无法淡出，设置非激活
                    data.PrefabInstance.SetActive(false);
                }
            }
        }
        
        /// <summary>
        /// 淡入 所有角色和特效。
        /// 若预制体带有 VNActorAnimator 则执行淡入动画；
        /// 否则为普通预制体，直接设置激活。
        /// </summary>
        private void FadeInActorsAndEffects()
        {
            foreach (var kvp in _mapActorAnimator)
            {
                var data = kvp.Value;
                if (data.ActorAnimator)
                {
                    // 有 VNActorAnimator，尝试淡入
                    bool handled = data.ActorAnimator.FadeIn();
                    if (!handled && data.PrefabInstance)
                    {
                        // VNActorAnimator 无法淡入（无Spine/粒子），降级为设置激活
                        data.PrefabInstance.SetActive(true);
                    }
                }
                else if (data.PrefabInstance)
                {
                    // 普通预制体，直接激活
                    data.PrefabInstance.SetActive(true);
                }
            }
        }
        #endregion
        #endregion

        #region 场景特效
        [Header("场景特效")]
#if USE_ADDRESSABLES
        [Tooltip("场景特效的 文件夹路径")]
        [SerializeField] private string effectAddressableFolder = "Assets/Plugins/Pixel Crushers/VNStoryManager/Assets/Effects/";
        [Tooltip("场景特效的 扩展名。一般使用Prefab")]
        [SerializeField] private string effectAddressableExtension = ".prefab";
#endif
        [Tooltip("Conversation中，节点上配置的 字段标题 特效 ")]
        [SerializeField] private string[] effectFieldTitle = { "Effect1Prefab", "Effect2Prefab", "Effect3Prefab" };
        [Tooltip("Conversation中，节点上配置的 字段标题 特效位置。[世界坐标X|世界坐标Y|世界坐标Z|移动速度倍率]")]
        [SerializeField] private string[] effectPosFieldTitle = { "Effect1Pos", "Effect2Pos", "Effect3Pos" };
        [Tooltip("Conversation中，节点上配置的 字段标题 特效旋转。[旋转角度X|旋转角度Y|旋转角度Z|旋转速度倍率]")]
        [SerializeField] private string[] effectRotateFieldTitle = { "Effect1Rotate", "Effect2Rotate", "Effect3Rotate" };
        [Tooltip("Conversation中，节点上配置的 字段标题 特效缩放。[缩放X|缩放Y|缩放Z|缩放速度倍率]")]
        [SerializeField] private string[] effectScaleFieldTitle = { "Effect1Scale", "Effect2Scale", "Effect3Scale" };
        [Tooltip("Conversation中，节点上配置的 字段标题 特效动画。[动画Key1|动画Key2|动画Key3]")]
        [SerializeField] private string[] effectAnimFieldTitle = { "Effect1Anim", "Effect2Anim", "Effect3Anim" };
        
        /// <summary>
        /// 对话行 特效变化
        /// </summary>
        /// <param name="subtitle"></param>
        private void OnConversationLineEffectChange(Subtitle subtitle)
        {
            // 与 角色加载流程、制作流程相同
            for (int i = 0; i < effectFieldTitle.Length; i++)
            {
                // 获取 特效预制体
                var effectFieldTitleName = this.effectFieldTitle[i];
                var effectPrefabParam = Field.LookupValue(subtitle.dialogueEntry.fields, effectFieldTitleName);
                if (effectPrefabParam == "")
                {
                    // 配置了 字段标题，但是值为空，表示卸载该 特效预制体
                    UnloadActorPrefab(effectFieldTitleName);
                }
                else
                {
                    // 获取 特效预制体名称 和 延迟显示时间
                    ParseStringAndFloat(effectPrefabParam, out var effectAssetPrefab, out var delay);
#if USE_ADDRESSABLES
                    // 使用Addressables时，添加文件夹路径
                    var effectAssetPrefabAddress = $"{effectAddressableFolder}{effectAssetPrefab}{effectAddressableExtension}";
#else
                    var effectAssetPrefabAddress = effectAssetPrefab;
#endif
                    // 获取 特效位置
                    var effectPosParam = Field.LookupValue(subtitle.dialogueEntry.fields, effectPosFieldTitle[i]);
                    // 获取 特效位置 移动速度
                    var effectRotateParam = Field.LookupValue(subtitle.dialogueEntry.fields, effectRotateFieldTitle[i]);
                    // 获取 特效缩放
                    var effectScaleParam = Field.LookupValue(subtitle.dialogueEntry.fields, effectScaleFieldTitle[i]);
                    // 获取 特效动画
                    var effectAnimParam = Field.LookupValue(subtitle.dialogueEntry.fields, effectAnimFieldTitle[i]);

                    // 加载 特效预制体
                    LoadActorPrefab
                    (
                        effectFieldTitleName, effectAssetPrefab, effectAssetPrefabAddress, delay,
                        effectPosParam, effectRotateParam, effectScaleParam, effectAnimParam
                    );
                }
            }
        }
        #endregion
        
        #region 音频
        [Header("音频")]
        [Tooltip("Conversation中，节点上配置的 字段标题 背景音乐 [音频Key|音量|音调|延迟播放时间(秒)]")]
        [SerializeField] private string[] audioBGMFieldTitle = { "AudioBGM1", "AudioBGM2", "AudioBGM3" };
        [Tooltip("Conversation中，节点上配置的 字段标题 环境音 [音频Key|音量|音调|延迟播放时间(秒)]")]
        [SerializeField] private string[] audioAmbientFieldTitle = { "AudioAmbient1", "AudioAmbient2", "AudioAmbient3" };
        [Tooltip("Conversation中，节点上配置的 字段标题 特效音 [音频Key|音量|音调|延迟播放时间(秒)]")]
        [SerializeField] private string[] audioSfxFieldTitle = { "AudioSFX1", "AudioSFX2", "AudioSFX3" };
        [Tooltip("Conversation中，节点上配置的 字段标题 语音 [音频Key|音量|音调|延迟播放时间(秒)]")]
        [SerializeField] private string[] audioVoiceFieldTitle = { "AudioVoice1", "AudioVoice2", "AudioVoice3" };
        
        // 记录 背景音乐-字段标题 到 音频Key 的映射表。用于停止上个对话行播放的背景音乐。
        private readonly Dictionary<string, string> _dicBgmFieldTitleToAudioKey = new Dictionary<string, string>();
        // 记录 音频-字段标题 到 音频Key 的映射表。进入到新的对话行时，停止所有上个对话行播放的音频。
        private readonly Dictionary<string, string> _dicSfxFieldTitleToAudioKey = new Dictionary<string, string>();
#if DOTWEEN
        // 记录 背景音乐 延迟播放的Tween。
        private readonly List<Tween> _bgmDelayTweens = new List<Tween>();
        // 记录 音效音频 延迟播放的Tween。
        private readonly List<Tween> _sfxDelayTweens = new List<Tween>();
#endif
        
        /// <summary>
        /// 对话行 音频变化
        /// </summary>
        /// <param name="subtitle"></param>
        private void OnConversationLineAudioChange(Subtitle subtitle)
        {
            // 立即完成 所有背景音乐 延迟播放的 Tween。
            // 立刻完成BGM的切换。避免BGM切换失败。
            CompleteAllBGMDelayTweens();
            // 背景音乐
            for (int i = 0; i < audioBGMFieldTitle.Length; i++)
            {
                var audioFieldTitle = audioBGMFieldTitle[i];
                var audioBGMParam = Field.LookupValue(subtitle.dialogueEntry.fields, audioFieldTitle);
                if (audioBGMParam == "")
                {
                    // 停止背景音乐
                    StopBGMByFieldTitle(audioFieldTitle);
                }
                else if (string.IsNullOrEmpty(audioBGMParam) == false)
                {
                    // 播放背景音乐
                    PlayBGMByParam(audioFieldTitle, audioBGMParam);
                }
            }

            // 停止所有 上个对话行播放的 音频
            // 不再播放。避免 大量音频叠加播放。影响演出效果。
            ClearAllSfx();
            // 环境音
            for (int i = 0; i < audioAmbientFieldTitle.Length; i++)
            {
                var audioFieldTitle = audioAmbientFieldTitle[i];
                var audioAmbientParam = Field.LookupValue(subtitle.dialogueEntry.fields, audioFieldTitle);
                PlaySfxByParam(audioFieldTitle, audioAmbientParam);
            }
            // 音效
            for (int i = 0; i < audioSfxFieldTitle.Length; i++)
            {
                var audioFieldTitle = audioSfxFieldTitle[i];
                var audioSfxParam = Field.LookupValue(subtitle.dialogueEntry.fields, audioFieldTitle);
                PlaySfxByParam(audioFieldTitle, audioSfxParam);
            }
            // 语音
            for (int i = 0; i < audioVoiceFieldTitle.Length; i++)
            {
                var audioFieldTitle = audioVoiceFieldTitle[i];
                var audioVoiceParam = Field.LookupValue(subtitle.dialogueEntry.fields, audioFieldTitle);
                PlaySfxByParam(audioFieldTitle, audioVoiceParam);
            }
        }

        #region 音频 播放与停止
        /// <summary>
        /// 播放 背景音乐。
        /// </summary>
        /// <param name="audioFieldTitle">音频 字段标题</param>
        /// <param name="audioParam"></param>
        private void PlayBGMByParam(string audioFieldTitle, string audioParam)
        {
            // 解析音频参数，获取音频Key和延迟时间
            if (ParseStringAndThreeFloat
            (
                audioParam, out var audioKey, 
                out var volume, out var pitch, out var delay
            ))
            {
                // 默认参数
                volume = volume <= 0f ? 1f : volume; // 音量 默认 1.0f
                pitch = pitch <= 0f ? 1f : pitch; // 音调 默认 1.0f。影响 播放速度 和 音高。
                // 延迟播放
                if (delay > 0f)
                {
#if DOTWEEN
                    var tweenHolder = new Tween[1];
                    tweenHolder[0] = DOVirtual.DelayedCall(delay, () =>
                    {
                        // 延迟时间到，播放背景音乐。循环播放
                        AudioManager.Instance.PlayWithChannel(audioFieldTitle, audioKey, volume, pitch);
                        // 记录 背景音乐Key
                        _dicBgmFieldTitleToAudioKey[audioFieldTitle] = audioKey;
                        // 从 延迟播放的 Tween 列表中 移除
                        _bgmDelayTweens.Remove(tweenHolder[0]);
                    });
                    // 记录 延迟播放的 Tween
                    _bgmDelayTweens.Add(tweenHolder[0]);
#endif
                }
                else
                {
                    // 立即播放背景音乐。循环播放
                    AudioManager.Instance.PlayWithChannel(audioFieldTitle, audioKey, volume, pitch);
                    // 记录 背景音乐Key
                    _dicBgmFieldTitleToAudioKey[audioFieldTitle] = audioKey;
                }
            }
        }
        
        /// <summary>
        /// 停止 背景音乐。
        /// </summary>
        /// <param name="audioFieldTitle">音频 字段标题</param>
        private void StopBGMByFieldTitle(string audioFieldTitle)
        {
            AudioManager.Instance.StopWithChannel(audioFieldTitle);
        }
        
        /// <summary>
        /// 立即完成 所有背景音乐 延迟播放的 Tween。
        /// </summary>
        private void CompleteAllBGMDelayTweens()
        {
#if DOTWEEN
            // 立即完成 背景音乐 延迟播放的 Tween
            foreach (Tween tween in _bgmDelayTweens)
            {
                tween.Complete();
            }
            _bgmDelayTweens.Clear();
#endif
        }
        
        /// <summary>
        /// 清除 所有背景音乐。
        /// </summary>
        private void ClearAllBGM()
        {
#if DOTWEEN
            // 立即完成 背景音乐 延迟播放的 Tween
            foreach (Tween tween in _bgmDelayTweens)
            {
                tween.Kill();
            }
            _bgmDelayTweens.Clear();
#endif
            // 停止所有 记录的 背景音乐Key
            foreach (var kvp in _dicBgmFieldTitleToAudioKey)
            {
                AudioManager.Instance.Stop(kvp.Value);
            }
            _dicBgmFieldTitleToAudioKey.Clear();
        }
        
        /// <summary>
        /// 播放 音频。
        /// </summary>
        /// <param name="audioFieldTitle">音频 字段标题。用于确定槽位。</param>
        /// <param name="audioParam">音频参数 内容。</param>
        private void PlaySfxByParam(string audioFieldTitle, string audioParam)
        {
            // 解析音频参数，获取音频Key和延迟时间
            if (ParseStringAndThreeFloat
            (
                audioParam, out var audioKey, 
                out var volume,out var pitch, out var delay
            ))
            {
                // 默认参数
                volume = volume <= 0f ? 1f : volume; // 音量 默认 1.0f
                pitch = pitch <= 0f ? 1f : pitch; // 音调 默认 1.0f。影响 播放速度 和 音高。
                // 延迟播放
                if (delay > 0f)
                {
#if DOTWEEN
                    var tweenHolder = new Tween[1];
                    tweenHolder[0] = DOVirtual.DelayedCall(delay, () =>
                    {
                        // 延迟 播放音频
                        AudioManager.Instance.Play(audioKey, volume, pitch);
                        // 记录 音频Key
                        _dicSfxFieldTitleToAudioKey[audioFieldTitle] = audioKey;
                        // 从 延迟播放的 Tween 列表中 移除
                        _sfxDelayTweens.Remove(tweenHolder[0]);
                    });
                    // 记录 延迟播放的 Tween
                    _sfxDelayTweens.Add(tweenHolder[0]);
#endif
                }
                else
                {
                    // 立即播放音频
                    // 立即 播放音频
                    AudioManager.Instance.Play(audioKey, volume, pitch);
                    // 记录 音频Key
                    _dicSfxFieldTitleToAudioKey[audioFieldTitle] = audioKey;
                }
            }
        }
        
        /// <summary>
        /// 清除 所有音频。环境音、音效、语音。
        /// </summary>
        private void ClearAllSfx()
        {
            // 停止所有 延迟播放的 音频
            // 跳过对话后，音频也不再播放。
#if DOTWEEN
            foreach (var tween in _sfxDelayTweens)
            {
                tween.Kill();
            }
            _sfxDelayTweens.Clear();
#endif
            // 停止所有 记录的 音频Key
            foreach (var kvp in _dicSfxFieldTitleToAudioKey)
            {
                AudioManager.Instance.Stop(kvp.Value);
            }
            _dicSfxFieldTitleToAudioKey.Clear();
        }
        #endregion
        #endregion

        #region 游戏玩法注册
        // Key=Fields的Title名称, Value=玩法系统的回调方法
        private readonly Dictionary<string, Action<string>> _gameplaySystemRegistry = new Dictionary<string, Action<string>>();

        /// <summary>
        /// 注册 玩法系统。
        /// 其他玩法系统通过此API进行注册，传入Fields的Title名称 和 自定义的回调方法。
        /// 当对话行中存在对应Title且值有效时，会自动调用回调方法并将Value值作为参数传入。
        /// </summary>
        /// <param name="fieldTitle">Fields的Title名称</param>
        /// <param name="callback">玩法系统的回调方法（传参为string，具体参数类型由玩法系统自行解析）</param>
        public void RegisterGameplaySystem(string fieldTitle, Action<string> callback)
        {
            if (string.IsNullOrEmpty(fieldTitle))
            {
                Debug.LogWarning("[VNStoryManager] RegisterGameplaySystem >> fieldTitle 不能为空。");
                return;
            }
            if (callback == null)
            {
                Debug.LogWarning($"[VNStoryManager] RegisterGameplaySystem >> 注册 '{fieldTitle}' 时，callback 不能为 null。");
                return;
            }

            if (_gameplaySystemRegistry.ContainsKey(fieldTitle))
            {
                Debug.LogWarning($"[VNStoryManager] RegisterGameplaySystem >> '{fieldTitle}' 已经注册过了。将覆盖之前的注册。");
                _gameplaySystemRegistry[fieldTitle] = callback;
            }
            else
            {
                _gameplaySystemRegistry.Add(fieldTitle, callback);
            }
        }

        /// <summary>
        /// 注销 玩法系统。
        /// </summary>
        /// <param name="fieldTitle">Fields的Title名称</param>
        public void UnregisterGameplaySystem(string fieldTitle)
        {
            if (string.IsNullOrEmpty(fieldTitle)) return;
            _gameplaySystemRegistry.Remove(fieldTitle);
        }

        /// <summary>
        /// 对话行 玩法系统。
        /// 遍历所有已注册的玩法系统，尝试从 subtitle 的 Fields 中获取对应 Title 的 Value，
        /// 若值有效则调用对应玩法系统的回调方法，并将 Value 值作为参数传入。
        /// </summary>
        /// <param name="subtitle"></param>
        private void OnConversationLineGamePlaySystem(Subtitle subtitle)
        {
            foreach (var kvp in _gameplaySystemRegistry)
            {
                var fieldTitle = kvp.Key;
                var callback = kvp.Value;

                // 尝试从 subtitle 的 Fields 中获取对应 Title 的 Value
                var value = Field.LookupValue(subtitle.dialogueEntry.fields, fieldTitle);

                // 值有效时，调用对应玩法系统的回调方法
                if (!string.IsNullOrEmpty(value))
                {
                    callback?.Invoke(value);
                }
            }
        }
        #endregion
        
        #region 字符串参数解析
        /// <summary>
        /// 解析字符串为字符串数组。
        /// </summary>
        /// <param name="arrayString"></param>
        /// <param name="values"></param>
        /// <returns></returns>
        private void ParseStringArray(string arrayString, out string[] values)
        {
            values = Array.Empty<string>();
            if (string.IsNullOrEmpty(arrayString)) return;

            // 使用,或者，进行分割
            values = arrayString.Split('|', StringSplitOptions.RemoveEmptyEntries);
        }

        /// <summary>
        /// 解析字符串为 字符串 和 一个浮点数。
        /// </summary>
        /// <param name="paramString"></param>
        /// <param name="outString"></param>
        /// <param name="outFloat"></param>
        /// <param name="defaultFloat"></param>
        /// <returns></returns>
        private void ParseStringAndFloat(string paramString, out string outString, out float outFloat,
            float defaultFloat = -1f)
        {
            outString = null;
            outFloat = defaultFloat;
            if (string.IsNullOrEmpty(paramString)) return;

            // 使用,或者，进行分割
            var values = paramString.Split('|', StringSplitOptions.RemoveEmptyEntries);
            if (values.Length >= 2)
            {
                outString = values[0];
                float.TryParse(values[1], out outFloat);
            }
            else if (values.Length == 1)
            {
                outString = values[0];
            }
        }

        /// <summary>
        /// 解析字符串为 Vector3 和 一个浮点数。
        /// </summary>
        /// <param name="paramString"></param>
        /// <param name="outVector3"></param>
        /// <param name="outFloat"></param>
        /// <param name="defaultVec3">默认 Vector3值</param>
        /// <param name="defaultFloat"></param>
        /// <returns></returns>
        private void ParseVector3AndFloat(string paramString, out Vector3 outVector3, out float outFloat,
            Vector3 defaultVec3, float defaultFloat = -1f)
        {
            outVector3 = defaultVec3;
            outFloat = defaultFloat;
            if (string.IsNullOrEmpty(paramString)) return;

            // 使用,或者，进行分割
            var values = paramString.Split('|', StringSplitOptions.RemoveEmptyEntries);
            if (values.Length >= 4)
            {
                float.TryParse(values[0], out outVector3.x);
                float.TryParse(values[1], out outVector3.y);
                float.TryParse(values[2], out outVector3.z);
                float.TryParse(values[3], out outFloat);
            }
            else if (values.Length == 3)
            {
                float.TryParse(values[0], out outVector3.x);
                float.TryParse(values[1], out outVector3.y);
                float.TryParse(values[2], out outVector3.z);
            }
            else if (values.Length == 2)
            {
                float.TryParse(values[0], out outVector3.x);
                float.TryParse(values[1], out outVector3.y);
            }
            else if (values.Length == 1)
            {
                float.TryParse(values[0], out outVector3.x);
            }
        }
        
        /// <summary>
        /// 解析字符串为 字符串 和 三个浮点数。
        /// </summary>
        /// <param name="paramString"></param>
        /// <param name="outString"></param>
        /// <param name="outFloat1"></param>
        /// <param name="outFloat2"></param>
        /// <param name="outFloat3"></param>
        /// <returns></returns>
        private bool ParseStringAndThreeFloat(string paramString, out string outString, out float outFloat1, out float outFloat2, out float outFloat3)
        {
            outString = null;
            outFloat1 = -1f;
            outFloat2 = -1f;
            outFloat3 = -1f;
            if (string.IsNullOrEmpty(paramString)) return false;
            
            // 使用,或者，进行分割
            var values = paramString.Split('|', StringSplitOptions.RemoveEmptyEntries);
            if (values.Length >= 4)
            {
                outString = values[0];
                float.TryParse(values[1], out outFloat1);
                float.TryParse(values[2], out outFloat2);
                float.TryParse(values[3], out outFloat3);
                return true;
            }
            else if (values.Length == 3)
            {
                outString = values[0];
                float.TryParse(values[1], out outFloat1);
                float.TryParse(values[2], out outFloat2);
                return true;
            }
            else if (values.Length == 2)
            {
                outString = values[0];
                float.TryParse(values[1], out outFloat1);
                return true;
            }
            else if (values.Length == 1)
            {
                outString = values[0];
                return true;
            }

            return false;
        }
        #endregion

        #region 多语言
#if HAS_LOCALIZATION
        /// <summary>
        /// 初始化 多语言
        /// </summary>
        private void AwakeLocalization()
        {
            // 注册 多语言 变更 事件
            LocalizationSettings.SelectedLocaleChanged += OnSelectedLocaleChanged;
            
            // 立刻更新 当前语言
            OnSelectedLocaleChanged(null);
        }
        
        /// <summary>
        /// 销毁 多语言
        /// </summary>
        private void OnDestroyLocalization()
        {
            // 注销 多语言 变更 事件
            LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
        }
        
        /// <summary>
        /// 多语言 变更 事件处理
        /// </summary>
        /// <param name="locale"></param>
        protected virtual void OnSelectedLocaleChanged(Locale locale)
        {
            // 更新 当前语言 代码
            Localization.language = LocalizationSettings.SelectedLocale.Identifier.Code;
        }
#endif
        #endregion

        #region 变量注册
        // Key变量名:Value变量值的获取方法
        private readonly Dictionary<string, Func<object>> _variableGetterRegistryMap = new Dictionary<string, Func<object>>();
        
        /// <summary>
        /// 注册 变量与变量值获取器
        /// </summary>
        /// <param name="variableName">DialogueSystem中的变量名</param>
        /// <param name="valueGetter">变量值获取器</param>
        public void RegisterVariableGetter(string variableName, Func<object> valueGetter)
        {
            if (_variableGetterRegistryMap.ContainsKey(variableName))
            {
                Debug.LogWarning($"[VNStoryManager] RegisterVariable >> 变量名 '{variableName}' 已经注册过了。将覆盖之前的注册。");
                _variableGetterRegistryMap[variableName] = valueGetter;
            }
            else
            {
                _variableGetterRegistryMap.Add(variableName, valueGetter);
            }
        }

        /// <summary>
        /// 设置 所有的变量值 到 DialogueSystem。
        /// </summary>
        public void SetAllVariablesToDialogueSystem()
        {
            foreach (var kvp in _variableGetterRegistryMap)
            {
                string varName = kvp.Key;
                // 执行委托，实时获取外部系统的当前值
                Func<object> getter = kvp.Value;
                if (getter == null) continue;
                object varValue = getter.Invoke(); 
                if (varValue == null) continue;
                
                // 调用 DialogueLua 写入 DialogueSystem（内部会根据运行时类型转换为对应的 Lua 值）
                DialogueLua.SetVariable(varName, varValue);
            }
        }
        #endregion
    }
}