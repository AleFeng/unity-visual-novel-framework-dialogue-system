using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using Ale.Toolkit.Runtime;
using Ale.AnimSimulatorSystem;

namespace Ale.VnFramework
{
    /// <summary>
    /// 游戏角色 控制器 动画。
    /// 动画后端由 com.ale.animsimulatorsystem 的 AnimatorBase 抽象，
    /// 挂 SpineAnimator 还是 Live2DAnimator 由预制体决定，本类不关心。
    /// </summary>
    public class VnActorAnimator : MonoBehaviour
    {
#if UNITY_EDITOR
        private void Reset()
        {
            // 获取 动画播放器。FindFor 取包内三种查找顺序的并集，且含未激活对象
            if (!m_ActorAnimator)
            {
                m_ActorAnimator = AnimatorBase.FindFor(this);
            }
        }
#endif
        
        private void Start()
        {
            // 自动初始化：用于「直接摆进场景、不经 VnStoryManager 驱动」的预制体。
            // 状态列表传 null —— 初始状态的唯一来源是动画播放器自身的 StateInitList。
            // 本方法与 AnimatorBase.Start 的先后是未定义的，靠就绪信号消化：
            // 先跑到这里就登记等待，后跑到这里就立即补发。
            if (m_AutoInit)
                ExecuteInit(transform.position, transform.eulerAngles, transform.localScale, null);
        }

        #region 初始化、销毁
        [Tooltip("自动初始化")]
        [SerializeField] private bool m_AutoInit;

        // 等待动画播放器就绪期间，暂存的目标状态列表
        private string[] _pendingInitStates;

        /// <summary>
        /// 初始化：落位 → 激活 → 在动画播放器就绪后淡入并切到目标状态。
        /// </summary>
        /// <param name="toPos">目标位置（世界坐标）。</param>
        /// <param name="toRot">目标旋转（欧拉角）。</param>
        /// <param name="toScale">目标缩放（本地）。</param>
        /// <param name="toStateArray">
        /// 目标状态列表。<c>null</c> = 沿用动画播放器自身配置的初始状态；空数组 = 明确不进入任何状态。
        /// </param>
        public void ExecuteInit(Vector3 toPos, Vector3 toRot, Vector3 toScale, string[] toStateArray)
        {
            // 撤掉上一轮遗留的单条动画。状态动画不在此处理——它们由下面的差集切换负责，
            // 差集切换比「全清再重放」更精准：同名循环动画不会被重头拉起。
            StopAllAnims();

            // 先掐掉在途的位移 / 旋转 / 缩放补间。初始化是硬落位，不能让上一行对话遗留的
            // SetToPosition 补间在随后的帧里把这里写的位姿覆盖回它自己的终点。
            // 用 Kill 而非 CompleteTransformTween：后者会先把角色瞬置到旧目标点再被下面覆盖，
            // 白跑一趟还会触发旧补间的完成回调。
            ToolkitTween.Kill(transform);

            // 先落位、后激活：本对象在被激活的那一刻就已处于最终位姿。
            // 原先「挪到 (999999999,…)、缩放归零」是为了遮住「等一帧再落位」的那一帧，
            // 现在位姿在激活前就写好了，遮丑不再需要，也不再依赖任何帧序。
            transform.position = toPos;
            transform.eulerAngles = toRot;
            transform.localScale = toScale;

            if (!m_ActorAnimator)
            {
                // 无动画播放器（例如纯粒子特效）：激活即完成
                gameObject.SetActive(true);
                MarkActorReady();
                return;
            }

            // 初始状态的唯一来源是 AnimatorBase.StateInitList。在首次激活之前写进去，
            // 它的 Start 就会直接应用这一组——不再「先按预制体配置应用一套、再清掉换成剧情要的那套」。
            // 已经 Start 过的实例写了也无害（没人再读），目标状态由下面的差集切换负责。
            if (toStateArray != null) m_ActorAnimator.StateInitList = toStateArray;
            _pendingInitStates = m_ActorAnimator.StateInitList;

            // 同一槽位可能被反复 Init，先退掉上一次可能还挂着的订阅
            m_ActorAnimator.OnInitComplete -= OnAnimatorInitComplete;

            // 激活对象。首次激活会触发 AnimatorBase 的 Start（Awake 在实例化时就已跑过）
            gameObject.SetActive(true);

            if (!m_ActorAnimator.isActiveAndEnabled)
            {
                // 动画播放器所在物体仍未激活、或组件被禁用：它的 Start 不会执行，就绪信号本轮不会到。
                // 不静默——否则角色会「加载成功但永远不出现」，且没有任何线索。
                int discarded = _pendingReadyActions?.Count ?? 0;
                Debug.LogWarning($"剧情演出 >> '{name}' 的动画播放器 '{m_ActorAnimator.name}' 未激活，动画初始化被跳过。"
                                 + (discarded > 0 ? $"已丢弃 {discarded} 个挂起的操作。" : string.Empty));

                // 丢弃挂起队列。此前这里既不 MarkActorReady 也不清队列，于是 RunWhenReady 的闭包
                // 会逐行对话无限堆积、永远排不空。就算之后对象被别处激活，重放一堆陈旧的状态切换也没有意义
                // ——真正该生效的是最新那一次。
                _pendingReadyActions = null;

                // 仍然订阅：对象若在别处被激活，AnimatorBase.Start 会跑，届时还能正常就绪。
                m_ActorAnimator.OnInitComplete += OnAnimatorInitComplete;
                return;
            }

            // ① 首次加载：此刻 Start 尚未执行，本行只登记，回调在 Start 末尾触发；
            // ② 同槽位重复 Init：已经就绪，本行内同步立即回调。
            m_ActorAnimator.OnInitComplete += OnAnimatorInitComplete;
        }

