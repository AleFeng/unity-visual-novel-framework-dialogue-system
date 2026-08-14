using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Scripting;
using UnityEngine.UI;
using Ale.Toolkit.Runtime;
using PixelCrushers;
using PixelCrushers.DialogueSystem;

#if ATK_LOCALIZATION
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
#endif

namespace Ale.VnFramework
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
        public VnActorAnimator ActorAnimator;
    }
    
    /// <summary>
    /// Vn故事系统 管理器组件。
    /// </summary>
    public class VnStoryManager : ToolkitMonoSingleton<VnStoryManager>, ISaveable
    {
        protected override void Awake()
        {
            base.Awake();
            // 基类判定为重复实例时会销毁本对象并 return，但派生类的 override 仍会继续执行。
            // 若不在此处拦下，下面的 Lua 函数注册会把绑定改到一个即将被销毁的对象上。
            if (Instance != this) return;

            // 注册Lua函数-背景淡入淡出时间
            Lua.RegisterFunction("BackgroundFadeDuration", this, SymbolExtensions.GetMethodInfo(() => BackgroundFadeDuration(0)));
            // 注册持久化数据
            PersistentDataManager.RegisterPersistentData(gameObject);
            
            // 初始化 UI设置
            InitUI();
            // 初始化 背景
            InitBackground();
            
#if ATK_LOCALIZATION
            // 初始化 多语言
            AwakeLocalization();
#endif
            // 初始化 对话
            AwakeDialogue();

            // 订阅演出倍率变更，把倍率推给场上的角色与特效。
            // 静态事件在关闭 Reload Domain 的工程里会留住上一次运行的订阅者，故必须与 OnDestroy 配对。
            VnPlaybackRate.PlaybackChanged -= OnPlaybackRateChanged;
            VnPlaybackRate.PlaybackChanged += OnPlaybackRateChanged;
        }

        protected override void OnDestroy()
        {
            // 与 Awake 对称：重复实例从未做过下面这些注册，也就不该反注册。
            // 注意要在 base.OnDestroy() 之前判断——基类会把 Instance 置空。
            bool isSingletonInstance = Instance == this;
            base.OnDestroy();
            if (!isSingletonInstance) return;

            // 注销Lua函数。必须与 Awake 的注册配对：Lua 环境是静态的，注册项会强引用 this，
            // 而 Dialogue System 的 RegisterFunction 不是覆盖语义——同名函数再次注册时它只打一条
            // 「已注册」日志并**保留旧绑定**。不注销的话，下一个 VnStoryManager 注册会被忽略，
            // 剧本里的 BackgroundFadeDuration() 从此驱动的是已销毁的那个实例。
            Lua.UnregisterFunction("BackgroundFadeDuration");
            // 注销持久化数据
            PersistentDataManager.UnregisterPersistentData(gameObject);

            // 与 Awake 的订阅配对。不退订的话，事件会一直强引用已销毁的本实例，
            // 下一个实例上场后两个都会收到通知、其中一个在改一堆已经不存在的对象。
            VnPlaybackRate.PlaybackChanged -= OnPlaybackRateChanged;
            // 倍率归位：静态值会跨播放会话存活，退出时若正处于快进，下次进播放所有补间都会是 5 倍速。
            VnPlaybackRate.Set(1f, 1f);

#if ATK_LOCALIZATION
            // 销毁 多语言
            OnDestroyLocalization();
#endif
        }

        private void Start()
        {
            if (Instance != this) return;

            // SimStatus 的开关必须在 Start 而不是 Awake 里压——
            // DialogueSystemController 与本类在同一个 GameObject 上，两者 Awake 的先后未定义，
            // 而它的 Awake 里有一句会把 DialogueLua.includeSimStatus 覆写回 Inspector 上的值。
            // 详见 VnReadHistory.EnsureSimStatusEnabled 的说明。
            VnReadHistory.EnsureSimStatusEnabled();
        }

        #region 已读记录
        private VnReadHistory _readHistory;

        /// <summary>
        /// 已读记录。运行时判定走 Dialogue System 的 SimStatus，本对象负责紧凑持久化与读档回填。
        /// </summary>
        public VnReadHistory ReadHistory => _readHistory ?? (_readHistory = new VnReadHistory());
        #endregion

        #region 存档接口
        /// <summary>播放控制组件。挂在本物体上，没挂时为 null（配置部分的存档会被跳过）。</summary>
        public VnPlaybackController Playback => _playback ? _playback : (_playback = GetComponent<VnPlaybackController>());
        private VnPlaybackController _playback;

        /// <summary>
        /// 取全部需要持久化的状态，返回**深拷贝**。宿主持有并序列化它的期间，
        /// 继续玩不会改变已取出的这份快照。
        ///
        /// <para><b>本包只提供 Get / Set，不实现 Load / Save</b>——落盘归宿主的存档系统。</para>
        /// </summary>
        public VnStorySaveData GetSaveData() => new VnStorySaveData
        {
            Version = 1,
            Settings = Playback ? Playback.GetSettings() : null,
            ReadHistory = ReadHistory.Encode(),
            ReadHistoryStamp = VnReadHistory.BuildStamp(),
        };

        /// <summary>
        /// 从存档恢复。**覆盖**语义而非合并：先丢弃当前内存状态，再按存档重建；
        /// 存档里没有、内存里有的条目不会残留。
        ///
        /// <para>接受 <c>null</c> 与字段缺失的脏数据（跳过而非抛异常），且<b>不触发任何变更事件</b>——
        /// 这是 <see cref="ISaveable"/> 一族的契约。界面刷新由调用方在载入完成后自行触发，
        /// 按钮条可调 <c>Playback.NotifyStateChanged()</c>。</para>
        /// </summary>
        public void LoadSaveData(VnStorySaveData data)
        {
            if (data == null)
            {
                ResetAll();
                return;
            }

            if (Playback) Playback.ApplySettings(data.Settings);

            // 剧本被整库重导入过的话，会话与节点 ID 已重编号，位图会静默错位——
            // 玩家会看到没读过的剧情被当作已读跳过。宁可丢掉记录也不能错位。
            var currentStamp = VnReadHistory.BuildStamp();
            if (!string.IsNullOrEmpty(data.ReadHistoryStamp) && data.ReadHistoryStamp != currentStamp)
            {
                Debug.LogWarning($"剧情演出 >> 存档里的剧本指纹（{data.ReadHistoryStamp}）与当前剧本（{currentStamp}）不一致，" +
                                 "已读记录已被丢弃。这通常意味着剧情库被整库重新导入过（Chat Mapper / articy / CSV），" +
                                 "会话与节点 ID 已重编号，沿用旧记录会导致已读状态错位。");
                ReadHistory.ClearAll();
                return;
            }

            string error;
            if (!ReadHistory.Load(data.ReadHistory, out error))
            {
                Debug.LogWarning($"剧情演出 >> 已读记录解析失败（{error}），已按「什么都没读过」处理。");
            }
        }

        /// <summary>
        /// 清空本系统的全部运行时状态，回到初始态（开新游戏 / 读档前清场）。
        /// 同样<b>不触发变更事件</b>。
        /// </summary>
        public void ResetAll()
        {
            ReadHistory.ClearAll();
            if (Playback) Playback.ResetToDefaults();
            SetPlaybackRate(1f, 1f);
        }
        #endregion

        #region 播放倍率
        /// <summary>
        /// 设置演出倍率。本管理器是 <see cref="VnPlaybackRate"/> 的唯一写入者。
        /// </summary>
        /// <param name="playbackRate">演出倍率：补间、延时、角色动画、粒子、语音。</param>
        /// <param name="typewriterRate">打字机倍率：字速与标点停顿。</param>
        public void SetPlaybackRate(float playbackRate, float typewriterRate)
        {
            // Set 内部会重定时在途补间，并在演出倍率真的变了时触发 PlaybackChanged（→ 角色 / 特效跟随）。
            VnPlaybackRate.Set(playbackRate, typewriterRate);
            // 打字机倍率不走事件——只改档位、不进快进时 PlaybackChanged 并不触发，这里显式重刷。
            RefreshTypewriterSpeed();
        }

        private void OnPlaybackRateChanged(float rate)
        {
            ApplyPlaybackRateToActors(rate);
            ApplyPlaybackRateToVoice(rate);
        }

        /// <summary>
        /// 把演出倍率推给场上所有角色与特效实例。新实例出场时也应调一次，
        /// 否则快进途中加载出来的角色会以常速播放。
        /// </summary>
        public void ApplyPlaybackRateToActors(float rate)
        {
            foreach (var kvp in _mapActorAnimator)
            {
                var instance = kvp.Value.PrefabInstance;
                if (!instance) continue;

                // 先找可选契约的实现者（本包的 VnActorAnimator 实现了它，第三方组件也可以）。
                // 找得到就交给它自己决定怎么缩放后端——它会正确缓存作者基准值。
                var receivers = instance.GetComponentsInChildren<IVnPlaybackRateReceiver>(true);
                if (receivers.Length > 0)
                {
                    foreach (var receiver in receivers) receiver.OnVnPlaybackRateChanged(rate);
                }
                else
                {
                    ApplyPlaybackRateToPlainInstance(instance, rate);
                }
            }
        }

        /// <summary>
        /// 没有 <see cref="IVnPlaybackRateReceiver"/> 时的降级路径（纯图片 / 纯粒子预制体）。
        /// 判据与 <c>FadeInActorsAndEffects</c> 的降级分支一致。
        ///
        /// <para><b>这里是绝对赋值而不是「作者基准 × 倍率」</b>：本管理器不为这类实例缓存基准值，
        /// 相乘会在连续变速时逐次累乘（1→5→1 之后会停在 5 而不是回到 1）。
        /// 代价是把 <c>simulationSpeed</c> 配成非 1 的预制体会被改写成倍率本身；
        /// 需要保住自定义基准速度的，给预制体挂上 <see cref="VnActorAnimator"/>——它会正确缓存基准。</para>
        /// </summary>
        private static void ApplyPlaybackRateToPlainInstance(GameObject instance, float rate)
        {
            foreach (var anim in instance.GetComponentsInChildren<Animator>(true))
            {
                if (anim) anim.speed = rate;
            }

            foreach (var ps in instance.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (!ps) continue;
                // ParticleSystem.main 是结构体，必须取出来改完再写回，不能直接给属性链赋值。
                var main = ps.main;
                main.simulationSpeed = rate;
            }
        }
        #endregion

        #region 流程控制
        private bool _isVnStoryStarted; // Vn故事系统 是否已经开始
        
        /// <summary>
        /// 开始 Vn故事系统
        /// </summary>
        public void StartVnStory(string conversationName = null)
        {
            // 会话级的淡入只做一次；重复调用时跳过，但不能连带把下面的「开始对话」也吞掉。
            // 一度是整个方法早退，于是：剧情自然播完后 _isVnStoryStarted 无人清除
            // （自然结束不经过 StopVnStory，VnStoryPlayer 也只翻自己的 IsPlaying），
            // 此后再播任何一段剧情都是静默空操作。
            if (!_isVnStoryStarted)
            {
                _isVnStoryStarted = true;

                // 淡入 UI
                FadeInUI();
                // 淡入 背景;
                FadeInBackground();
                // 淡入 角色、特效
                FadeInActorsAndEffects();
            }

            // 开始 指定的对话演出
            if (!string.IsNullOrEmpty(conversationName))
                StartStoryConversation(conversationName);
        }
        
        /// <summary>
        /// 停止 Vn故事系统
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

        // ── Dialogue System 的消息处理器（以下四个） ─────────────────────────────
        //
        // 它们由 DialogueManager 经 BroadcastMessage **按方法名字符串**调用，代码里没有任何
        // 直接引用。两个后果，两条应对：
        //
        // ① **托管裁剪**：对 IL2CPP 的静态分析器而言，这是「没有任何调用方的方法」——
        //    教科书级的裁剪目标。目前 ProjectSettings 的 managedStrippingLevel 未设、走默认 Low，
        //    不裁用户程序集，所以尚未发作；一旦为移动端调到 Medium / High 就会变成真 bug
        //    （本工程 Android 已是 IL2CPP）。故一律加 [Preserve]。
        // ② **可覆写**：Pixel Crushers 自己的组件把这些声明为 public virtual（其插件内有 32 处），
        //    子类得以拦截或扩展演出流程。本类此前是 private，比任何一个 PC 组件都严。
        //    四个类都不是 sealed、当前也没有任何子类，故提升为 public virtual 纯属加法。
        //
        // ⚠️ 覆写时务必调用 base，否则整条演出链路会断掉。

        /// <summary>
        /// 当 对话 开始。由 Dialogue System 经 <c>BroadcastMessage</c> 调用。
        /// <para>覆写点：可在此暂停宿主的场景 BGM、切换输入模式等。<b>请调用 base。</b></para>
        /// </summary>
        /// <param name="actor"></param>
        [Preserve]
        public virtual void OnConversationStart(Transform actor)
        {
            // TODO: 暂停当前BGM

            // 把本段对话的已读状态推进 Lua。**必须在这里，不能更晚**：
            // DialogueSystemController.StartConversation 里 :1178 广播本消息、:1179 才 GotoState(firstState)，
            // 而首行的 SimStatus 是在 GotoState 之后的行准备阶段打的标。
            // 若拖到 OnConversationLine 才水合，就会用存档数据把 DS 刚打好的「本行已显示」覆盖掉。
            // lastConversationID 在 :1144 已经赋值，此刻可用。
            VnReadHistory.EnsureSimStatusEnabled();
            var conversationId = DialogueManager.lastConversationID;
            if (conversationId >= 0) ReadHistory.HydrateConversation(conversationId);
        }

        /// <summary>
        /// 当 对话 结束。由 Dialogue System 经 <c>BroadcastMessage</c> 调用。
        /// <para>覆写点：可在此恢复宿主的场景 BGM。<b>请调用 base</b>——
        /// 下面这几个 Clear 负责回收全部演出资源，跳过会造成句柄泄漏。</para>
        /// </summary>
        /// <param name="actor"></param>
        [Preserve]
        public virtual void OnConversationEnd(Transform actor)
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
        /// 当 对话行 开始。由 Dialogue System 经 <c>BroadcastMessage</c> 调用，是整条演出的分发中枢。
        /// <para>覆写点：可在 base 之前做前置处理、之后做收尾，或整段替换以自定义分发顺序。
        /// <b>不调 base 就等于关掉全部演出</b>（背景 / 角色 / 特效 / 音频 / 玩法系统都在里面）。</para>
        /// </summary>
        /// <param name="subtitle"></param>
        [Preserve]
        public virtual void OnConversationLine(Subtitle subtitle)
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
        /// 当 对话行 结束。由 Dialogue System 经 <c>BroadcastMessage</c> 调用。
        /// <para>本类不需要在这里做任何事，方法体刻意留空——它存在的意义是作为覆写点：
        /// 子类可在每行对话演完之后插入自己的逻辑（结算、埋点、条件判定等）。</para>
        /// </summary>
        /// <param name="subtitle"></param>
        [Preserve]
        public virtual void OnConversationLineEnd(Subtitle subtitle)
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
                // 淡入动画
                VnTween.FadeCanvasGroup(uiCanvasGroup, 1f, 0.5f, unscaled: false, onComplete: () =>
                {
                    // uiCanvasGroup可用
                    uiCanvasGroup.interactable = true; // 可交互
                    uiCanvasGroup.blocksRaycasts = true; // 接收射线
                });
            }
            else if (uiCanvas)
            {
                uiCanvas.gameObject.SetActive(true);
                // 与 FadeOutUI 的兜底路径成对：那里断掉了射线，这里必须收回来，否则淡出过一次 UI 就永久失灵。
                SetUiRaycasterEnabled(true);
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
                // DialogueSystem需要uiCanvas保持激活状态，因此 不进行非激活设置。
                // uiCanvasGroup不可用
                uiCanvasGroup.interactable = false; // 不可交互
                uiCanvasGroup.blocksRaycasts = false; // 不接收射线
                // 淡出动画
                VnTween.FadeCanvasGroup(uiCanvasGroup, 0f, 0.5f, unscaled: false, onComplete: () =>
                {
                    // 完成回调
                    onComplete?.Invoke();
                });
            }
            else if (uiCanvas)
            {
                // 走到这里的前提就是 uiCanvasGroup 为空——此处一度仍在给 uiCanvasGroup 赋值，
                // 必然 NRE，且异常发生在 onComplete 之前，对话因此永远停不下来。
                //
                // 没有 CanvasGroup 就没有 alpha 可淡、也没有 interactable 可关，能做的只有断掉射线。
                // 不用 SetActive(false)：上面那条注释说得很清楚，DialogueSystem 需要 uiCanvas 保持激活。
                // GraphicRaycaster.enabled 正是 blocksRaycasts 的对应物，且不动激活状态。
                SetUiRaycasterEnabled(false);
                // 完成回调
                onComplete?.Invoke();
            }
            else
            {
                // 两者都没配：没有可淡出的东西，但回调仍必须触发，
                // 否则 StopVnStory 里的 StopStoryConversation 永远不会执行。
                onComplete?.Invoke();
            }
        }

        /// <summary>
        /// 开关 <see cref="uiCanvas"/> 上的射线投射器。仅用于没有 <see cref="uiCanvasGroup"/> 的兜底路径，
        /// 承担 <c>CanvasGroup.blocksRaycasts</c> 的角色。组件缺席时静默跳过。
        /// </summary>
        private void SetUiRaycasterEnabled(bool enable)
        {
            if (!uiCanvas) return;
            var raycaster = uiCanvas.GetComponent<GraphicRaycaster>();
            if (raycaster) raycaster.enabled = enable;
        }
        #endregion
        
        #region 演出日志

        /// <summary>
        /// 演出流水日志。<b>只用于「发生了什么」的流水记录，不要拿它报错</b>——
        /// 真正的告警仍应直接用 <c>Debug.LogWarning</c>，那些在发行版里必须保留。
        ///
        /// <para><b>为什么用 <c>[Conditional]</c> 而不是运行时开关</b>：
        /// <c>Debug.Log($"…")</c> 的插值串在**调用之前**就构造好了，
        /// <c>Debug.unityLogger</c> 的过滤、乃至 <c>if (flag)</c> 守卫都救不了实参求值。
        /// <c>[Conditional]</c> 是**调用点**消除——符号没定义时，连同实参一起从 IL 里消失。</para>
        ///
        /// <para>多个 <c>[Conditional]</c> 之间是「或」：编辑器内照常输出；
        /// 发行版默认静默，想要日志就自行定义 <c>VNS_VERBOSE_LOG</c>。
        /// 这一路演出日志逐条对话都会刷，不门控的话会淹掉玩家的 log 文件——
        /// 顺带一提，Dialogue System 自己的 info 级日志默认就是关的（<c>DialogueDebug.level</c>
        /// 默认为 <c>Warning</c>，且叠了 <c>Debug.isDebugBuild</c>）。</para>
        ///
        /// <para>包内 <c>NullVnAudioBackend.WarnOnce</c> 是同款写法。</para>
        /// </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("VNS_VERBOSE_LOG")]
        private static void LogVerbose(string message) => Debug.Log(message);

        #endregion

        #region 资源加载与卸载
        // 加载中的资源 计数器。
        private readonly Dictionary<string, int> _dicLoadingAssetCounter = new Dictionary<string, int>();
        // 已加载的资源 计数器。
        private readonly Dictionary<string, int> _dicLoadedAssetCounter = new Dictionary<string, int>();
        // 已加载的资源表。
        // ⚠️ 只以地址为键、值类型是 UnityEngine.Object，而 LoadAsset 的泛型形参 T 随调用点变化
        // （头像用 Object、背景用 Sprite、角色与特效用 GameObject）。同一个地址若被两种 T 请求，
        // 第二次的 `loadedAsset as T` 会对一个完好的资源静默返回 null。
        // 目前四类资源的地址前缀（ActorsHead/ Backgrounds/ Actors/ Effects/）互不重叠，故不会发生；
        // 若将来出现共用地址的情况，这里要改成以 (地址, 类型) 为键。
        private readonly Dictionary<string, UnityEngine.Object> _dicLoadedAssets = new Dictionary<string, UnityEngine.Object>();
        // 加载中的资源 请求卸载的计数器（加载完成后应立即卸载的次数）。
        private readonly Dictionary<string, int> _dicPendingUnloadAfterLoad = new Dictionary<string, int>();
        // 同一地址在途加载期间，后续请求的回调队列（见 LoadAsset 里「同址并发」那段的说明）。
        private readonly Dictionary<string, List<Action<UnityEngine.Object>>> _dicPendingCallbacks =
            new Dictionary<string, List<Action<UnityEngine.Object>>>();
        // 由本类在运行时 Sprite.Create 出来的 Sprite（地址解析到的是 Texture2D 而非 Sprite 时才有）。
        // 这种 Sprite 不属于 Addressables，UnloadAsset 释放的只是它背后的纹理句柄，它自己必须显式 Destroy，
        // 否则每换一次头像 / 背景就漏一个。以地址为键，生命周期便与纹理严格对齐——
        // 尤其是背景交叉淡入淡出期间旧图仍在 srBackgroundLast 上显示，早一步销毁会让淡出的那张凭空消失。
        private readonly Dictionary<string, Sprite> _dicRuntimeCreatedSprites = new Dictionary<string, Sprite>();

        /// <summary>
        /// 把地址解析到的 <see cref="Texture2D"/> 包成 <see cref="Sprite"/> 并登记，供 <see cref="UnloadAsset"/> 回收。
        /// 资源本身就是 Sprite 时直接返回，不登记也无需回收。
        /// </summary>
        private Sprite ResolveSprite(UnityEngine.Object asset, string assetAddress)
        {
            var sprite = asset as Sprite;
            if (sprite) return sprite;

            var texture = asset as Texture2D;
            if (!texture) return null;

            // pivot 是「相对 rect 归一化」的，不是像素坐标：中心点恒为 (0.5, 0.5)。
            // 背景那一路曾误传 (width * 0.5f, height * 0.5f)，等于把轴心推到上千个 rect 之外。
            sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            sprite.name = texture.name;

            if (!string.IsNullOrEmpty(assetAddress))
            {
                // 同地址重复创建时先回收上一个，避免登记表里的旧值被覆盖后再也够不着。
                if (_dicRuntimeCreatedSprites.TryGetValue(assetAddress, out var previous) && previous)
                    Destroy(previous);
                _dicRuntimeCreatedSprites[assetAddress] = sprite;
            }
            return sprite;
        }

        /// <summary>销毁并注销某地址上由本类创建的 Sprite。地址没有登记过时是空操作。</summary>
        private void DestroyRuntimeCreatedSprite(string assetAddress)
        {
            if (string.IsNullOrEmpty(assetAddress)) return;
            if (!_dicRuntimeCreatedSprites.TryGetValue(assetAddress, out var sprite)) return;

            _dicRuntimeCreatedSprites.Remove(assetAddress);
            if (sprite) Destroy(sprite);
        }

        /// <summary>
        /// 加载资源。委派给 <see cref="ToolkitAssets"/>：启用 ATK_ADDRESSABLE 时走 Addressables 异步加载，
        /// 否则回退 Resources（同步）。本方法在其之上做引用计数与「加载中被请求卸载」的簿记。
        /// <para>地址格式同样由 ATK_ADDRESSABLE 决定（启用时带文件夹路径与扩展名，否则是裸名），
        /// 与加载后端共用同一个宏，避免出现「完整路径 + Resources」这种必然取不到的组合。</para>
        /// </summary>
        /// <param name="assetAddress"></param>
        /// <param name="onAssetLoaded"></param>
        /// <typeparam name="T"></typeparam>
        private void LoadAsset<T>(string assetAddress, Action<T> onAssetLoaded) where T : UnityEngine.Object
        {
            // 检查 资源是否已经加载。
            // 注意判活方式：Dictionary 里存着键就会让 TryGetValue 返回 true，哪怕值是 null 或
            // 已被销毁的对象（Unity 的「假 null」——托管引用还在，但重载的 == 判定为 null）。
            // 这两种情况都必须当成未命中重新加载，否则该地址会被永久毒化、再也不会真正加载一次。
            if (_dicLoadedAssets.TryGetValue(assetAddress, out var loadedAsset))
            {
                if ((UnityEngine.Object)loadedAsset)
                {
                    // 增加 已加载资源计数器
                    _dicLoadedAssetCounter[assetAddress] = _dicLoadedAssetCounter.GetValueOrDefault(assetAddress, 0) + 1;
                    // 资源已经加载，直接调用回调
                    onAssetLoaded?.Invoke(loadedAsset as T);
                    return;
                }

                // 死条目：清掉它与配套计数，落到下面走一次真正的加载。
                _dicLoadedAssets.Remove(assetAddress);
                _dicLoadedAssetCounter.Remove(assetAddress);
            }

            // 该地址是否已有在途加载。必须在下面那行自增之前判断。
            bool alreadyLoading = _dicLoadingAssetCounter.ContainsKey(assetAddress);
            // 增加 资源加载计数器
            _dicLoadingAssetCounter[assetAddress] = _dicLoadingAssetCounter.GetValueOrDefault(assetAddress, 0) + 1;
            // 加载中，禁止跳过对话。
            // 注意：直接模式（未启用 ATK_ADDRESSABLE）下 ToolkitAssets 是同步回调，下面的 callback
            // 会在同一调用栈内把它改回 true——本守卫因而实际不生效，且每次请求都会把继续按钮
            // 关一次再开一次。接上真正的异步后端后语义自然恢复，故此处不改逻辑。
            SetContinueButtonActive(false);

            // ── 同址并发：只排队，不再向后端要第二次 ──────────────────────────────
            // 同一行里两个槽位配了同一个预制体时（3 个角色槽 + 3 个特效槽，很现实），
            // 两次请求都会在回调之前发生、都 miss _dicLoadedAssets。
            //
            // 后端（AddressableManager）自己也按地址去重，**不会**重复取 Addressable 句柄——
            // 但它的 RefCount 会被加两次，而本类的 UnloadAsset 只在自己的计数归零时释放**一次**，
            // 于是 RefCount 2 → 1，永远到不了 0：句柄与资源永久滞留。
            //
            // 故在这里确立不变式：**同一地址同时只有一次在途加载**。
            // 排队的请求不发新请求，但仍要各自累加 _dicLoadedAssetCounter（在排空时做），
            // 这样它们各自的 UnloadAsset 才配得平。
            if (alreadyLoading)
            {
                if (!_dicPendingCallbacks.TryGetValue(assetAddress, out var queue))
                {
                    queue = new List<Action<UnityEngine.Object>>();
                    _dicPendingCallbacks[assetAddress] = queue;
                }
                queue.Add(asset => onAssetLoaded?.Invoke(asset as T));
                return;
            }

            Action<T> callback = (asset) =>
            {
                // 本地址的**全部**并发请求共用这一次回调，故整条计数一次性清掉，不是减一。
                _dicLoadingAssetCounter.Remove(assetAddress);
                // 恢复允许跳过对话
                if (_dicLoadingAssetCounter.Count == 0)
                {
                    SetContinueButtonActive(true);
                }

                // 取出排队的请求。主请求 + 队列 = 本地址这一轮的全部请求数，
                // 后端只被请求过一次，所以它那边的引用计数也只有 1，下面按「一次」结算。
                _dicPendingCallbacks.TryGetValue(assetAddress, out var queued);
                _dicPendingCallbacks.Remove(assetAddress);

                // 加载是否成功。泛型形参 T 拿不到 UnityEngine.Object 的 bool 重载，必须先上转型再判空
                // （与 ToolkitAssets 内部同一写法）。ToolkitAssets 约定：加载失败时回调传入 null。
                bool succeeded = (UnityEngine.Object)asset;

                // 如果在加载过程中，有人请求卸载（pending unload），则立即卸载并不要把资源记录为已加载，也不要调用回调。
                var pendingCount = _dicPendingUnloadAfterLoad.GetValueOrDefault(assetAddress, 0);
                if (pendingCount > 0)
                {
                    // 消耗一个 pending 卸载请求
                    pendingCount -= 1;
                    if (pendingCount <= 0) _dicPendingUnloadAfterLoad.Remove(assetAddress);
                    else _dicPendingUnloadAfterLoad[assetAddress] = pendingCount;
                    // 卸载资源。仅在确实加载成功时才释放——失败时后端根本没持有句柄，
                    // 空放会让加载器的引用计数错位。
                    if (succeeded) ToolkitAssets.ReleaseAddress(assetAddress);

                    // 排队的请求同样按「已取消」处理：回传 null，让它们各自走失败分支。
                    // 不入缓存也不计数，故它们不会去扣一个不存在的计数。
                    InvokeQueued(queued, null);
                    return;
                }

                // 加载失败：不入缓存、不加计数、也不释放（没持有过句柄），但仍然回调，
                // 让调用方走自己的失败分支。若把 null 写进 _dicLoadedAssets，下次同地址会命中缓存
                // 直接回传 null，导致该地址再也不会重试加载。
                if (!succeeded)
                {
                    onAssetLoaded?.Invoke(null);
                    InvokeQueued(queued, null);
                    return;
                }

                // 记录已加载的资源
                _dicLoadedAssets[assetAddress] = asset;
                // 增加 已加载资源计数器：主请求 1 次 + 每个排队请求各 1 次。
                // 它们各自都会调一次 UnloadAsset，计数必须与请求数一致才配得平。
                _dicLoadedAssetCounter[assetAddress] =
                    _dicLoadedAssetCounter.GetValueOrDefault(assetAddress, 0) + 1 + (queued?.Count ?? 0);

                // 调用原始回调
                onAssetLoaded?.Invoke(asset);
                InvokeQueued(queued, asset);
            };

            ToolkitAssets.LoadByAddress(assetAddress, callback);
        }

        /// <summary>
        /// 排空同址并发的回调队列。逐个 try/catch：调用方的回调抛异常不应让后面排队的请求收不到结果，
        /// 更不应让上面刚记好的计数与实际回调数对不上。
        /// </summary>
        private static void InvokeQueued(List<Action<UnityEngine.Object>> queued, UnityEngine.Object asset)
        {
            if (queued == null) return;

            for (int i = 0; i < queued.Count; i++)
            {
                try { queued[i]?.Invoke(asset); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }
        
        /// <summary>
        /// 卸载资源。
        /// </summary>
        /// <param name="assetAddress"></param>
        private void UnloadAsset(string assetAddress)
        {
            if (string.IsNullOrEmpty(assetAddress)) return;
            
            // 检查 是否 已加载资源。这里只关心「该地址是否登记过」，取不到值也无所谓，故用 ContainsKey。
            if (_dicLoadedAssets.ContainsKey(assetAddress))
            {
                // 减少 已加载资源计数器
                _dicLoadedAssetCounter[assetAddress] = _dicLoadedAssetCounter.GetValueOrDefault(assetAddress, 1) - 1;
                if (_dicLoadedAssetCounter[assetAddress] <= 0)
                {
                    _dicLoadedAssetCounter.Remove(assetAddress);
                    // 先销毁本类为该地址创建的 Sprite，再放纹理句柄——顺序反了会留下一个指向已释放纹理的 Sprite
                    DestroyRuntimeCreatedSprite(assetAddress);
                    // 卸载资源
                    ToolkitAssets.ReleaseAddress(assetAddress);
                    // 从已加载资源表中移除
                    _dicLoadedAssets.Remove(assetAddress);
                }
            }
            // 检查 是否 正在加载资源。
            // 同上：同步回调下 _dicLoadingAssetCounter 在 LoadAsset 返回前就已清空，本分支
            // 与 LoadAsset 里消费 _dicPendingUnloadAfterLoad 的那段目前都到不了；接上异步后端后才活。
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
                            // 取基类而非 TextMeshProTypewriterEffect：面板挂的可能是
                            // UnityUITypewriterEffect（未装 TextMeshPro 时），取具体子类会漏掉它、调速静默失效。
                            var typewriter = panel.subtitleText.gameObject.GetComponent<AbstractTypewriterEffect>();
                            if (typewriter)
                                // 连同作者配置的标点停顿一起记下来当基准，原因见 FTypewriterEntry 的说明。
                                // 这里是全流程中唯一读这两个值的地方，且早于任何倍率写入。
                                _dialogueTypewriters.Add(new FTypewriterEntry
                                {
                                    Typewriter = typewriter,
                                    FullPauseBase = typewriter.fullPauseDuration,
                                    QuarterPauseBase = typewriter.quarterPauseDuration,
                                });
                            else
                                Debug.LogWarning($"剧情演出 >> 字幕面板 '{panel.name}' 的字幕文本组件上没有打字机组件，无法设置打字机速度。" +
                                                 $"请在该文本组件上添加 TextMeshPro Typewriter Effect（或 Unity UI Typewriter Effect）。");
                        }
                        else
                        {
                            Debug.LogWarning($"剧情演出 >> 字幕面板 '{panel.name}' 未配置字幕文本组件（subtitleText 为空），无法设置打字机速度。");
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
                var speed = -1f;
                ParseFloat(dialogueTypewriterSpeed, ref speed);
                if (speed > 0f)
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
        /// 是否正在加载演出资源。
        ///
        /// <para>自动播放与快进必须看这个门：它为 true 时继续按钮是被藏起来的，
        /// 语义就是「资源还没到位，别往下翻」。绕过按钮直接调 <c>OnContinueConversation()</c>
        /// 的话，这道守卫就作废了，会跳过尚未加载完的演出。</para>
        ///
        /// <para>⚠️ 未开启 <c>ATK_ADDRESSABLE</c> 的直接模式下 <c>ToolkitAssets</c> 是同步回调，
        /// 计数器在 <c>LoadAsset</c> 返回前就已清空，本属性实际恒为 false——
        /// 也就是说这道门<b>在直接模式下测不出来</b>，接上异步 Addressables 后端后要重新验证。</para>
        /// </summary>
        public bool IsLoadingAssets => !_continueButtonisActive;

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
        
        /// <summary>
        /// 一个打字机组件，连同它在预制体上**作者配置的**标点停顿基准值。
        ///
        /// <para>停顿基准必须在收集时缓存一次：之后本管理器会独占写入
        /// <c>fullPauseDuration</c> / <c>quarterPauseDuration</c>（按倍率缩放），
        /// 若等到倍率已经不是 1 时才第一次去读，读到的就是被缩放过的值——
        /// 基准一旦污染，此后每次变速都在错误的基数上再除一次，越调越离谱。</para>
        /// </summary>
        private struct FTypewriterEntry
        {
            public AbstractTypewriterEffect Typewriter;
            public float FullPauseBase;
            public float QuarterPauseBase;
        }

        // 如果你只需要快速访问所有的打字机组件。
        // 用基类装：TextMeshProTypewriterEffect 与 UnityUITypewriterEffect 都派生自它，
        // charactersPerSecond 与两个停顿时长也都声明在基类上，故两种实现都能被收集与调速。
        private readonly List<FTypewriterEntry> _dialogueTypewriters = new List<FTypewriterEntry>();

        // 当前这一行由剧本节点指定的速度倍率（字段标题 DialogueTypewriterSpeed）。
        // 与玩家侧的倍率相乘才是最终字速，故要留着——倍率中途变化时需要用它重算。
        private float _lineTypewriterSpeed = 1.0f;

        /// <summary>
        /// 设置 打字机的速度。<paramref name="speed"/> 是**剧本节点**给的倍率，
        /// 与玩家侧的 <see cref="VnPlaybackRate.Typewriter"/> 相乘。
        /// </summary>
        /// <param name="speed">速度倍率。默认为 1.0</param>
        private void SetAllTypewritersSpeed(float speed = 1.0f)
        {
            _lineTypewriterSpeed = speed;
            RefreshTypewriterSpeed();
        }

        /// <summary>
        /// 按当前倍率重刷全部打字机的字速与标点停顿。
        /// 切换播放速度档位、进出快进之后调用。
        ///
        /// <para><b>字速的写入时机很讲究。</b>Dialogue System 的打字循环里
        /// <c>goal = elapsed * charactersPerSecond</c>，<c>elapsed</c> 从行首累计，
        /// 而 <c>delay = 1 / charactersPerSecond</c> **只在起播时算一次**。所以在一行打到一半时
        /// 提高字速，既会因为 <c>goal</c> 突变而一次性蹦出一大段字，又会因为外层循环仍按老节奏轮转
        /// 而变成「每 0.1 秒吐 5 个字」的顿挫——后者比跳字更难看。
        /// 因此调用方应当只在**行边界**改字速；进入快进时先把当前行的打字机 <c>Stop()</c> 掉再推进，
        /// 于是字速永远只在「没有行正在打字」时被写。</para>
        ///
        /// <para><b>标点停顿则可以随时改。</b>它们是每次逐字循环重新读的，不会跳变。
        /// 而且不缩放的话速度档基本是假的：样例配的是句号停 1 秒、逗号停 0.3 秒的**绝对秒数**，
        /// 一句 30 字的中文台词打字 1.5 秒、标点却要停 1.6 秒——只调字速的话 3 档速实际只有 1.48 倍。</para>
        /// </summary>
        public void RefreshTypewriterSpeed()
        {
            var rate = VnPlaybackRate.Typewriter;
            var cps = _lineTypewriterSpeed * dialogueTypewriterPerSecond * rate;

            foreach (var entry in _dialogueTypewriters)
            {
                var tw = entry.Typewriter;
                if (!tw) continue;
                tw.charactersPerSecond = cps;
                tw.fullPauseDuration = entry.FullPauseBase / rate;
                tw.quarterPauseDuration = entry.QuarterPauseBase / rate;
            }
        }

        /// <summary>
        /// 立刻停掉所有字幕打字机，把本行文本一次显示完（等价于玩家点了一下「跳到全显」）。
        ///
        /// <para>进入快进时调用：这样字速就永远只在「没有行正在打字」时被改写，
        /// 避开 DS 打字循环中途变速的跳字与顿挫。详见 <see cref="RefreshTypewriterSpeed"/>。</para>
        /// </summary>
        public void StopAllTypewriters()
        {
            foreach (var entry in _dialogueTypewriters)
            {
                if (entry.Typewriter && entry.Typewriter.isPlaying) entry.Typewriter.Stop();
            }
        }

        /// <summary>当前是否还有字幕打字机在逐字显示。自动播放据此判断「本行文本是否已显示完」。</summary>
        public bool IsAnyTypewriterPlaying()
        {
            foreach (var entry in _dialogueTypewriters)
            {
                if (entry.Typewriter && entry.Typewriter.isPlaying) return true;
            }
            return false;
        }
        #endregion

        #region 头像切换
        [Header("对话-头像切换")]
#if ATK_ADDRESSABLE
        [Tooltip("对话-头像切换 文件夹路径")]
        [SerializeField] private string dialogueHeadAddressableFolder = "VNFrameworkDemo/Assets/ActorsHead/";
        [Tooltip("对话-头像切换 扩展名。通常为PNG格式")]
        [SerializeField] private string dialogueHeadExtension = ".png";
#endif
        [Tooltip("对话-头像切换 字段标题")]
        [SerializeField] private string dialogueHeadFieldTitle = "DialogueHead";

        // 当前对话头像资源地址（可能包含路径/扩展名）
        private string _dialogueHeadAssetName;
        // 上一个对话头像资源地址（用于卸载）
        private string _dialogueHeadAssetNameLast;
        // 当前头像**绑到了哪个 actor**。去重判据必须连它一起比——见 SetDialogueHeadImage 的说明。
        private string _dialogueHeadActorName;
        
        /// <summary>
        /// 设置 对话 头像图像
        /// </summary>
        /// <param name="headImageName"></param>
        /// <param name="actorName">Actor database name (used for SetActorPortraitSprite)</param>
        private void SetDialogueHeadImage(string headImageName, string actorName)
        {
            if (string.IsNullOrEmpty(actorName))
            {
                Debug.LogWarning("剧情演出 >> 设置对话头像时，actorName 为空，无法通过 DialogueManager 绑定头像。");
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
                _dialogueHeadActorName = null;
                // 通过 DialogueManager 将 头像图片 绑定到 actor 的 portrait（持久生效）
                DialogueManager.instance.SetActorPortraitSprite(actorName, null);
                return;
            }

            // 先构造地址再比对——比对必须发生在覆写 _dialogueHeadAssetNameLast 之前。
#if ATK_ADDRESSABLE
            var newAddress = $"{dialogueHeadAddressableFolder}{headImageName}{dialogueHeadExtension}";
#else
            var newAddress = headImageName;
#endif

            // 同一张头像 + 同一个角色：无事可做，直接返回。连续几行同一个人说话是最常见的写法。
            //
            // ⚠️ **必须连 actorName 一起比，只比地址会漏绑。**
            // SetActorPortraitSprite 是按 actor 绑定的：同一张图给另一个角色时仍然要重新绑一次，
            // 否则那个角色的头像就一直是空的。这是本条守卫最容易写错的地方。
            if (string.Equals(newAddress, _dialogueHeadAssetName) &&
                string.Equals(actorName, _dialogueHeadActorName)) return;

            // 记录上一个头像地址，切到当前的资源地址（如果使用Addressables会加上路径和扩展名）
            _dialogueHeadAssetNameLast = _dialogueHeadAssetName;
            _dialogueHeadAssetName = newAddress;
            _dialogueHeadActorName = actorName;
            LogVerbose($"剧情演出 >> 设置对话头像为 '{_dialogueHeadAssetName}'。");

            // 加载 头像图片
            LoadAsset<UnityEngine.Object>(_dialogueHeadAssetName, (asset) =>
            {
                // 处理Sprite或Texture2D两种情况
                var sprite = ResolveSprite(asset, _dialogueHeadAssetName);
                if (!sprite)
                {
                    Debug.LogWarning($"剧情演出 >> 无法加载对话头像 '{_dialogueHeadAssetName}'。请检查名称是否正确。");
                    // 失败也要卸载上一个头像：此处一度直接 return，把下面那段跳过去了，
                    // 而下一次切头像会覆写 _dialogueHeadAssetNameLast，那个地址的句柄就再也放不掉。
                    UnloadLastDialogueHeadImage();
                    return;
                }

                // 通过 DialogueManager 将 头像图片 绑定到 actor 的 portrait（持久生效）
                DialogueManager.instance.SetActorPortraitSprite(actorName, sprite);

                // 卸载 上一个头像图片
                UnloadLastDialogueHeadImage();
            });
        }
        
        /// <summary>卸载 上一个头像图片。加载成功与失败两条路径都必须走到，否则该地址的句柄会漏掉。</summary>
        private void UnloadLastDialogueHeadImage()
        {
            if (string.IsNullOrEmpty(_dialogueHeadAssetNameLast)) return;

            UnloadAsset(_dialogueHeadAssetNameLast);
            _dialogueHeadAssetNameLast = null;
        }

        /// <summary>
        /// 清除所有 对话 头像图像
        /// </summary>
        /// <returns></returns>
        private void ClearAllDialogueHeadImage()
        {
            // 立刻卸载 上一个头像图片
            UnloadLastDialogueHeadImage();
            // 卸载 当前头像图片
            if (!string.IsNullOrEmpty(_dialogueHeadAssetName))
            {
                UnloadAsset(_dialogueHeadAssetName);
                _dialogueHeadAssetName = null;
            }
            // 绑定记录一并清掉，否则清完之后再设同一张头像会被去重守卫误判为「已是当前」而不加载
            _dialogueHeadActorName = null;
        }
        #endregion
        #endregion
        
        #region 背景
        [Header("背景")]
        [Tooltip("背景图像组件 当前")]
        [SerializeField] private SpriteRenderer srBackgroundCurrent;
        [Tooltip("背景图像组件 上次（用于淡入淡出效果）")]
        [SerializeField] private SpriteRenderer srBackgroundLast;
        
#if ATK_ADDRESSABLE
        [Tooltip("背景资产的文件夹路径")]
        [SerializeField] private string backgroundAddressableFolder = "VNFrameworkDemo/Assets/Backgrounds/";
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
        // 清除所有背景图像的 延时句柄。值类型，无效句柄为 default。
        private ToolkitTweenHandle _tweenClearAllBackground;
        // 最近一次启动的背景切换协程。用于「启动新的之前先停掉上一条」——同一时刻多条协程
        // 会争写同一组渲染器。可能指向一条已结束的协程，对其 StopCoroutine 是空操作，无妨。
        private Coroutine _backgroundCoroutine;

        /// <summary>停掉在途的背景切换协程。其 finally 会顺带卸载上一张背景。</summary>
        private void StopBackgroundCoroutine()
        {
            if (_backgroundCoroutine == null) return;

            StopCoroutine(_backgroundCoroutine);
            _backgroundCoroutine = null;
        }

        /// <summary>卸载 上一张背景图像。正常结束与提前退出两类路径都必须走到。</summary>
        private void UnloadLastBackground()
        {
            if (string.IsNullOrEmpty(_backgroundAssetNameLast)) return;

            UnloadAsset(_backgroundAssetNameLast);
            _backgroundAssetNameLast = null;
        }
        
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
                // 本门面不做覆盖管理，先打断该目标上的在途补间再起新的
                VnTween.Kill(srBackgroundCurrent);
                VnTween.FadeSpriteRenderer(srBackgroundCurrent, 1f, backgroundFadeDuration, unscaled: false);
            }
            if (srBackgroundLast)
            {
                srBackgroundLast.gameObject.SetActive(true);
                VnTween.Kill(srBackgroundLast);
                VnTween.FadeSpriteRenderer(srBackgroundLast, 0f, backgroundFadeDuration, unscaled: false);
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
                VnTween.Kill(srBackgroundCurrent);
                VnTween.FadeSpriteRenderer(srBackgroundCurrent, 0f, backgroundFadeDuration, unscaled: false,
                    onComplete: () =>
                    {
                        srBackgroundCurrent.gameObject.SetActive(false);
                    });
            }
            if (srBackgroundLast)
            {
                VnTween.Kill(srBackgroundLast);
                VnTween.FadeSpriteRenderer(srBackgroundLast, 0f, backgroundFadeDuration, unscaled: false,
                    onComplete: () =>
                    {
                        srBackgroundLast.gameObject.SetActive(false);
                    });
            }
        }
        
        /// <summary>
        /// 清除所有 背景图像
        /// </summary>
        /// <returns></returns>
        private void ClearAllBackground()
        {
            float delay = backgroundFadeDuration;

            // 先停掉在途的切换协程：否则它会在这里清完之后继续跑，
            // 把刚隐藏的渲染器重新激活、重新贴图，背景「自己又亮回来」。
            StopBackgroundCoroutine();

            // 淡出背景图像
            if (srBackgroundCurrent)
            {
                VnTween.Kill(srBackgroundCurrent);
                VnTween.FadeSpriteRenderer(srBackgroundCurrent, 0f, delay, unscaled: false);
            }
            if (srBackgroundLast)
            {
                VnTween.Kill(srBackgroundLast);
                VnTween.FadeSpriteRenderer(srBackgroundLast, 0f, delay, unscaled: false);
            }
            _tweenClearAllBackground = VnTween.DelayedCall(delay, () =>
            {
                // 卸载背景图像资源
                UnloadLastBackground();
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

                _tweenClearAllBackground = default;
            }, unscaled: false);

            // 背景已经淡到 alpha 0 并隐藏，状态上等同于「已淡出」，标记必须一并清掉。
            // 否则下次 FadeInBackground 会在开头早退：backgroundFadeDuration 为 0 时
            // 背景被赋值却停在 alpha 0，画面上什么都看不见。
            _isBackgroundFadeIn = false;
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
            // 上次清除背景图像的延时未结束，强制完成（同步触发其回调）
            if (_tweenClearAllBackground.IsActive)
            {
                _tweenClearAllBackground.Complete();
                _tweenClearAllBackground = default;
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

            // 先构造地址再比对——比对必须发生在覆写 _backgroundAssetNameLast 之前，否则「上一张」会被写坏。
#if ATK_ADDRESSABLE
            // 使用Addressables时，添加文件夹路径
            var newAddress = $"{backgroundAddressableFolder}{backgroundName}{backgroundAddressableExtension}";
#else
            var newAddress = backgroundName;
#endif

            // 已经是这张背景：直接返回。连续几行配同一张背景是很常见的写法，
            // 不拦的话每行都会重走一遍「加载 → 计数增减 → 重启协程 → 对自己做一次交叉淡入」。
            //
            // ⚠️ 必须同时确认没有待触发的清理：ClearAllBackground 是**延时**执行的
            // （延时等于 backgroundFadeDuration）。若在那个窗口内又请求同一张背景就直接早退，
            // 稍后清理照样落下来，画面上背景就没了。此时不能早退，要正经重新走一遍。
            if (!_tweenClearAllBackground.IsActive && string.Equals(newAddress, _backgroundAssetName)) return;

            // 记录上个背景图像名称
            _backgroundAssetNameLast = _backgroundAssetName;
            // 记录背景图像名称
            _backgroundAssetName = newAddress;
            // 日志输出
            LogVerbose($"剧情演出 >> 设置背景图像为 '{_backgroundAssetName}'。");

            // 加载图像资源。把本次请求的地址一并带进回调：加载是异步的，回来时可能已经不是当前背景了。
            var requestedAddress = _backgroundAssetName;
            LoadAsset<Sprite>(requestedAddress, asset => OnBackgroundAssetLoaded(asset, requestedAddress));
        }

        /// <summary>
        /// 资源加载完成 背景图像
        /// </summary>
        /// <param name="asset">加载回来的资源。</param>
        /// <param name="requestedAddress">发起本次加载时的背景地址，用于判断回调是否已经过期。</param>
        private void OnBackgroundAssetLoaded(UnityEngine.Object asset, string requestedAddress)
        {
            // 迟到的回调：等待期间又切了别的背景，这一张已经不是当前要显示的了。
            // 若不拦，赢的会是「最后完成的」而不是「最后请求的」——快进时画面会停在中间某一张。
            // 它的引用计数在 LoadAsset 里已经加过，这里必须放掉，否则该地址永远留在表里。
            if (requestedAddress != _backgroundAssetName)
            {
                UnloadAsset(requestedAddress);
                return;
            }

            // Sprite 直接用；是 Texture2D 时包一层。
            // ⚠️ 后一路目前不可达：加载走的是 LoadAsset<Sprite>，泛型一直传到 Addressables.LoadAssetAsync<Sprite>，
            // 拿回来的只可能是 Sprite 或 null。这里保留只是防御——但**别为了「激活」它而把加载放宽成 <Object>**：
            // 那样取到的是纹理主资源，运行时包出的 Sprite 会丢掉导入器的 pixels-per-unit，背景尺寸会跟着变。
            // （此处一度自行 Sprite.Create 且把 pivot 传成了像素中心，pivot 其实是相对 rect 归一化的，见 ResolveSprite。）
            var image = ResolveSprite(asset, _backgroundAssetName);
            if (!image)
            {
                Debug.LogWarning($"剧情演出 >> 无法加载背景图像 '{_backgroundAssetName}'。请检查名称是否正确。");
                // 失败也要卸载上一张背景，否则下次换背景覆写 _backgroundAssetNameLast 后该句柄再也放不掉。
                UnloadLastBackground();
                return;
            }

            // 启动协程，设置背景图像。
            // 存句柄并先停掉在途的那条：跳过 / 快进时（VN 的常态）行推进得比 backgroundFadeDuration 快，
            // 会叠开多个协程同时写 srBackgroundCurrent.color 与 srBackgroundLast.sprite，最后谁写谁赢。
            StopBackgroundCoroutine();
            _backgroundCoroutine = StartCoroutine(SetBackgroundImageCoroutine(image));
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
            // 整个方法体包在 try/finally 里，只为一件事：无论从哪条路径退出，都必须卸载上一张背景。
            // 此前卸载写在方法末尾，而下面有两条 yield break 会绕过它（fadeDuration 为 0 的快路径、
            // 以及「单渲染器 + 无场景切换管理器」的路径）；一旦绕过，下次换背景就会覆写
            // _backgroundAssetNameLast，那个地址的句柄再也放不掉——fadeDuration 设 0 时每换一次漏一个。
            // finally 同时覆盖 StopCoroutine：Unity 停协程会 Dispose 迭代器，finally 照常执行。
            try
            {
                // 渲染器可能没配（其余四处 InitBackground / FadeIn / FadeOut / ClearAll 都做了守卫，唯独这里没有），
                // 也可能在协程在途时随场景被销毁，而本管理器是 DontDestroyOnLoad 的。
                if (!srBackgroundCurrent) yield break;

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
                        // 跨过 yield 之后渲染器可能已经没了
                        if (!srBackgroundCurrent) yield break;
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
                        // 每帧都要判活：本管理器 DontDestroyOnLoad，渲染器却随场景走
                        if (!srBackgroundCurrent || !srBackgroundLast) yield break;
                        var t = (elapsed / backgroundFadeDuration);
                        srBackgroundLast.color = new Color(1, 1, 1, 1 - t);
                        srBackgroundCurrent.color = new Color(1, 1, 1, t);
                        yield return null;
                        // 乘演出倍率：背景交叉淡是手写插值而不是补间，走不了 VnTween 那条路。
                        // 好处是它天然支持中途变速——快进按下去的当帧就开始按新倍率推进，不像补间要杀掉重起。
                        elapsed += Time.deltaTime * VnPlaybackRate.Playback;
                    }
                    if (!srBackgroundCurrent || !srBackgroundLast) yield break;
                    // 动画结束后，设置新背景为不透明，隐藏旧背景
                    srBackgroundLast.enabled = false;
                    srBackgroundCurrent.color = new Color(1, 1, 1, 1);
                }
            }
            finally
            {
                // 卸载上个背景图像资源。走到这里的路径包括正常结束、上面每一条 yield break，
                // 以及外部 StopCoroutine 导致的迭代器 Dispose。
                //
                // 注意这里不清 _backgroundCoroutine：fadeDuration 为 0 且加载同步返回时，整条协程会在
                // StartCoroutine 内部跑完，finally 先于外层那句赋值执行——在这里置空会立刻被覆盖回去。
                // 字段本就只用于「启动新的之前停掉上一条」，而对已结束的协程调 StopCoroutine 是空操作。
                UnloadLastBackground();
            }
        }
        #endregion
        #endregion

        #region 角色
        [Header("角色")]
