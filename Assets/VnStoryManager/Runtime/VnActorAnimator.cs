using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using Ale.Toolkit.Runtime;
using Ale.AnimSimulatorSystem;

namespace PixelCrushers.DialogueSystem.VnStoryFramework
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
                // 动画播放器所在物体仍未激活、或组件被禁用：它的 Start 不会执行，就绪信号永远不会到。
                // 不静默——否则角色会「加载成功但永远不出现」，且没有任何线索。
                Debug.LogWarning($"剧情演出 >> '{name}' 的动画播放器 '{m_ActorAnimator.name}' 未激活，动画初始化被跳过。");
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
        // 排空挂起队列时的暂存表，提为字段免得每次都新分配
        private readonly List<Action> _readyActionScratch = new List<Action>();

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

            // 先快照再清空：挂起的操作里可能又调 RunWhenReady，此时已就绪、会走同步分支，不会回写本表
            _readyActionScratch.Clear();
            _readyActionScratch.AddRange(_pendingReadyActions);
            _pendingReadyActions.Clear();
            foreach (var action in _readyActionScratch) action();
            _readyActionScratch.Clear();
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
            float duration = (targetPos - transform.position).magnitude / (m_ActorPosSpeed * speedRate);
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
            float duration = angleDiff / (m_ActorRotateSpeed * speedRate);
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
            float duration = (targetScale - transform.localScale).magnitude / (m_ActorScaleSpeed * speedRate);
            // 缩放。平滑过渡
            ToolkitTween.ScaleTransform(transform, targetScale, duration, m_ActorScaleEase, unscaled: false);
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

        /// <summary>
        /// 切换 整个状态列表
        /// </summary>
        /// <param name="actorAnims"></param>
        public void SwitchStateArray(string[] actorAnims)
        {
            // 切换 整个状态列表
            if (m_ActorAnimator)
                m_ActorAnimator.SwitchAnimStateArray(actorAnims);
        }

        /// <summary>
        /// 添加状态。播放状态对应的动画。
        /// </summary>
        /// <param name="newState"></param>
        /// <returns></returns>
        public void AddState(string newState)
        {
            // 添加状态
            if (m_ActorAnimator)
                m_ActorAnimator.AddAnimState(newState);
        }
        
        /// <summary>
        /// 移除状态。移除状态对应的动画。
        /// </summary>
        /// <param name="newState"></param>
        /// <returns></returns>
        public void RemoveState(string newState)
        {
            // 移除状态
            if (m_ActorAnimator)
                m_ActorAnimator.RemoveAnimState(newState);
        }
        #endregion

        #region 粒子系统
        [Header("粒子系统")]
        [Tooltip("角色粒子系统 根节点")]
        [SerializeField] private ParticleSystem m_ParticleSystemRoot;
        
        /// <summary>
        /// 销毁 粒子系统
        /// </summary>
        /// <param name="delay"></param>
        /// <returns></returns>
        private bool DestroyParticleSystem(out float delay)
        {
            delay = -1f;
            if (m_ParticleSystemRoot == null) return false;
            
            // 停止 粒子系统 发射新粒子
            m_ParticleSystemRoot.Stop();
            // 延迟时间 为粒子系统中 所有粒子的最大存活时间
            // 包括子物体上的 粒子系统
            float maxLifetime = 0f;
            var particleSystems = m_ParticleSystemRoot.GetComponentsInChildren<ParticleSystem>();
            foreach (var ps in particleSystems)
            {
                maxLifetime = Mathf.Max(maxLifetime, ps.main.startLifetime.constantMax);
            }
            delay = maxLifetime;
            
            return true;
        }
        #endregion
    }
}