        // 动画播放器 初始化完成
        private void OnAnimatorInitComplete(AnimatorBase animator)
        {
            // 一次性订阅：立刻退订，避免同槽位反复 Init 时委托堆积
            animator.OnInitComplete -= OnAnimatorInitComplete;

            // 先切状态、后淡入。⚠️ 这两步的顺序不能调换，否则角色会在初始化完成的同一调用栈里被淡出隐藏：
            // 差集切换移除旧状态时，AnimatorBase.RemoveAnimState 会把渲染器引用计数减到 0 并就地
            // FadeAnimator(false)；若淡入排在前面，它会被这次淡出 Complete() 掉，收尾 alpha 恒为 0。
            // 目标状态为空数组（剧情行未配置动画）时必然踩中——实测 alpha 归零、角色整行对话不可见。
            // 反过来把淡入放在最后，它会 Complete() 掉切换过程中产生的中间态淡出并稳定收在 alpha=1。
            //
            // 切换用差集而非「全清再重放」：首次初始化时 AnimatorBase.Start 已按同一组应用过，
            // 这里两个循环都空转；同槽位重复 Init 时，同名循环动画不会被重头拉起、不会跳帧。
            animator.SwitchAnimStateArray(_pendingInitStates ?? Array.Empty<string>());
            _pendingInitStates = null;

            // 淡入显示。无条件调用：目标状态为空、或状态名在动画数据表里查不到时，
            // AddAnimState 不会淡入任何东西，只有这一行能保证角色最终可见。
            animator.FadeAnimator(true);

            MarkActorReady();
        }

        /// <summary>
        /// 淡出（临时隐藏，不销毁）。
        /// 有Spine动画则淡出；有粒子系统则停止发射。
        /// 普通预制体（无以上组件）则不处理，由外部设置非激活。
        /// </summary>
        /// <returns>是否由此方法处理了淡出（true=有淡出效果；false=无，外部应自行处理）</returns>
        public bool FadeOut()
        {
            bool handled = false;
            if (m_ActorAnimator)
            {
                // clearAnimOnFadeOut=false：临时隐藏，保留动画轨道与数据。
                // 渲染器与动画播放器同体时（Demo 的 Spine 角色即是），基类不会禁用它
                // ——见 AnimatorBase.CanDeactivateRenderer——只把不透明度补到 0，动画照常推进；
                // 渲染器在子物体上而被真禁用时，只要 clearStateOnDisable 为 false，
                // 后端的播放轨道也原样留存，重新激活后从冻结处继续。
                // 两种情形下 FadeIn() 都只需把不透明度补回来，不需要「重放动画」这一步。
                m_ActorAnimator.FadeAnimator(false, null, clearAnimOnFadeOut: false);
                handled = true;
            }
            if (m_ParticleSystemRoot)
            {
                m_ParticleSystemRoot.Stop();
                handled = true;
            }
            return handled;
        }
        