#if ATK_ADDRESSABLE
        [Tooltip("角色资产 的文件夹路径")]
        [SerializeField] private string actorAddressableFolder = "VNFrameworkDemo/Assets/Actors/";
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
        // 延迟激活的Actor的 延时句柄列表
        private readonly List<ToolkitTweenHandle> _actorInitDelayTweens = new List<ToolkitTweenHandle>();
        
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
#if ATK_ADDRESSABLE
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
        /// 停止所有 延迟激活的Actor的延时。
        /// </summary>
        private void StopAllActorDelayTweens()
        {
            // 停止所有延迟激活的Actor的延时。
            // 倒序索引遍历而非 foreach：Kill 虽不触发回调，但与 CompleteAllBGMDelayTweens 保持同一写法，
            // 避免日后有人改成 Complete() 时踩到「遍历中回调改集合」的 InvalidOperationException。
            for (int i = _actorInitDelayTweens.Count - 1; i >= 0; i--)
            {
                _actorInitDelayTweens[i].Kill();
            }
            _actorInitDelayTweens.Clear();
        }
        
        /// <summary>
        /// 清除所有 角色 预制体。
        /// </summary>
        /// <returns></returns>
        private void ClearAllActors()
        {
            // 停止所有延迟激活的Actor的延时
            StopAllActorDelayTweens();

            // 卸载 所有角色预制体。
            // 先快照键：UnloadActorPrefab 会从 _mapActorAnimator 里移除条目，不能直接对字典遍历。
            var actorFieldTitleList = new List<string>(_mapActorAnimator.Keys);
            foreach (var actorFieldTitleName in actorFieldTitleList)
            {
                UnloadActorPrefab(actorFieldTitleName);
            }

            // 这里一度还有第二个循环，把所有实例同帧 Destroy 一遍——
            // 那正好抵消了 UnloadActorPrefab 刻意安排的延迟销毁（Spine 淡出、在途粒子播完再回收），
            // 对话结束这条路径上「不能把在途粒子拦腰截断」的取舍因此完全失效。已删除。
            // 资源计账不受影响：延时回调里的 UnloadAsset 本就带 `if (instance)` 守卫，照常执行。
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
            LogVerbose($"剧情演出 >> 加载Actor预制体 '{actorAssetPrefab}'。");
            
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
                
                // 实例化角色预制体
                var actorPrefabInstance = Instantiate(actorPrefab, this.transform);
                actorPrefabInstance.SetActive(false); // 设置非激活。等待 InitActorPrefab 激活（延时加载期间也靠它把实例藏住）

                // 获取 角色动画组件。缺失是受支持的降级形态（纯粒子特效即是）——位姿、激活与销毁照常走，
                // 只是没有淡入淡出与动画状态，故用 Log 而非 Warning。
                // 只在实例上取一次：此前预制体资产与实例上各取了一次，前一次的结果只用来判空。
                var actorAnimator = actorPrefabInstance.GetComponent<VnActorAnimator>();
                if (!actorAnimator)
                    LogVerbose($"剧情演出 >> Actor预制体 '{actorAssetPrefab}' 未挂载 VnActorAnimator，按普通预制体处理（无淡入淡出与动画状态）。");

                // 记录 角色预制体
                var actorAnimsLoadData = new FActorPrefabLoadData
                {
                    AssetAddress = actorAssetPrefabAddress,
                    PrefabAsset = actorPrefab,
                    PrefabInstance = actorPrefabInstance,
                    ActorAnimator = actorAnimator
                };
                _mapActorAnimator[actorFieldTitleName] = actorAnimsLoadData;
                
                if (delay > 0f)
                {
                    // 延迟设置角色预制体
                    // 使用单元素持有器引用句柄，避免闭包捕获外部被修改的变量。
                    var tweenHolder = new ToolkitTweenHandle[1];
                    tweenHolder[0] = VnTween.DelayedCall(delay, () =>
                    {
                        // 设置角色预制体
                        InitActorPrefab(actorAnimsLoadData, actorPosParam, actorRotateParam, actorScaleParam, actorAnimParam);
                        // 从延迟句柄列表中移除
                        _actorInitDelayTweens.Remove(tweenHolder[0]);
                    }, unscaled: false);
                    // 仅登记真正在途的句柄：delay ≤ 0 会同步跑完回调并返回空句柄，
                    // 此时上面的 Remove 已先于 Add 执行，不加守卫会留下永不移除的僵尸条目。
                    if (tweenHolder[0].IsActive) _actorInitDelayTweens.Add(tweenHolder[0]);
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
            if (!_mapActorAnimator.TryGetValue(actorFieldTitleName, out var actorPrefabLoadData)) return;

            // 先从映射表移除：下面的销毁可能带延时，期间不应再被淡入淡出等全表遍历扫到
            _mapActorAnimator.Remove(actorFieldTitleName);

            // 提出到局部变量：闭包捕获结构体字段会把整个结构体一起装进闭包类
            var assetAddress = actorPrefabLoadData.AssetAddress;

            if (actorPrefabLoadData.ActorAnimator)
            {
                // 销毁 角色预制体的实例。延迟激活、尚未激活的会直接销毁。
                actorPrefabLoadData.ActorAnimator.ExecuteDestroy(() =>
                {
                    // 卸载 角色预制体资源
                    UnloadAsset(assetAddress);
                });
                return;
            }

            // 无 VnActorAnimator 的普通预制体：自己销毁实例并卸载资源。
            // 此前这里是 ActorAnimator?.ExecuteDestroy(...)，组件缺失时整条链被跳过
            // ——实例永不销毁、UnloadAsset 永不调用（Addressable 句柄泄漏），却已经从映射表里摘掉了。
            var instance = actorPrefabLoadData.PrefabInstance;

            // 实例已不在或未激活：直接销毁，没有在途表现要收尾
            if (!instance || !instance.activeSelf)
            {
                if (instance) Destroy(instance);
                UnloadAsset(assetAddress);
                return;
            }

            // 与 VnActorAnimator.ExecuteDestroy 同一套收尾：先停粒子发射，等在途粒子自然播完再销毁。
            // 「直接做一个自动播放的粒子特效预制体」是受支持的用法，不能因为它没挂组件就被拦腰截断。
            if (!VnActorAnimator.StopParticlesAndGetDelay(instance, out var delayParticle))
            {
                // 没有粒子系统（例如纯图片预制体）：无需等待
                Destroy(instance);
                UnloadAsset(assetAddress);
                return;
            }

            // 刻意不传 owner：要销毁的正是这个实例，绑定生命周期会让回调随对象一起被丢弃，
            // 而 UnloadAsset 靠这个回调执行。与 VnActorAnimator.ExecuteDestroy 的取舍一致。
            VnTween.DelayedCall(delayParticle, () =>
            {
                if (instance) Destroy(instance);
                UnloadAsset(assetAddress);
            }, unscaled: false);
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
            if (!actorPrefabLoadData.PrefabInstance) return;

            // 获取 角色预制体 实例的 Transform
            var actorTrans = actorPrefabLoadData.PrefabInstance.transform;

            // 角色位置、旋转、缩放。默认 为 预制体 当前值
            ParseVector3AndFloat(actorPosParam, out var toPos, out var _, actorTrans.position);
            ParseVector3AndFloat(actorRotateParam, out var toRot, out var _, actorTrans.rotation.eulerAngles);
            ParseVector3AndFloat(actorScaleParam, out var toScale, out var _, actorTrans.localScale);
            // 角色动画组。字段缺失（null）时传 null，表示沿用预制体自身配置的初始状态；
            // 字段存在但为空时传空数组，表示明确不进入任何状态。判据与 SetActorPrefab 一致
            // ——ParseStringArray 对两者都返回空数组，不看原串就区分不出来。
            ParseStringArray(actorAnimParam, out var toStateArray);

            if (actorPrefabLoadData.ActorAnimator)
            {
                // 交给动画组件：它会落位、激活，并在动画播放器就绪后淡入与切换状态
                actorPrefabLoadData.ActorAnimator.ExecuteInit(
                    toPos, toRot, toScale, actorAnimParam == null ? null : toStateArray);
                return;
            }

            // 无 VnActorAnimator 的普通预制体（如纯粒子特效）：自己落位并激活。
            // 与 FadeIn/FadeOutActorsAndEffects 的降级分支同一套判据。
            // 此前这整段都包在组件判空里，而实例在 LoadActorPrefab 中被 SetActive(false)，
            // 于是缺组件的预制体加载后没有任何代码把它激活回来——两个 Demo 特效正是这种。
            // 先禁用再落位再激活：让粒子等由 OnEnable 驱动的组件从头开始，也保证不会在旧位置上被渲染一帧。
            if (actorPrefabLoadData.PrefabInstance.activeSelf)
                actorPrefabLoadData.PrefabInstance.SetActive(false);
            actorTrans.SetPositionAndRotation(toPos, Quaternion.Euler(toRot));
            actorTrans.localScale = toScale;
            actorPrefabLoadData.PrefabInstance.SetActive(true);
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
            if (!actorPrefabLoadData.PrefabInstance) return;

            // 获取 角色预制体 实例的 Transform
            var actorTrans = actorPrefabLoadData.PrefabInstance.transform;

            // 设置 角色预制体 实例的位置、缩放、动画
            if (actorPrefabLoadData.ActorAnimator)
            {
                // 完成之前的 位置、缩放 插值动画
                actorPrefabLoadData.ActorAnimator.CompleteTransformTween();

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
                return;
            }

            // 无 VnActorAnimator 的普通预制体：没有补间能力，直接瞬置（参数里的速度倍率对它无意义）。
            // 这条路不是边角——对话行只要没写 ActorNPrefab 字段，就会走到 SetActorPrefab
            // （Field.LookupValue 对缺失字段返回 null，`== ""` 判定为假），
            // 即「保留该槽位的角色、只改位姿」正是最常见的写法。
            if (!actorPrefabLoadData.PrefabInstance.activeSelf)
                actorPrefabLoadData.PrefabInstance.SetActive(true);
            if (actorPosParam != null)
            {
                ParseVector3AndFloat(actorPosParam, out var toPos, out var _, actorTrans.position, 1f);
                actorTrans.position = toPos;
            }
            if (actorRotateParam != null)
            {
                ParseVector3AndFloat(actorRotateParam, out var toRot, out var _, actorTrans.rotation.eulerAngles, 1f);
                actorTrans.eulerAngles = toRot;
            }
            if (actorScaleParam != null)
            {
                ParseVector3AndFloat(actorScaleParam, out var toScale, out var _, actorTrans.localScale, 1f);
                actorTrans.localScale = toScale;
            }
            // 角色动画组：无动画播放器，忽略
        }
        #endregion
        
        #region 角色、特效 淡入淡出
        /// <summary>
        /// 淡出 所有角色和特效。
        /// 若预制体带有 VnActorAnimator 则执行淡出动画；
        /// 否则为普通预制体，直接设置非激活（可能只是暂时被隐藏，之后还需恢复）。
        /// </summary>
        private void FadeOutActorsAndEffects()
        {
            foreach (var kvp in _mapActorAnimator)
            {
                var data = kvp.Value;
                if (data.ActorAnimator)
                {
                    // 有 VnActorAnimator，尝试淡出
                    bool handled = data.ActorAnimator.FadeOut();
                    if (!handled && data.PrefabInstance)
                    {
                        // VnActorAnimator 无法淡出（无Spine/粒子），降级为设置非激活
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
        /// 若预制体带有 VnActorAnimator 则执行淡入动画；
        /// 否则为普通预制体，直接设置激活。
        /// </summary>
        private void FadeInActorsAndEffects()
        {
            foreach (var kvp in _mapActorAnimator)
            {
                var data = kvp.Value;
                if (data.ActorAnimator)
                {
                    // 有 VnActorAnimator，尝试淡入
                    bool handled = data.ActorAnimator.FadeIn();
                    if (!handled && data.PrefabInstance)
                    {
                        // VnActorAnimator 无法淡入（无Spine/粒子），降级为设置激活
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
#if ATK_ADDRESSABLE
        [Tooltip("场景特效的 文件夹路径")]
        [SerializeField] private string effectAddressableFolder = "VNFrameworkDemo/Assets/Effects/";
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
#if ATK_ADDRESSABLE
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
        // 记录 音频-字段标题 到 (音频Key, 音频类别) 的映射表。进入到新的对话行时，停止所有上个对话行播放的音频。
        // 之所以连类别一起记：环境音 / 音效 / 语音共用这张表，而停止时要把正确的类别回传给后端。
        private readonly Dictionary<string, (string Key, EVnAudioCategory Category)> _dicSfxFieldTitleToAudioKey
            = new Dictionary<string, (string, EVnAudioCategory)>();
        // 记录 背景音乐 延迟播放的 延时句柄。
        private readonly List<ToolkitTweenHandle> _bgmDelayTweens = new List<ToolkitTweenHandle>();
        // 记录 音效音频 延迟播放的 延时句柄。
        private readonly List<ToolkitTweenHandle> _sfxDelayTweens = new List<ToolkitTweenHandle>();
        
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
                PlaySfxByParam(EVnAudioCategory.Ambient, audioFieldTitle, audioAmbientParam);
            }
            // 音效
            for (int i = 0; i < audioSfxFieldTitle.Length; i++)
            {
                var audioFieldTitle = audioSfxFieldTitle[i];
                var audioSfxParam = Field.LookupValue(subtitle.dialogueEntry.fields, audioFieldTitle);
                PlaySfxByParam(EVnAudioCategory.Sfx, audioFieldTitle, audioSfxParam);
            }
            // 语音
            for (int i = 0; i < audioVoiceFieldTitle.Length; i++)
            {
                var audioFieldTitle = audioVoiceFieldTitle[i];
                var audioVoiceParam = Field.LookupValue(subtitle.dialogueEntry.fields, audioFieldTitle);
                PlaySfxByParam(EVnAudioCategory.Voice, audioFieldTitle, audioVoiceParam);
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
                // 判据是 < 0 而不是 <= 0：解析器的「没配」哨兵是 -1，0 是一个合法取值。
                // 用 <= 0 的话「音量 0」（本意静音）会被当成没配、反而放成满音量；负音调（倒放）也表达不出来。
                volume = volume < 0f ? 1f : volume; // 音量 默认 1.0f
                pitch = Mathf.Approximately(pitch, -1f) ? 1f : pitch; // 音调 默认 1.0f。影响 播放速度 和 音高。
                // 延迟播放
                if (delay > 0f)
                {
                    var tweenHolder = new ToolkitTweenHandle[1];
                    tweenHolder[0] = VnTween.DelayedCall(delay, () =>
                    {
                        // 延迟时间到，播放背景音乐。循环播放
                        VnStoryAudio.PlayWithChannel(EVnAudioCategory.Bgm, audioFieldTitle, audioKey, volume, pitch);
                        // 记录 背景音乐Key
                        _dicBgmFieldTitleToAudioKey[audioFieldTitle] = audioKey;
                        // 从 延迟播放的 句柄列表中 移除
                        _bgmDelayTweens.Remove(tweenHolder[0]);
                    }, unscaled: false);
                    // 记录 延迟播放的 句柄。仅登记真正在途的（见 LoadActorPrefab 处的同款守卫说明）
                    if (tweenHolder[0].IsActive) _bgmDelayTweens.Add(tweenHolder[0]);
                }
                else
                {
                    // 立即播放背景音乐。循环播放
                    VnStoryAudio.PlayWithChannel(EVnAudioCategory.Bgm, audioFieldTitle, audioKey, volume, pitch);
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
            VnStoryAudio.StopWithChannel(EVnAudioCategory.Bgm, audioFieldTitle);
        }
        
        /// <summary>
        /// 立即完成 所有背景音乐 延迟播放的 延时。
        /// </summary>
        private void CompleteAllBGMDelayTweens()
        {
            // 立即完成 背景音乐 延迟播放的 延时。
            // 必须倒序索引遍历，不能用 foreach：Complete() 是同步的，会就地触发回调，
            // 而回调里有 _bgmDelayTweens.Remove(...)，foreach 的枚举器会因版本号失配抛
            // InvalidOperationException（即使只有一个元素也会抛，返回 false 的那次 MoveNext 同样校验版本）。
            for (int i = _bgmDelayTweens.Count - 1; i >= 0; i--)
            {
                _bgmDelayTweens[i].Complete();
            }
            _bgmDelayTweens.Clear();
        }

        /// <summary>
        /// 清除 所有背景音乐。
        /// </summary>
        private void ClearAllBGM()
        {
            // 立即打断 背景音乐 延迟播放的 延时（不触发回调，故延迟的BGM不会再播）
            for (int i = _bgmDelayTweens.Count - 1; i >= 0; i--)
            {
                _bgmDelayTweens[i].Kill();
            }
            _bgmDelayTweens.Clear();
            // 停止所有 记录的 背景音乐通道
            // 用通道路径停、而不是 Key 路径：BGM 是经 PlayWithChannel(Bgm, 字段标题, …) 播出的，
            // 字典的 Key 才是通道名（Value 是音频 Key）。IVnAudioBackend 明确区分这两条路径，
            // 按 Key 停一条按通道播出的 BGM，在任何合规后端上都停不掉——当前是空后端，故听不出来。
            foreach (var kvp in _dicBgmFieldTitleToAudioKey)
            {
                VnStoryAudio.StopWithChannel(EVnAudioCategory.Bgm, kvp.Key);
            }
            _dicBgmFieldTitleToAudioKey.Clear();
        }
        
        /// <summary>
        /// 播放 音频。
        /// </summary>
        /// <param name="category">音频类别。环境音 / 音效 / 语音共用本方法，类别用于回传给音频后端。</param>
        /// <param name="audioFieldTitle">音频 字段标题。用于确定槽位。</param>
        /// <param name="audioParam">音频参数 内容。</param>
        private void PlaySfxByParam(EVnAudioCategory category, string audioFieldTitle, string audioParam)
        {
            // 解析音频参数，获取音频Key和延迟时间
            if (ParseStringAndThreeFloat
            (
                audioParam, out var audioKey, 
                out var volume,out var pitch, out var delay
            ))
            {
                // 默认参数
                // 判据是 < 0 而不是 <= 0：解析器的「没配」哨兵是 -1，0 是一个合法取值。
                // 用 <= 0 的话「音量 0」（本意静音）会被当成没配、反而放成满音量；负音调（倒放）也表达不出来。
                volume = volume < 0f ? 1f : volume; // 音量 默认 1.0f
                pitch = Mathf.Approximately(pitch, -1f) ? 1f : pitch; // 音调 默认 1.0f。影响 播放速度 和 音高。
                // 延迟播放
                if (delay > 0f)
                {
                    // 语音的延迟要单独计数：延迟期间它还没进 _dicSfxFieldTitleToAudioKey，
                    // 自动播放若只看那张表会以为「本行没有语音」，在语音响起之前就把台词翻过去。
                    bool isVoice = category == EVnAudioCategory.Voice;
                    if (isVoice) _pendingVoiceDelayCount++;

                    var tweenHolder = new ToolkitTweenHandle[1];
                    tweenHolder[0] = VnTween.DelayedCall(delay, () =>
                    {
                        if (isVoice && _pendingVoiceDelayCount > 0) _pendingVoiceDelayCount--;
                        // 延迟 播放音频。音调在此刻现算，而不是排延时的时候——
                        // 延时本身会被倍率压缩，等它到期时倍率可能已经变了。
                        VnStoryAudio.Play(category, audioKey, volume, EffectiveAudioPitch(category, pitch));
                        // 记录 音频Key 与类别
                        _dicSfxFieldTitleToAudioKey[audioFieldTitle] = (audioKey, category);
                        // 从 延迟播放的 句柄列表中 移除
                        _sfxDelayTweens.Remove(tweenHolder[0]);
                    }, unscaled: false);
                    // 记录 延迟播放的 句柄。仅登记真正在途的（见 LoadActorPrefab 处的同款守卫说明）
                    if (tweenHolder[0].IsActive) _sfxDelayTweens.Add(tweenHolder[0]);
                }
                else
                {
                    // 立即 播放音频
                    VnStoryAudio.Play(category, audioKey, volume, EffectiveAudioPitch(category, pitch));
                    // 记录 音频Key 与类别
                    _dicSfxFieldTitleToAudioKey[audioFieldTitle] = (audioKey, category);
                }
            }
        }
        
        /// <summary>
        /// 清除 所有音频。环境音、音效、语音。
        /// </summary>
        private void ClearAllSfx()
        {
            // 停止所有 延迟播放的 音频
            // 跳过对话后，音频也不再播放。Kill 不触发回调，故延迟的音频不会再播。
            for (int i = _sfxDelayTweens.Count - 1; i >= 0; i--)
            {
                _sfxDelayTweens[i].Kill();
            }
            _sfxDelayTweens.Clear();
            // 停止所有 记录的 音频Key
            foreach (var kvp in _dicSfxFieldTitleToAudioKey)
            {
                VnStoryAudio.Stop(kvp.Value.Category, kvp.Value.Key);
            }
            _dicSfxFieldTitleToAudioKey.Clear();
            // 延时被 Kill 掉了，回调不会执行，计数只能在这里归零
            _pendingVoiceDelayCount = 0;
        }

        #region 语音状态
        // 已排期但尚未响起的语音条数。见 PlaySfxByParam 里的说明。
        private int _pendingVoiceDelayCount;

        /// <summary>
        /// 本行是否还有语音「没播完」——包括**已排期但还没响起**的延迟语音。
        /// 自动播放据此决定要不要继续等。
        ///
        /// <para>后端没有实现 <see cref="IVnAudioPlaybackInfo"/> 时，
        /// <see cref="VnStoryAudio.IsPlaying"/> 恒返回 false（并告警一次），
        /// 于是本方法只剩「延迟尚未到期」这一路判断，自动播放退化为
        /// 「打字机结束 + 停留时长」——这正是想要的降级方向。</para>
        /// </summary>
        public bool IsLineVoicePlaying()
        {
            if (_pendingVoiceDelayCount > 0) return true;

            foreach (var kvp in _dicSfxFieldTitleToAudioKey)
            {
                if (kvp.Value.Category != EVnAudioCategory.Voice) continue;
                if (VnStoryAudio.IsPlaying(EVnAudioCategory.Voice, kvp.Value.Key)) return true;
            }
            return false;
        }

        /// <summary>
        /// 把演出倍率作用到<b>已经在播</b>的语音上。新播的语音由
        /// <see cref="EffectiveAudioPitch"/> 在播放瞬间直接算进 pitch，不走这里。
        /// </summary>
        private void ApplyPlaybackRateToVoice(float rate)
        {
            foreach (var kvp in _dicSfxFieldTitleToAudioKey)
            {
                if (kvp.Value.Category != EVnAudioCategory.Voice) continue;
                VnStoryAudio.SetPlaybackRate(EVnAudioCategory.Voice, kvp.Value.Key, rate);
            }
        }

        /// <summary>
        /// 播放时的实际音调 = 剧本配的音调 × 演出倍率（**仅语音**）。
        ///
        /// <para>只给语音倍速，是因为 pitch 变速必然变调：BGM 与环境音一旦被拉成 5 倍速就成了噪音，
        /// 主流 VN 的快进也不会去动音乐。需求里点名要倍速的也只有「语音播放速度」。</para>
        /// </summary>
        private static float EffectiveAudioPitch(EVnAudioCategory category, float pitch)
            => category == EVnAudioCategory.Voice ? pitch * VnPlaybackRate.Playback : pitch;
        #endregion
        #endregion
        #endregion

        #region 游戏玩法注册
        // Key=Fields的Title名称, Value=玩法系统的回调方法
        private readonly Dictionary<string, Action<string>> _gameplaySystemRegistry = new Dictionary<string, Action<string>>();
        // 遍历用的快照缓冲。回调是宿主代码，可能在其中增删注册项，不能直接对字典 foreach。
        private readonly List<KeyValuePair<string, Action<string>>> _gameplaySystemScratch =
            new List<KeyValuePair<string, Action<string>>>();

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
                Debug.LogWarning("[VnStoryManager] RegisterGameplaySystem >> fieldTitle 不能为空。");
                return;
            }
            if (callback == null)
            {
                Debug.LogWarning($"[VnStoryManager] RegisterGameplaySystem >> 注册 '{fieldTitle}' 时，callback 不能为 null。");
                return;
            }

            if (_gameplaySystemRegistry.ContainsKey(fieldTitle))
            {
                Debug.LogWarning($"[VnStoryManager] RegisterGameplaySystem >> '{fieldTitle}' 已经注册过了。将覆盖之前的注册。");
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
            if (_gameplaySystemRegistry.Count == 0) return;

            // 先快照再遍历：下面调的是宿主工程的回调，它完全可能在里面
            // RegisterGameplaySystem / UnregisterGameplaySystem（比如小游戏结束后登记后续处理器），
            // 直接对字典 foreach 会当场抛 InvalidOperationException。
            _gameplaySystemScratch.Clear();
            foreach (var kvp in _gameplaySystemRegistry) _gameplaySystemScratch.Add(kvp);

            foreach (var kvp in _gameplaySystemScratch)
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
            _gameplaySystemScratch.Clear();
        }
        #endregion
        
        #region 字符串参数解析

        /// <summary>
        /// 解析剧本里的一个数值。所有数值参数都必须经过这里。
        ///
        /// <para><b>不变文化</b>：剧本里的数字是<b>数据</b>而不是界面文本，绝不能跟随运行机器的区域设置。
        /// 用不带 <c>IFormatProvider</c> 的重载时，逗号小数点区域（de / fr / ru / es / pt-BR 等）下
        /// <c>"1.5"</c> 会解析失败——角色瞬移到原点、缩放塌成 0、延迟消失，而且不报任何错。
        /// Pixel Crushers 自己也是这么处理的（见其 <c>ConversationView</c>）。</para>
        ///
        /// <para><b>失败时不改写</b>：<c>float.TryParse</c> 失败会把 out 参数写成 0，
        /// 那会冲掉调用方预先设好的默认值（哨兵 -1）。故这里用 <c>ref</c>，只在解析成功时才赋值。</para>
        /// </summary>
        private static void ParseFloat(string text, ref float value)
        {
            if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                value = parsed;
        }

        /// <summary>
        /// 解析字符串为字符串数组。
        /// </summary>
        /// <param name="arrayString"></param>
        /// <param name="values"></param>
        /// <returns></returns>
        private static void ParseStringArray(string arrayString, out string[] values)
        {
            values = Array.Empty<string>();
            if (string.IsNullOrEmpty(arrayString)) return;

            // 按 | 分割
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
        private static void ParseStringAndFloat(string paramString, out string outString, out float outFloat,
            float defaultFloat = -1f)
        {
            outString = null;
            outFloat = defaultFloat;
            if (string.IsNullOrEmpty(paramString)) return;

            // 按 | 分割
            var values = paramString.Split('|', StringSplitOptions.RemoveEmptyEntries);
            if (values.Length >= 2)
            {
                outString = values[0];
                ParseFloat(values[1], ref outFloat);
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
        private static void ParseVector3AndFloat(string paramString, out Vector3 outVector3, out float outFloat,
            Vector3 defaultVec3, float defaultFloat = -1f)
        {
            outVector3 = defaultVec3;
            outFloat = defaultFloat;
            if (string.IsNullOrEmpty(paramString)) return;

            // 按 | 分割
            var values = paramString.Split('|', StringSplitOptions.RemoveEmptyEntries);
            if (values.Length >= 4)
            {
                ParseFloat(values[0], ref outVector3.x);
                ParseFloat(values[1], ref outVector3.y);
                ParseFloat(values[2], ref outVector3.z);
                ParseFloat(values[3], ref outFloat);
            }
            else if (values.Length == 3)
            {
                ParseFloat(values[0], ref outVector3.x);
                ParseFloat(values[1], ref outVector3.y);
                ParseFloat(values[2], ref outVector3.z);
            }
            else if (values.Length == 2)
            {
                ParseFloat(values[0], ref outVector3.x);
                ParseFloat(values[1], ref outVector3.y);
            }
            else if (values.Length == 1)
            {
                ParseFloat(values[0], ref outVector3.x);
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
        private static bool ParseStringAndThreeFloat(string paramString, out string outString, out float outFloat1, out float outFloat2, out float outFloat3)
        {
            outString = null;
            // 哨兵 -1：调用方据此区分「没配」与「配了 0」。解析失败时 ParseFloat 不改写，哨兵得以保留。
            outFloat1 = -1f;
            outFloat2 = -1f;
            outFloat3 = -1f;
            if (string.IsNullOrEmpty(paramString)) return false;

            // 按 | 分割
            var values = paramString.Split('|', StringSplitOptions.RemoveEmptyEntries);
            if (values.Length >= 4)
            {
                outString = values[0];
                ParseFloat(values[1], ref outFloat1);
                ParseFloat(values[2], ref outFloat2);
                ParseFloat(values[3], ref outFloat3);
                return true;
            }
            else if (values.Length == 3)
            {
                outString = values[0];
                ParseFloat(values[1], ref outFloat1);
                ParseFloat(values[2], ref outFloat2);
                return true;
            }
            else if (values.Length == 2)
            {
                outString = values[0];
                ParseFloat(values[1], ref outFloat1);
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
#if ATK_LOCALIZATION
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
        /// 多语言 变更 事件处理。把 Unity Localization 选中的语言代码同步给 Dialogue System。
        ///
        /// <para>⚠️ 这里的 <c>Localization</c> 是 <c>PixelCrushers.DialogueSystem.Localization</c>，
        /// 不是 <c>UnityEngine.Localization</c>。给它赋值有个从调用点看不出来的副作用：
        /// DS 的 setter 会在 DialogueManager 物体上<b>自动挂一个 <c>UILocalizationManager</c> 组件</b>
        /// （若尚未存在），并把它的 currentLanguage 一并设过去。</para>
        ///
        /// <para>Unity Localization 只负责提供这个语言代码字符串；真正的取值全在 DS 内部完成——
        /// 对白按<b>裸语言代码</b>字段查找，其余字段按「标题 + 空格 + 语言代码」查找。</para>
        /// </summary>
        /// <param name="locale">变更后的语言。为 null 时（如初始化时的主动调用）从 LocalizationSettings 现取。</param>
        protected virtual void OnSelectedLocaleChanged(Locale locale)
        {
            // 可用语言表为空、或 Localization 尚未初始化完成时，SelectedLocale 会是 null，
            // 直接取 .Identifier.Code 会抛 NRE。取不到语言代码就维持 DS 的现状（默认语言）。
            var code = (locale ?? LocalizationSettings.SelectedLocale)?.Identifier.Code;
            if (string.IsNullOrEmpty(code)) return;

            // 更新 当前语言 代码
            Localization.language = code;
        }
#endif
        #endregion

        #region 变量注册
        // Key变量名:Value变量值的获取方法
        private readonly Dictionary<string, Func<object>> _variableGetterRegistryMap = new Dictionary<string, Func<object>>();
        // 遍历用的快照缓冲，理由同 _gameplaySystemScratch。
        private readonly List<KeyValuePair<string, Func<object>>> _variableGetterScratch =
            new List<KeyValuePair<string, Func<object>>>();

        /// <summary>
        /// 注册 变量与变量值获取器
        /// </summary>
        /// <param name="variableName">DialogueSystem中的变量名</param>
        /// <param name="valueGetter">变量值获取器</param>
        public void RegisterVariableGetter(string variableName, Func<object> valueGetter)
        {
            if (_variableGetterRegistryMap.ContainsKey(variableName))
            {
                Debug.LogWarning($"[VnStoryManager] RegisterVariable >> 变量名 '{variableName}' 已经注册过了。将覆盖之前的注册。");
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
            if (_variableGetterRegistryMap.Count == 0) return;

            // 同 OnConversationLineGamePlaySystem：getter 是宿主代码，可能在其中调 RegisterVariableGetter，
            // 直接对字典 foreach 会抛 InvalidOperationException。先快照。
            _variableGetterScratch.Clear();
            foreach (var kvp in _variableGetterRegistryMap) _variableGetterScratch.Add(kvp);

            foreach (var kvp in _variableGetterScratch)
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
            _variableGetterScratch.Clear();
        }
        #endregion
    }
}