using UnityEngine;
using UnityEngine.UI;
using Ale.Toolkit.Runtime;

#if HAS_TMPRO
using TMPro;
#endif

namespace PixelCrushers.DialogueSystem.VNStoryFramework
{
    /// <summary>
    /// 选项按钮。
    /// VNStoryManager专用，会根据 是否已读 的状态，切换不同的文本颜色、按钮颜色、提示对象显示、图片颜色等UI显示。
    /// </summary>
    public class VNResponseButton : StandardUIResponseButton
    {
        [Tooltip("图片列表：可包含多个图片组件，根据 是否已读的状态 设置图片的颜色。")] [SerializeField]
        private Image[] stateImageArray;
        [Tooltip("文本列表：可包含多个文本组件，根据 是否已读的状态 设置文本的颜色。")]
#if HAS_TMPRO
        [SerializeField] private TextMeshProUGUI[] stateTxtArray;
#else
        [SerializeField] private Text[] stateTxtArray;
#endif

        [Tooltip("未读 提示物体：根据 是否已读的状态 设置提示对象的显示或隐藏。")] 
        [SerializeField] private GameObject isUnReadTip;
        [Tooltip("未读 图片颜色")] 
        [SerializeField] private Color isUnReadImageColor = Color.white;
        [Tooltip("未读 文本颜色")] 
        [SerializeField] private Color isUnReadTxtColor = Color.white;

        [Tooltip("已读 提示物体：根据 是否已读的状态 设置提示对象的显示或隐藏。")] 
        [SerializeField] private GameObject isReadTip;
        [Tooltip("已读 图片颜色")] 
        [SerializeField] private Color isReadImageColor = Color.gray;
        [Tooltip("已读 文本颜色")] 
        [SerializeField] private Color isReadTxtColor = Color.gray;

        /// <summary>
        /// 选项数据。
        /// 被设置时，触发UI显示的更新。
        /// </summary>
        public override Response response
        {
            get => base.response;
            set
            {
                base.response = value;
                if (response != null)
                {
                    var entry = response?.destinationEntry;
                    if (entry == null) return;

                    if (VnStoryManager.Instance == false) return;
                    // 获取 选项是否已读 全局变量的名称
                    string conversationResponseButtonVariableIsRead =
                        VnStoryManager.Instance.ConversationResponseButtonVariableIsReadFieldTitle;
                    string isReadVariableName =
                        Field.LookupValue(entry.fields, conversationResponseButtonVariableIsRead);
                    // 获取 选项是否已读 的值
                    bool isRead = DialogueLua.GetVariable(isReadVariableName).AsBool;

                    // 根据 是否已读 的状态，设置 UI显示
                    // 设置 提示对象 显示或隐藏
                    if (isUnReadTip != null) isUnReadTip.SetActive(!isRead);
                    if (isReadTip != null) isReadTip.SetActive(isRead);
                    // 设置 文本颜色
                    if (stateTxtArray != null)
                    {
                        var txtColor = isRead ? isReadTxtColor : isUnReadTxtColor;
                        foreach (var txt in stateTxtArray)
                        {
                            if (txt == null) continue;
                            // 选项按钮会被 DialogueSystem 复用，同一实例可能在上一次 0.2s 变色未结束时又被赋新 response。
                            // ToolkitTween 不做覆盖管理，故先打断该目标上的在途补间，避免两个补间同帧争写导致闪色。
                            ToolkitTween.Kill(txt);
                            ToolkitTween.TintGraphic(txt, txtColor, 0.2f, unscaled: false);
                        }
                    }
                    // 设置 图片颜色
                    if (stateImageArray != null)
                    {
                        foreach (var img in stateImageArray)
                        {
                            if (img != null) img.color = isRead ? isReadImageColor : isUnReadImageColor;
                        }
                    }
                }
            }
        }
    }
}