        /// <summary>
        /// 淡入（恢复显示）。
        /// 有Spine动画则淡入；有粒子系统则恢复播放。
        /// 普通预制体（无以上组件）则不处理，由外部设置激活。
        /// </summary>
        /// <returns>是否由此方法处理了淡入（true=有淡入效果；false=无，外部应自行处理）</returns>
        public bool FadeIn()
        {
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);
            
            bool handled = false;
            if (m_ActorAnimator)
            {
                // 淡入 动画播放器（内部会调用其 gameObject.SetActive(true)）
                m_ActorAnimator.FadeAnimator(true);
                handled = true;
            }
            if (m_ParticleSystemRoot)
            {
                m_ParticleSystemRoot.Play();
                handled = true;
            }
            return handled;
        }
        
        /// <summary>
        /// 销毁
        /// </summary>
        /// <param name="onComplete"></param>
        public void ExecuteDestroy(Action onComplete = null)
        {
            if (gameObject.activeSelf == false)
            {
                // 对象未激活，直接销毁
                Destroy(this.gameObject);
                // 调用回调
                onComplete?.Invoke();
                return;
            }
            
            // 计算销毁延迟时间
            bool hasDelay = false;
            float maxDelay = 0f;
            // 销毁 动画播放器
            if (m_ActorAnimator && m_ActorAnimator.DestroyAnim(out var delayAnim))
            {
                hasDelay = true;
                maxDelay = Mathf.Max(maxDelay, delayAnim);
            }
            // 销毁 粒子系统
            if (DestroyParticleSystem(out var delayParticle))
            {
                hasDelay = true;
                maxDelay = Mathf.Max(maxDelay, delayParticle);
            }
            
            // 如果有延迟，则等待延迟后销毁。
            // 刻意不传 owner：本对象正是要被销毁的目标，绑定生命周期会让回调随对象一起被丢弃，
            // 而 VnStoryManager.UnloadActorPrefab 靠这个回调卸载资源。保持「延时独立于目标存亡」。
            if (hasDelay)
            {
                ToolkitTween.DelayedCall(maxDelay, () =>
                {
                    // 销毁对象
                    if (this != null && gameObject) Destroy(gameObject);
                    // 调用回调
                    onComplete?.Invoke();
                }, unscaled: false);
            }
            else
            {
                // 销毁对象
                if (this != null && gameObject) Destroy(gameObject);
                // 调用回调
                onComplete?.Invoke();
            }
        }
        #endregion

        #region 就绪信号
        // 是否已完成过一次 ExecuteInit
        private bool _isActorReady;
        // 等待就绪的挂起操作
        private List<Action> _pendingReadyActions;
        /// <summary>
        /// 是否已就绪：至少完成过一次 <see cref="ExecuteInit"/>（已落位、已淡入、已切到目标状态）。
        /// <para>与 <c>AnimatorBase.IsInitComplete</c> 的区别：那条只说「动画播放器初始化完毕」；
        /// 本条说「本组件这一轮演出准备就绪」，没有动画播放器的普通预制体也会正常置位。</para>
        /// </summary>
        public bool IsActorReady => _isActorReady;

        /// <summary>
        /// 就绪后执行。已就绪则<b>同步立即</b>执行；否则挂起到 <see cref="ExecuteInit"/> 完成时按登记顺序执行一次。
        /// <para>本组件从未被 <see cref="ExecuteInit"/> 过（也没开 <c>m_AutoInit</c>）时，挂起的操作不会执行。</para>
        /// </summary>
        public void RunWhenReady(Action action)
        {
            if (action == null) return;
            if (_isActorReady) { action(); return; }
            if (_pendingReadyActions == null) _pendingReadyActions = new List<Action>();
            _pendingReadyActions.Add(action);
        }

