using System;
using System.Collections;
using UnityEngine;
// using Fs.GameFramework.Gameplay.AnimSimulatorSystem;

#if DOTWEEN
using DG.Tweening;
#endif

namespace PixelCrushers.DialogueSystem.VNStoryFramework
{
    /// <summary>
    /// 游戏角色 控制器 Spine动画
    /// </summary>
    public class VNActorAnimator : MonoBehaviour
    {
#if DOTWEEN
#if UNITY_EDITOR
        private void Reset()
        {
#if HAS_SPINE
            // 获取 Spine动画组件
            if (!m_SpineAnimator)
            {
                // 从当前对象或子对象中 获取 Spine动画组件
                m_SpineAnimator = GetComponentInChildren<SpineAnimator>();
            }
#endif
        }
#endif
        
        private void Start()
        {
            // 自动初始化
            if (m_AutoInit)
            {
                ExecuteInit
                (
                    transform.position, 
                    transform.eulerAngles, 
                    transform.localScale, 
                    m_StateInitList
                );
            }
        }

        #region 初始化、销毁
        [Tooltip("自动初始化")]
        [SerializeField] private bool m_AutoInit;
        
        /// <summary>
        /// 初始化
        /// 位置、缩放、状态列表
        /// </summary>
        /// <param name="toPos"></param>
        /// <param name="toRot"></param>
        /// <param name="toScale"></param>
        /// <param name="toStateArray"></param>
        public void ExecuteInit(Vector3 toPos, Vector3 toRot, Vector3 toScale, string[] toStateArray)
        {
            // 先将位置设置到无穷远，缩放设置为0，以隐藏角色
            transform.position = new Vector3(999999999, 999999999, 999999999);
            transform.localScale = Vector3.zero;
            // 激活对象
            gameObject.SetActive(true);
            // 使用协程以确保在 Awake 完成后再进行初始化
            StartCoroutine(CorInit(toPos, toRot, toScale, toStateArray));
        }
        
        private IEnumerator CorInit(Vector3 toPos, Vector3 toRot, Vector3 toScale, string[] toStateArray)
        {
            // 等待一帧，确保 Awake 完成
            yield return null;
            
            // 初始化时，直接设置位置和缩放
            transform.position = toPos;
            transform.eulerAngles = toRot;
            transform.localScale = toScale;
            // 初始化时，清除所有状态。并重置Spine动画组件
            ClearAllStates();
            
            yield return null;

#if HAS_SPINE
            if (m_SpineAnimator)
                // 淡入显示
                m_SpineAnimator.FadeSpineAnimator(true);
#endif
            
            // 切换状态列表
            SwitchStateArray(toStateArray);
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
#if HAS_SPINE
            if (m_SpineAnimator)
            {
                // clearAnimOnFadeOut=false：临时隐藏，仅禁用对象，保留动画数据
                // 以便 FadeIn() 时通过 RestoreCurrentAnims() 重新播放动画
                m_SpineAnimator.FadeSpineAnimator(false, null, clearAnimOnFadeOut: false);
                handled = true;
            }
#endif
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
#if HAS_SPINE
            if (m_SpineAnimator)
            {
                // 淡入 Spine 组件（内部会调用 spineAnimator.gameObject.SetActive(true)）
                m_SpineAnimator.FadeSpineAnimator(true);
                handled = true;
            }
#endif
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
#if HAS_SPINE
            // 销毁 Spine动画
            if (m_SpineAnimator && m_SpineAnimator.DestroySpineAnim(out var delaySpine))
            {
                hasDelay = true;
                maxDelay = Mathf.Max(maxDelay, delaySpine);
            }
#endif
            // 销毁 粒子系统
            if (DestroyParticleSystem(out var delayParticle))
            {
                hasDelay = true;
                maxDelay = Mathf.Max(maxDelay, delayParticle);
            }
            
            // 如果有延迟，则启动协程等待延迟后销毁
            if (hasDelay)
            {
                DOVirtual.DelayedCall(maxDelay, () =>
                {
                    // 销毁对象
                    if (this != null && gameObject) Destroy(gameObject);
                    // 调用回调
                    onComplete?.Invoke();
                });
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
        
        #region 参数设置。位置、缩放、速度等
        [Header("参数设置。位置、缩放、速度等")]
        [Tooltip("角色移动 速度（米/秒）")]
        [SerializeField] private float m_ActorPosSpeed = 3.5f;
        [Tooltip("角色移动 过渡类型")]
        [SerializeField] private Ease m_ActorPosEase = Ease.InOutQuad;
        [Tooltip("角色旋转 速度（度/秒）")]
        [SerializeField] private float m_ActorRotateSpeed = 360f;
        [Tooltip("角色旋转 过渡类型")]
        [SerializeField] private Ease m_ActorRotateEase = Ease.InOutQuad;
        [Tooltip("角色缩放 速度（1.0=100%/秒）")]
        [SerializeField] private float m_ActorScaleSpeed = 1.5f;
        [Tooltip("角色移动 过渡类型")]
        [SerializeField] private Ease m_ActorScaleEase = Ease.InOutQuad;
        
        /// <summary>
        /// 立刻完成 当前的移动和缩放动画
        /// </summary>
        public void CompleteDoTweenTransform()
        {
            // 立刻完成 当前的移动和缩放动画
            transform.DOComplete();
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
            transform.DOMove(targetPos, duration).SetEase(m_ActorPosEase);
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
            // 旋转。平滑过渡
            transform.DORotate(targetRot, duration).SetEase(m_ActorRotateEase);
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
            transform.DOScale(targetScale, duration).SetEase(m_ActorScaleEase);
        }
        #endregion
        
        #region 动画设置
        [Header("动画设置")]
#if HAS_SPINE
        [Tooltip("Spine动画播放器")]
        [SerializeField] private SpineAnimator m_SpineAnimator;
#endif
        [Tooltip("初始状态")]
        [SerializeField] private string[] m_StateInitList = new string[] { "idle" };
        
        /// <summary>
        /// 清除 所有状态
        /// </summary>
        private void ClearAllStates()
        {
#if HAS_SPINE
            // 清除所有 Spine动画
            if (m_SpineAnimator)
                m_SpineAnimator.ClearAllSpineAnim();
#endif
        }
        
        /// <summary>
        /// 切换 整个状态列表
        /// </summary>
        /// <param name="actorAnims"></param>
        public void SwitchStateArray(string[] actorAnims)
        {
#if HAS_SPINE
            // 清除所有 Spine动画
            if (m_SpineAnimator)
                m_SpineAnimator.SwitchStateArray(actorAnims);
#endif
        }

        /// <summary>
        /// 添加状态。播放状态对应的动画。
        /// </summary>
        /// <param name="newState"></param>
        /// <returns></returns>
        public void AddState(string newState)
        {
            // 添加状态
#if HAS_SPINE
            // Spine动画播放器
            if (m_SpineAnimator)
                m_SpineAnimator.AddSpineAnimState(newState);
#endif
        }
        
        /// <summary>
        /// 移除状态。移除状态对应的动画。
        /// </summary>
        /// <param name="newState"></param>
        /// <returns></returns>
        public void RemoveState(string newState)
        {
            // 移除状态
#if HAS_SPINE
            // Spine动画播放器
            if (m_SpineAnimator)
                m_SpineAnimator.RemoveSpineAnimState(newState);
#endif
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
#endif
    }
}