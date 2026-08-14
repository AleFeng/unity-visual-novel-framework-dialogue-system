using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Ale.VnFramework
{
    /// <summary>播放控制按钮的用途。决定它点一下做什么、图标怎么切。</summary>
    public enum EVnPlaybackButtonKind
    {
        /// <summary>自动播放开关。</summary>
        AutoPlay,
        /// <summary>播放速度档位，点一下循环 1x → 2x → 3x。</summary>
        SpeedTier,
        /// <summary>快进。<b>长按生效、松开结束</b>，不是点击切换。</summary>
        FastForward,
        /// <summary>新对话停止开关。</summary>
        StopOnUnread,
        /// <summary>隐藏 UI。点一下即隐藏，再点屏幕任意处恢复。</summary>
        HideUi,
    }

    /// <summary>
    /// 播放控制按钮。挂在按钮物体上，按 <see cref="kind"/> 自动接上
    /// <see cref="VnPlaybackController"/>，并按状态切换开 / 关两张图。
    ///
    /// <para><b>走 EventSystem 的指针事件，不碰输入系统。</b>本包不引用 Input System，
    /// 而旧版 <c>UnityEngine.Input</c> 在启用了新输入系统的工程里直接抛异常；
    /// <c>IPointerDownHandler</c> 一族由 EventSystem 派发，两种后端通吃。</para>
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class VnPlaybackButton : MonoBehaviour,
        IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, IPointerClickHandler
    {
        [Header("用途")]
        [Tooltip("这个按钮做什么。快进是长按，其余是点击。")]
        [SerializeField] private EVnPlaybackButtonKind kind = EVnPlaybackButtonKind.AutoPlay;

        [Header("图标")]
        [Tooltip("要换图的 Image。留空则取本物体上的 Image。")]
        [SerializeField] private Image targetImage;
        [Tooltip("关闭态图标。")]
        [SerializeField] private Sprite offSprite;
        [Tooltip("开启态图标（带外发光）。")]
        [SerializeField] private Sprite onSprite;

        [Header("播放速度专用")]
        [Tooltip("三档的关闭态图标，顺序为 1x / 2x / 3x。仅 kind = SpeedTier 时使用。")]
        [SerializeField] private Sprite[] speedOffSprites = new Sprite[3];
        [Tooltip("三档的开启态图标，顺序为 1x / 2x / 3x。仅 kind = SpeedTier 时使用。")]
        [SerializeField] private Sprite[] speedOnSprites = new Sprite[3];

        [Header("快进专用")]
        [Tooltip("指针移出按钮时是否视作松手。关掉的话，按住拖出按钮再抬起会收不到 PointerUp，快进将卡住不放。")]
        [SerializeField] private bool releaseOnPointerExit = true;

        [Header("隐藏UI专用")]
        [Tooltip("负责隐藏的组件。留空则运行时自动在 VnStoryManager 上找。")]
        [SerializeField] private VnUiHider uiHider;

        private VnPlaybackController _controller;
        private bool _pressed;

        private VnPlaybackController Controller
        {
            get
            {
                if (_controller) return _controller;
                var manager = VnStoryManager.Instance;
                if (manager) _controller = manager.Playback;
                return _controller;
            }
        }

        private VnUiHider Hider
        {
            get
            {
                if (uiHider) return uiHider;
                var manager = VnStoryManager.Instance;
                if (manager) uiHider = manager.GetComponent<VnUiHider>();
                return uiHider;
            }
        }

        private void Awake()
        {
            if (!targetImage) targetImage = GetComponent<Image>();
        }

        private void OnEnable()
        {
            var c = Controller;
            if (c != null)
            {
                c.StateChanged -= Refresh;
                c.StateChanged += Refresh;
            }
            Refresh();
        }

        private void OnDisable()
        {
            var c = Controller;
            if (c != null) c.StateChanged -= Refresh;

            // 被禁用时若还按着快进，PointerUp 永远收不到，倍率会一直卡在 5。
            ReleaseIfHolding();
        }

        #region 指针事件

        /// <inheritdoc/>
        public void OnPointerDown(PointerEventData eventData)
        {
            if (kind != EVnPlaybackButtonKind.FastForward) return;
            _pressed = true;
            Controller?.BeginFastForward();
            Refresh();
        }

        /// <inheritdoc/>
        public void OnPointerUp(PointerEventData eventData) => ReleaseIfHolding();

        /// <inheritdoc/>
        public void OnPointerExit(PointerEventData eventData)
        {
            if (releaseOnPointerExit) ReleaseIfHolding();
        }

        /// <inheritdoc/>
        public void OnPointerClick(PointerEventData eventData)
        {
            // 快进只认按下 / 松开，不响应点击——否则一次长按会在松手时再触发一遍。
            if (kind == EVnPlaybackButtonKind.FastForward) return;

            switch (kind)
            {
                case EVnPlaybackButtonKind.AutoPlay: Controller?.ToggleAutoPlay(); break;
                case EVnPlaybackButtonKind.SpeedTier: Controller?.CycleSpeedTier(); break;
                case EVnPlaybackButtonKind.StopOnUnread: Controller?.ToggleStopOnUnread(); break;
                case EVnPlaybackButtonKind.HideUi:
                    var hider = Hider;
                    if (hider) hider.Hide();
                    else Debug.LogWarning("剧情演出 >> 隐藏UI 按钮没有找到 VnUiHider 组件，请把它挂到 VnStoryManager 上。");
                    break;
            }
            Refresh();
        }

        private void ReleaseIfHolding()
        {
            if (!_pressed) return;
            _pressed = false;
            Controller?.EndFastForward();
            Refresh();
        }

        #endregion

        /// <summary>按当前状态刷新图标。由 <see cref="VnPlaybackController.StateChanged"/> 驱动。</summary>
        public void Refresh()
        {
            if (!targetImage) return;
            var c = Controller;
            if (c == null) return;

            switch (kind)
            {
                case EVnPlaybackButtonKind.AutoPlay:
                    Apply(c.AutoPlay);
                    break;

                case EVnPlaybackButtonKind.SpeedTier:
                    {
                        int tier = Mathf.Clamp((int)c.SpeedTier, 1, 3);
                        // 1 档用关闭态（它是默认值，不该一直发着光），2 / 3 档用开启态。
                        var set = tier == 1 ? speedOffSprites : speedOnSprites;
                        if (set != null && set.Length >= tier && set[tier - 1]) targetImage.sprite = set[tier - 1];
                        break;
                    }

                case EVnPlaybackButtonKind.FastForward:
                    // Suppressed 显示为关闭态：它确实已经不生效了，亮着会骗人。
                    Apply(c.FastForwardState == EVnFastForwardState.Active);
                    break;

                case EVnPlaybackButtonKind.StopOnUnread:
                    Apply(c.StopOnUnread);
                    break;

                case EVnPlaybackButtonKind.HideUi:
                    Apply(false);   // 瞬时动作，没有持续状态
                    break;
            }
        }

        private void Apply(bool on)
        {
            var sprite = on ? onSprite : offSprite;
            if (sprite) targetImage.sprite = sprite;
        }
    }
}