        // 标记就绪并排空挂起队列
        private void MarkActorReady()
        {
            _isActorReady = true;
            if (_pendingReadyActions == null || _pendingReadyActions.Count == 0) return;

            // 把整张表摘下来交给局部变量，而不是共用一张实例暂存表。
            // 挂起的操作里可以合法地同步触发 ExecuteInit → OnAnimatorInitComplete → 本方法重入
            // （已就绪时本组件明确支持同步回调）；若两次调用共用同一张实例表，
            // 重入的那次会 Clear 掉外层 foreach 正在枚举的表，当场抛 InvalidOperationException。
            // 摘表之后重入会在上面那行 null 判断处直接返回，天然安全。
            var actions = _pendingReadyActions;
            _pendingReadyActions = null;
            for (int i = 0; i < actions.Count; i++) actions[i]();
        }
        #endregion

        #region 参数设置。位置、缩放、速度等
        [Header("参数设置。位置、缩放、速度等")]
        [Tooltip("角色移动 速度（米/秒）")]
        [SerializeField] private float m_ActorPosSpeed = 3.5f;
        [Tooltip("角色移动 过渡类型")]
        [SerializeField] private EToolkitEase m_ActorPosEase = EToolkitEase.InOutQuad;
        [Tooltip("角色旋转 速度（度/秒）")]
        [SerializeField] private float m_ActorRotateSpeed = 360f;
        [Tooltip("角色旋转 过渡类型")]
        [SerializeField] private EToolkitEase m_ActorRotateEase = EToolkitEase.InOutQuad;
        [Tooltip("角色缩放 速度（1.0=100%/秒）")]
        [SerializeField] private float m_ActorScaleSpeed = 1.5f;
        [Tooltip("角色移动 过渡类型")]
        [SerializeField] private EToolkitEase m_ActorScaleEase = EToolkitEase.InOutQuad;

        /// <summary>
        /// 立刻完成 当前的移动、旋转和缩放动画
        /// </summary>
        public void CompleteTransformTween()
        {
            // 立刻完成 本 Transform 上全部在途补间（瞬置到终值并触发完成回调）
            ToolkitTween.Kill(transform, complete: true);
        }
        
        /// <summary>
        /// 设置 位置
        /// 平滑过渡 到目标位置
        /// </summary>
        /// <param name="targetPos"></param>
        /// <param name="speedRate">速度倍率 标准值为1.0</param>
        public void SetToPosition(Vector3 targetPos, float speedRate = 1f)
        {
            // 如果对象未激活，激活后 直接设置位置 
            if (gameObject.activeSelf == false)
            {
                gameObject.SetActive(true);
                transform.position = targetPos;
                return;
            }
            
            // 计算过渡时间
            float duration = SafeDuration((targetPos - transform.position).magnitude, m_ActorPosSpeed, speedRate);
            // 位置。平滑过渡
            ToolkitTween.MoveTransform(transform, targetPos, duration, m_ActorPosEase, unscaled: false);
        }
        
        /// <summary>
        /// 设置 旋转
        /// 平滑过渡 到目标旋转
        /// </summary>
        /// <param name="targetRot"></param>
        /// <param name="speedRate"></param>
        public void SetToRotation(Vector3 targetRot, float speedRate = 1f)
        {
            // 如果对象未激活，激活后 直接设置旋转 
            if (gameObject.activeSelf == false)
            {
                gameObject.SetActive(true);
                transform.eulerAngles = targetRot;
                return;
            }
            
            // 计算过渡时间
            float angleDiff = Quaternion.Angle(Quaternion.Euler(transform.eulerAngles), Quaternion.Euler(targetRot));
            float duration = SafeDuration(angleDiff, m_ActorRotateSpeed, speedRate);
            // 旋转。平滑过渡。逐轴走最短弧，与上面按 Quaternion.Angle 算出的时长一致
            ToolkitTween.RotateTransform(transform, targetRot, duration, m_ActorRotateEase, unscaled: false);
        }
        
