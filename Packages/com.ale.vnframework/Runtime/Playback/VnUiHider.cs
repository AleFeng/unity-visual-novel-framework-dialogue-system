using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Ale.VnFramework
{
    /// <summary>
    /// 隐藏 UI：点一下藏起全部界面欣赏画面，再点屏幕任意位置恢复。
    ///
    /// <para><b>为什么不用 <c>uiCanvasGroup</c>。</b>那个 CanvasGroup 被
    /// <c>FadeInUI</c> / <c>FadeOutUI</c> 连同 <c>_isUiFadeIn</c> 独占。隐藏 UI 去改它的 alpha，
    /// 一旦期间发生 <c>StopVnStory()</c>，淡出会因状态判断错位而直接返回或重复执行，
    /// 而它的完成回调里挂着 <c>StopStoryConversation()</c>——结果是「点了隐藏 UI，对话停不下来」。</para>
    ///
    /// <para><b>为什么不用 <c>SetActive(false)</c>。</b>两条硬理由：
    /// 既有注释明确写着 Dialogue System 要求 uiCanvas 保持激活；
    /// 而且打字机的 <c>OnDisable</c> 会调 <c>Stop()</c>，把当前行一次显示完并触发结束事件——
    /// 隐藏一次就把这行的逐字演出打断了。</para>
    ///
    /// <para>所以用 <c>Canvas.enabled = false</c>：渲染与射线一起断（uGUI 的 Graphic 只认
    /// 第一个 <c>isActiveAndEnabled</c> 的祖先 Canvas，组件被禁用时子级会从 GraphicRegistry 摘除），
    /// 而 GameObject 全程保持激活，协程、打字机、Sequencer 一切照旧。</para>
    /// </summary>
    [DisallowMultipleComponent]
    public class VnUiHider : MonoBehaviour
    {
        [Header("隐藏范围")]
        [Tooltip("除 VnStoryManager 的 uiCanvas 之外，还要一并隐藏的画布。宿主的 HUD 放这里。")]
        [SerializeField] private Canvas[] extraCanvases;

        [Header("恢复用的点击捕获器")]
        [Tooltip("捕获器所在画布的排序层级。要盖过所有被隐藏的画布，一般不用改。")]
        [SerializeField] private int catcherSortingOrder = 32767;

        private readonly List<Canvas> _hostCanvases = new List<Canvas>();
        // 隐藏时记下每块画布**原本**的 enabled，恢复时按原值还原——
        // 无脑置 true 会把本来就关着的画布一起打开。
        private readonly List<KeyValuePair<Canvas, bool>> _hidden = new List<KeyValuePair<Canvas, bool>>();

        private VnHideUiCatcher _catcher;

        /// <summary>当前是否处于隐藏状态。</summary>
        public bool IsHidden { get; private set; }

        /// <summary>宿主注册额外要隐藏的画布（自己的 HUD 等）。</summary>
        public void RegisterCanvas(Canvas canvas)
        {
            if (canvas && !_hostCanvases.Contains(canvas)) _hostCanvases.Add(canvas);
        }

        /// <summary>取消注册。</summary>
        public bool UnregisterCanvas(Canvas canvas) => _hostCanvases.Remove(canvas);

        /// <summary>切换隐藏 / 显示。</summary>
        public void Toggle()
        {
            if (IsHidden) Show(); else Hide();
        }

        /// <summary>
        /// 隐藏全部 UI。
        ///
        /// <para><b>场景里没有 EventSystem 时会拒绝隐藏并报错</b>，而不是照做——
        /// 没有 EventSystem 就收不到点击，藏起来之后<b>永远恢复不了</b>，那是软锁。
        /// 宁可这个功能不生效。</para>
        /// </summary>
        public void Hide()
        {
            if (IsHidden) return;

            if (EventSystem.current == null)
            {
                Debug.LogError("剧情演出 >> 场景里没有 EventSystem，隐藏 UI 之后将无法点击恢复（软锁）。" +
                               "本次隐藏已取消。请在场景中添加 EventSystem。");
                return;
            }

            _hidden.Clear();
            foreach (var canvas in EnumerateCanvases())
            {
                if (!canvas) continue;
                _hidden.Add(new KeyValuePair<Canvas, bool>(canvas, canvas.enabled));
                canvas.enabled = false;
            }

            EnsureCatcher();
            if (_catcher) _catcher.gameObject.SetActive(true);

            IsHidden = true;
            SetPlaybackPaused(true);
        }

        /// <summary>恢复显示。</summary>
        public void Show()
        {
            if (!IsHidden) return;

            for (int i = 0; i < _hidden.Count; i++)
            {
                var kv = _hidden[i];
                if (kv.Key) kv.Key.enabled = kv.Value;
            }
            _hidden.Clear();

            if (_catcher) _catcher.gameObject.SetActive(false);

            IsHidden = false;
            SetPlaybackPaused(false);
        }

        private void OnDisable()
        {
            // 组件被关掉时如果还藏着，画布就再也没人还原了。
            if (IsHidden) Show();
        }

        private IEnumerable<Canvas> EnumerateCanvases()
        {
            var manager = VnStoryManager.Instance;
            if (manager && manager.UiCanvas) yield return manager.UiCanvas;

            if (extraCanvases != null)
            {
                foreach (var c in extraCanvases) yield return c;
            }
            foreach (var c in _hostCanvases) yield return c;
        }

        private void SetPlaybackPaused(bool paused)
        {
            var manager = VnStoryManager.Instance;
            if (!manager) return;
            var playback = manager.Playback;
            if (playback) playback.SetUiHidden(paused);
        }

        // 捕获器必须在**独立的画布**上：被隐藏的正是 uiCanvas，
        // 捕获器若挂在它下面会跟着一起失效，直接软锁。
        private void EnsureCatcher()
        {
            if (_catcher) return;

            var go = new GameObject("[VnHideUiCatcher]");
            go.transform.SetParent(transform, false);
            go.layer = LayerMask.NameToLayer("UI");

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = catcherSortingOrder;
            go.AddComponent<GraphicRaycaster>();

            var image = go.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0f);   // 全透明，只用来吃射线
            image.raycastTarget = true;
            var rt = image.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            _catcher = go.AddComponent<VnHideUiCatcher>();
            _catcher.Bind(this);
            go.SetActive(false);
        }
    }

    /// <summary>
    /// 隐藏期间铺满屏幕的点击捕获器。由 <see cref="VnUiHider"/> 在运行时创建，不写进预制体——
    /// 这样任何工程的 UI 结构都能用，不需要额外接线。
    ///
    /// <para>用 <see cref="IPointerClickHandler"/> 而不是 PointerDown：click 要求按下与抬起在同一对象上，
    /// 拖拽经过时不会误触恢复。</para>
    /// </summary>
    public class VnHideUiCatcher : MonoBehaviour, IPointerClickHandler
    {
        private VnUiHider _owner;

        /// <summary>绑定所属的隐藏组件。</summary>
        public void Bind(VnUiHider owner) => _owner = owner;

        /// <inheritdoc/>
        public void OnPointerClick(PointerEventData eventData)
        {
            if (_owner) _owner.Show();
        }
    }
}