        /// <summary>
        /// 设置 缩放
        /// 平滑过渡 到目标缩放
        /// </summary>
        /// <param name="targetScale"></param>
        /// <param name="speedRate">速度倍率 标准值为1.0</param>
        public void SetToScale(Vector3 targetScale, float speedRate = 1f)
        {
            // 如果对象未激活，激活后 直接设置缩放 
            if (gameObject.activeSelf == false)
            {
                gameObject.SetActive(true);
                transform.localScale = targetScale;
                return;
            }
            
            // 计算过渡时间
            float duration = SafeDuration((targetScale - transform.localScale).magnitude, m_ActorScaleSpeed, speedRate);
            // 缩放。平滑过渡
            ToolkitTween.ScaleTransform(transform, targetScale, duration, m_ActorScaleEase, unscaled: false);
        }

        /// <summary>
        /// 由「距离 ÷ 速度」算补间时长，并挡住两种会污染 Transform 的取值。
        ///
        /// <para>分母为 0 时得 <c>Infinity</c>——补间永远走不完，角色卡在半路；
        /// 已经在目标点时得 <c>0f / 0f = NaN</c>，而 <c>ToolkitTween</c> 只挡 <c>duration &lt;= 0f</c>，
        /// 而 <c>NaN &lt;= 0</c> 为 <c>false</c>，于是 NaN 会一路写进 <c>transform</c>，把整条子层级污染成 NaN。</para>
        ///
        /// <para>速度字段没有 <c>[Min]</c> 约束，<paramref name="speedRate"/> 又直接来自剧本配置，
        /// 两者都可能是 0 或负数，故在这里统一兜底：算不出有限的正时长就返回 0，让补间退化为瞬置。</para>
        /// </summary>
        private static float SafeDuration(float distance, float speed, float speedRate)
        {
            float denominator = speed * speedRate;
            if (denominator <= 0f) return 0f;

            float duration = distance / denominator;
            return float.IsNaN(duration) || float.IsInfinity(duration) ? 0f : duration;
        }
        #endregion
        
        #region 动画设置
        [Header("动画设置")]
        [Tooltip("动画播放器。后端无关基类，挂 SpineAnimator 或 Live2DAnimator 均可")]
        [FormerlySerializedAs("m_SpineAnimator")]
        [SerializeField] private AnimatorBase m_ActorAnimator;

        // 「初始状态」此前在本组件与 AnimatorBase 上各存一份（本组件那份仅 m_AutoInit 为 true 时才被读，
        // 两个 Demo 预制体都是 false，实际从未生效）。现统一由 AnimatorBase.StateInitList 持有，
        // 本组件不再重复配置——见 ExecuteInit 对 StateInitList 的写入。

        // 状态与皮肤类接口一律经 RunWhenReady 门控，原因有两条，都是「不报错、只是不生效」的那种坑：
        // ① 早于 AnimatorBase.Start 调 AddAnimState，会被它的 SwitchAnimStateArray(stateInitList)
        //    当作「不在新列表里的旧状态」直接移除；
        // ② AnimatorBase 的每个皮肤入口都以 InitSkin() 开头，而 InitSkin 先落锁 _isSkinInit
        //    再让后端建表，骨架数据未就绪时会建出一张空表并【永不重试】，之后所有皮肤名都判不存在。

        /// <summary>
        /// 切换 整个状态列表：差集移除旧状态、差集添加新状态。
        /// <para>⚠️ 传空数组表示「不进入任何状态」，会把渲染器引用计数减到 0 从而<b>淡出隐藏角色</b>
        /// （这是 <c>AnimatorBase</c> 把可见性绑定在状态使用计数上的既有语义）。
        /// 只想换动画、不想让角色消失时，不要传空数组。</para>
        /// </summary>
        /// <param name="actorAnims">目标状态名列表。</param>
        public void SwitchStateArray(string[] actorAnims)
            => RunWhenReady(() => { if (m_ActorAnimator) m_ActorAnimator.SwitchAnimStateArray(actorAnims); });

        /// <summary>
        /// 添加 一个状态：播放该状态配置的一组动画。状态已存在时不重复添加。
        /// </summary>
        /// <param name="state">状态名称。</param>
        public void AddState(string state)
            => RunWhenReady(() => { if (m_ActorAnimator) m_ActorAnimator.AddAnimState(state); });

        /// <summary>
        /// 移除 一个状态：停止该状态配置的一组动画。状态不存在时不处理。
        /// </summary>
        /// <param name="state">状态名称。</param>
        public void RemoveState(string state)
            => RunWhenReady(() => { if (m_ActorAnimator) m_ActorAnimator.RemoveAnimState(state); });
        #endregion

        #region 单条动画 播放与停止
        // 动画名:正在播放的动画数据。
        // 基类以 AnimData 的【引用地址】作为轨道播放栈里的身份标识，StopAnim 也只认引用；
        // 而剧情侧手里只有字符串，所以要支持「按名停止」，就必须由本组件把那个引用留住。
        private readonly Dictionary<string, AnimData> _playingAnimMap = new Dictionary<string, AnimData>();
        // 批量停止时的暂存表，提为字段免得每次都新分配
        private readonly List<AnimData> _stopAnimScratch = new List<AnimData>();

        /// <summary>
        /// 播放 单条动画（叠加在状态动画之上，不改变当前状态列表）。
        ///
        /// <para><b>轨道语义</b>：默认走 <see cref="EAnimTrack.Action"/>——它会压在状态动画之上，
        /// 单次播完时基类自动把该轨道上被压住的上一条恢复播放，因此「插播一个动作、播完回到 idle」
        /// 调用方不需要做任何事。</para>
        ///
        /// <para><b>完成回调只在「非循环、且真的播到时长尽头」时触发一次。</b>以下情形一律<b>不</b>触发：
        /// ① <paramref name="isLoop"/> 为 true（调用时告警）；② <paramref name="speed"/> 为 0（返回 false）；
        /// ③ 动画名在后端查不到（返回 true，但 <see cref="IsAnimPlaying"/> 随即为 false）；
        /// ④ <b>动画时长为 0</b>——单帧姿势类动画都是这种，它们会正常摆出姿势并一直保持，只是没有「播完」这一刻；
        /// ⑤ 被 <see cref="StopAnim"/>、<see cref="StopAllAnims"/> 或新一轮 <see cref="ExecuteInit"/> 打断。</para>
        ///
        /// <para><b>完成时刻是「时长 / 速度」的定时器估算</b>，不是动画后端的结束事件；混合时长与
        /// timeScale 都会带来偏差。需要严格同步的场合不要依赖它。</para>
        /// </summary>
        /// <param name="animName">动画名称：动画软件中制作时的名称。</param>
        /// <param name="animTrack">动画轨道。不同轨道可同时播放，枚举值大的覆盖枚举值小的。</param>
        /// <param name="animTrackSub">动画子轨道，0~9。同一轨道下的不同子轨道可同时播放。</param>
        /// <param name="isLoop">是否循环播放。循环时没有完成回调。</param>
        /// <param name="isReverse">是否反向播放（从结束位置起、反方向播）。</param>
        /// <param name="speed">播放速度倍率。为 0 时拒绝播放。</param>
        /// <param name="startDelayTime">起播延时（秒）。</param>
        /// <param name="onComplete">单次播放完成回调。</param>
        /// <returns>是否受理了这次播放请求。<c>false</c> = 明确不会播，也不会有回调。</returns>
        public bool PlayAnim(
            string animName,
            EAnimTrack animTrack = EAnimTrack.Action,
            int animTrackSub = 0,
            bool isLoop = false,
            bool isReverse = false,
            float speed = 1f,
            float startDelayTime = 0f,
            Action onComplete = null)
        {
            if (!m_ActorAnimator) return false;
            if (string.IsNullOrEmpty(animName)) return false;

            // 把两处「回调静默不触发」显式化，不留给剧情侧去猜
            if (Mathf.Approximately(speed, 0f))
            {
                Debug.LogWarning($"剧情演出 >> '{name}' 的动画 '{animName}' 速度为 0，将定格在起始帧，不予播放。");
                return false;
            }
            if (isLoop && onComplete != null)
                Debug.LogWarning($"剧情演出 >> '{name}' 播放循环动画 '{animName}' 时传了完成回调，该回调永远不会触发。");

            // 同名已在播：先停掉旧的那一条，语义取「重头播」。
            // 这顺带让「同一个 AnimData 实例不能同时播两次」这个坑不可能发生——每次都是新实例。
            StopAnim(animName);

            // AnimData 的构造函数收的是【完整轨道号】（主轨道 * 10 + 子轨道），与 Inspector 上那两个字段等价
            int trackIndex = (int)animTrack * 10 + Mathf.Clamp(animTrackSub, 0, 9);
            var animData = new AnimData(animName, trackIndex, isLoop, isReverse, speed, startDelayTime);
            _playingAnimMap[animName] = animData;

            RunWhenReady(() =>
            {
                if (!m_ActorAnimator) return;
                // 挂起期间可能已被 StopAnim 或新一轮 ExecuteInit 取消
                AnimData pending;
                if (!_playingAnimMap.TryGetValue(animName, out pending) || !ReferenceEquals(pending, animData)) return;

                // 播放令牌只在后端真的起播之后才递增，故「前后是否变化」正是「这次有没有播起来」。
                // 不能用「令牌是否为 0」作判据：该轨道上此前播过别的动画时令牌早就非 0 了。
                int tokenBefore = m_ActorAnimator.GetAnimPlayToken(trackIndex);

                m_ActorAnimator.PlayAnim(animData, _ =>
                {
                    // 基类在回调前已经把这条停掉了，这里只做本组件的簿记
                    AnimData cur;
                    if (_playingAnimMap.TryGetValue(animName, out cur) && ReferenceEquals(cur, animData))
                        _playingAnimMap.Remove(animName);
                    if (onComplete != null) onComplete();
                });

                // 起播失败（动画名在后端查不到）时基类只留一句告警、不会有任何回调，
                // 这里靠令牌未推进判定并把表清干净，免得 IsAnimPlaying 永远为真。
                // 起播延时 > 0 的那次播放是推迟发生的，令牌当然还没变，故排除在判据之外。
                if (startDelayTime <= 0f && m_ActorAnimator.GetAnimPlayToken(trackIndex) == tokenBefore)
                    _playingAnimMap.Remove(animName);
            });

            return true;
        }

        /// <summary>
        /// 停止 由 <see cref="PlayAnim"/> 播放的指定名称的动画。该轨道上被它压住的上一条会自动恢复。
        /// </summary>
        /// <param name="animName">动画名称。</param>
        /// <returns>是否确实停掉了一条。</returns>
        public bool StopAnim(string animName)
        {
            if (string.IsNullOrEmpty(animName)) return false;

            AnimData animData;
            if (!_playingAnimMap.TryGetValue(animName, out animData)) return false;

            _playingAnimMap.Remove(animName);
            // 必须交回同一个引用：基类以引用地址作身份标识
            if (m_ActorAnimator) m_ActorAnimator.StopAnim(animData);
            return true;
        }

        /// <summary>
        /// 停止 全部由 <see cref="PlayAnim"/> 播放的单条动画。不影响状态动画。
        /// </summary>
        public void StopAllAnims()
        {
            if (_playingAnimMap.Count == 0) return;

            // 先快照再清空：StopAnim 会触发基类的轨道恢复，避免在字典上迭代时被改
            _stopAnimScratch.Clear();
            _stopAnimScratch.AddRange(_playingAnimMap.Values);
            _playingAnimMap.Clear();
            if (m_ActorAnimator)
            {
                foreach (var animData in _stopAnimScratch)
                    m_ActorAnimator.StopAnim(animData);
            }
            _stopAnimScratch.Clear();
        }

        /// <summary>
        /// 指定名称的单条动画是否在播（含仍在等待起播延时的）。
        /// </summary>
        public bool IsAnimPlaying(string animName)
            => !string.IsNullOrEmpty(animName) && _playingAnimMap.ContainsKey(animName);
        #endregion

        #region 换装 / 皮肤
        /// <summary>
        /// 设置 基础皮肤组（始终显示的一组皮肤），覆盖预制体上配置的那一组。
        /// </summary>
        public void SetBaseSkin(string[] baseSkinNames, bool isRefresh = true)
            => RunWhenReady(() => { if (m_ActorAnimator) m_ActorAnimator.SetBaseSkin(baseSkinNames, isRefresh); });

        /// <summary>
        /// 添加 皮肤。可叠加多件；名称不存在时动画播放器会告警并忽略。
        /// <para>本层只做透传，<b>没有</b><c>AnimActor</c> 那套「同一部位只能穿一件」的分组互斥语义。</para>
        /// </summary>
        /// <param name="skinName">皮肤名称：含文件夹路径时一般用 '/' 分隔。</param>
        /// <param name="isRefresh">是否立即刷新显示。批量增删时前几次传 false、最后一次传 true。</param>
        public void AddSkin(string skinName, bool isRefresh = true)
            => RunWhenReady(() => { if (m_ActorAnimator) m_ActorAnimator.AddSkin(skinName, isRefresh); });

        /// <summary>
        /// 移除 皮肤。
        /// </summary>
        public void RemoveSkin(string skinName, bool isRefresh = true)
            => RunWhenReady(() => { if (m_ActorAnimator) m_ActorAnimator.RemoveSkin(skinName, isRefresh); });

        /// <summary>
        /// 刷新 皮肤：把基础皮肤与应用中皮肤的并集重新应用到渲染器。
        /// </summary>
        public void RefreshSkin()
            => RunWhenReady(() => { if (m_ActorAnimator) m_ActorAnimator.RefreshSkin(); });
        #endregion

        #region 粒子系统
        [Header("粒子系统")]
        [Tooltip("角色粒子系统 根节点")]
        [SerializeField] private ParticleSystem m_ParticleSystemRoot;
        
        /// <summary>
        /// 销毁 粒子系统：停止发射并给出等待时长。作用于本组件指定的粒子根节点。
        /// </summary>
        /// <param name="delay">需要等待的时长（秒）。返回 false 时为 -1。</param>
        /// <returns>是否存在粒子系统。</returns>
        private bool DestroyParticleSystem(out float delay)
        {
            delay = -1f;
            if (m_ParticleSystemRoot == null) return false;

            return StopParticlesAndGetDelay(m_ParticleSystemRoot.gameObject, out delay);
        }

        /// <summary>
        /// 停止 <paramref name="root"/> 子树内全部粒子系统的发射，并给出「已发射的粒子自然播完」所需的等待时长。
        ///
        /// <para>提为静态是因为<b>没有挂 <see cref="VnActorAnimator"/> 的预制体也需要这套收尾</b>——
        /// 剧情演出里「直接做一个自动播放的粒子特效预制体」是受支持的用法，
        /// 销毁它时同样不能把在途粒子拦腰截断。<c>VnStoryManager</c> 的降级销毁路径调用的就是本方法。</para>
        /// </summary>
        /// <param name="root">要扫描的根物体。本组件传粒子根节点，无组件时由外部传整个实例。</param>
        /// <param name="delay">需要等待的时长（秒）。返回 false 时为 -1。</param>
        /// <returns>子树内是否存在（激活的）粒子系统。</returns>
        internal static bool StopParticlesAndGetDelay(GameObject root, out float delay)
        {
            delay = -1f;
            if (!root) return false;

            // 只取激活的：未激活的粒子系统本就没在发射，把它的存活期计进等待时长只会平白拖长销毁
            var particleSystems = root.GetComponentsInChildren<ParticleSystem>();
            if (particleSystems.Length == 0) return false;

            // 延迟时间 为粒子系统中 所有粒子的最大存活时间，包括子物体上的粒子系统
            float maxLifetime = 0f;
            foreach (var ps in particleSystems)
            {
                // 只停发射、不清除已有粒子，让它们自然播完
                ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                maxLifetime = Mathf.Max(maxLifetime, ps.main.startLifetime.constantMax);
            }
            delay = maxLifetime;

            return true;
        }
        #endregion
    }
}