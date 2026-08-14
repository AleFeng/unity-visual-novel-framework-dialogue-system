using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using Ale.Toolkit.Editor;
using static Ale.Toolkit.Editor.ToolkitEditorL10n;

namespace Ale.VnFramework.Editor
{
    /// <summary>
    /// VN Framework 欢迎窗口：插件的统一入口，集中「前置条件自检 / 插件支持（编译宏）/ 查看文档」。
    ///
    /// <para>界面语言与 <c>ATK_*</c> 宏是项目级全局设定，归 Ale Toolkit 的欢迎窗口管，本窗口只提供跳转按钮。</para>
    /// </summary>
    public class VnFrameworkWelcomeWindow : EditorWindow
    {
        private const string PackageName = "com.ale.vnframework";
        private const string SetupDocPath = "Packages/" + PackageName + "/Docs~/Setup/PixelCrushers/README.md";
        private const string ReadmePath = "Packages/" + PackageName + "/README.md";
        private const string ManualPath = "Packages/" + PackageName + "/Docs~/VnStoryManager/VnStoryManager.md";
        private const string LogoPath = "Packages/" + PackageName + "/Docs~/Images/VnFramework_Logo.png";

        // 打开时的初始高宽；窗口可手动调整（只设 minSize，min==max 会禁止缩放）。
        private static readonly Vector2 WindowSize = new Vector2(560f, 680f);
        private static readonly Vector2 MinWindowSize = new Vector2(520f, 500f);

        private bool _initialized;
        private bool _autoShow;
        private bool _pendingRecompile;
        private Vector2 _scroll;

        private bool _fsEnabled;
        private bool _fsInstalled;

        private Texture2D _logoTexture;
        private bool _logoLoadAttempted;

        #region 打开窗口

        [MenuItem("Tools/Ale Toolkit/VN Framework/Welcome", priority = 1020)]
        public static void Open()
        {
            OpenWindow();
        }

        private static void OpenWindow()
        {
            var window = GetWindow<VnFrameworkWelcomeWindow>(true, Tr("VN Framework 剧情演出框架"), true);
            window.minSize = MinWindowSize;
            window.CenterOnMainWin();
            window.Show();
            window.Focus();
        }

        private void CenterOnMainWin()
        {
            var main = EditorGUIUtility.GetMainWindowPosition();
            float x = main.x + (main.width - WindowSize.x) * 0.5f;
            float y = main.y + (main.height - WindowSize.y) * 0.5f;
            position = new Rect(x, y, WindowSize.x, WindowSize.y);
        }

        #endregion

        #region 生命周期

        private void OnEnable()
        {
            _initialized = false;
            _logoTexture = null;
            _logoLoadAttempted = false;
        }

        private void OnDisable()
        {
            if (_logoTexture)
            {
                DestroyImmediate(_logoTexture);
                _logoTexture = null;
            }
        }

        // 惰性初始化：EditorStyles 与 PlayerSettings 在域重载期间访问不安全，故不放 OnEnable。
        private void LoadState()
        {
            if (_initialized) return;
            _initialized = true;

            _fsEnabled = VnFrameworkDefines.IsFsGameFrameworkEnabled();
            _fsInstalled = VnFrameworkDefines.IsFsGameFrameworkInstalled();
            _autoShow = EditorPrefs.GetBool(VnFrameworkEditorPrefs.WelcomeAutoShow, true);

            RefreshPreflight();
        }

        #endregion

        #region UI界面

        private void OnGUI()
        {
            LoadState();
            titleContent.text = Tr("VN Framework 剧情演出框架");

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            DrawHeader();
            EditorGUILayout.Space(8);

            DrawGlobalSettingsLink();
            DrawSeparator();

            DrawPreflightSection();
            DrawSeparator();

            DrawMacroSection();
            DrawSeparator();

            DrawDocSection();

            EditorGUILayout.EndScrollView();

            DrawSeparator();
            DrawFooter();
        }

        private static void DrawSeparator()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField(string.Empty, GUI.skin.horizontalSlider);
            EditorGUILayout.Space(2);
        }

        // 这些样式原先若写在 OnGUI 里，鼠标在窗口上动一下就要 new 几十次。
        // EditorStyles.* 要等编辑器 GUI 初始化后才可用，故不能做成静态字段初始化器，改为首次绘制时惰性构造。
        private static GUIStyle _styleHeader;
        private static GUIStyle _styleSub;
        private static GUIStyle _styleOk;
        private static GUIStyle _styleWarn;

        private static void EnsureStyles()
        {
            if (_styleHeader != null) return;

            _styleHeader = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 18,
                alignment = TextAnchor.MiddleCenter
            };
            _styleSub = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.6f, 0.6f, 0.6f) }
            };
            _styleOk = new GUIStyle(EditorStyles.miniLabel)
                { normal = { textColor = new Color(0.3f, 0.8f, 0.3f) } };
            _styleWarn = new GUIStyle(EditorStyles.miniLabel)
                { wordWrap = true, normal = { textColor = new Color(0.9f, 0.7f, 0.2f) } };
        }

        private void DrawHeader()
        {
            EnsureStyles();

            EditorGUILayout.BeginVertical(GUILayout.Height(56));
            GUILayout.FlexibleSpace();

            EditorGUILayout.Space(20);
            var logo = GetLogoTexture();
            if (logo)
            {
                const int displaySize = 128;
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                Rect logoRect = GUILayoutUtility.GetRect(
                    displaySize, displaySize,
                    GUILayout.Width(displaySize), GUILayout.Height(displaySize));
                GUI.DrawTexture(logoRect, logo, ScaleMode.ScaleToFit, true);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space(6);
            }

            EditorGUILayout.LabelField($"Ale VN Framework  v{Version}", _styleHeader);
            EditorGUILayout.LabelField(Tr("基于 Dialogue System 的视觉小说剧情演出框架"), _styleSub);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();
        }

        private static void DrawGlobalSettingsLink()
        {
            if (GUILayout.Button(Tr("打开 Ale Toolkit 设置（语言 / 插件宏）"), GUILayout.Height(24)))
                ToolkitWelcomeWindow.Open();
        }

        #endregion

        #region 版本号

        private static string _version;

        /// <summary>
        /// 从 <c>package.json</c> 动态读取，不写死常量——写死的版本号迟早会与包脱节。
        /// </summary>
        private static string Version
        {
            get
            {
                if (!string.IsNullOrEmpty(_version)) return _version;
                var pkg = UnityEditor.PackageManager.PackageInfo.FindForAssembly(
                    typeof(VnFrameworkWelcomeWindow).Assembly);
                _version = pkg != null && !string.IsNullOrEmpty(pkg.version) ? pkg.version : "?";
                return _version;
            }
        }

        #endregion

        #region 前置条件自检

        private bool _dsAssemblyFound;
        private string[] _templateLeakFiles = Array.Empty<string>();

        /// <summary>
        /// 自检能查出什么、查不出什么：
        ///
        /// <para>「Dialogue System 完全没有 asmdef」这个状态<b>查不到</b>——那种情况下本包根本编译不过，
        /// 本窗口也就不存在。所以这里显示的 DS 程序集状态只是一条确认信息。</para>
        ///
        /// <para>真正有价值的是第二项：官方那套 asmdef 把 <c>DialogueSystem.asmdef</c> 放在
        /// <c>Dialogue System/</c> 根目录，会把 <c>Templates/Scripts/Editor/</c> 下 3 个纯编辑器脚本
        /// 卷进<b>运行时</b>程序集。它们用了 <c>UnityEditor</c> 却没有 <c>#if UNITY_EDITOR</c> 保护，
        /// 于是<b>编辑器里编译不报错、只在出包时失败</b>——「控制台零错误」这道关卡拦不住它。
        /// 这里按程序集的源文件列表来判定，与 Pixel Crushers 装在哪个目录无关。</para>
        /// </summary>
        private void RefreshPreflight()
        {
            _dsAssemblyFound = false;
            _templateLeakFiles = Array.Empty<string>();

            var ds = FindAssembly(AssembliesType.Player) ?? FindAssembly(AssembliesType.Editor);
            if (ds == null) return;

            _dsAssemblyFound = true;

            var leaked = new List<string>();
            foreach (var f in ds.sourceFiles)
            {
                if (f.Replace('\\', '/').Contains("/Templates/Scripts/Editor/"))
                    leaked.Add(System.IO.Path.GetFileName(f));
            }
            _templateLeakFiles = leaked.ToArray();
        }

        private static Assembly FindAssembly(AssembliesType type)
        {
            try
            {
                foreach (var a in CompilationPipeline.GetAssemblies(type))
                    if (a.name == "DialogueSystem") return a;
            }
            catch (Exception)
            {
                // 某些 Unity 版本在特定时机取 Player 程序集会抛，交给调用方回退。
            }
            return null;
        }

        private void DrawPreflightSection()
        {
            EnsureStyles();

            EditorGUILayout.LabelField(Tr("前置条件自检"), EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                Tr("本包是 UPM 包、代码在独立程序集里，而 asmdef 无法引用预定义程序集 Assembly-CSharp，" +
                   "因此 Dialogue System 必须先有自己的 Assembly Definitions。"),
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(2);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // ① DS 程序集
            if (_dsAssemblyFound)
                EditorGUILayout.LabelField(Tr("  ✓ Dialogue System 程序集已就位"), _styleOk);
            else
                EditorGUILayout.LabelField(Tr("  ⚠ 未找到 DialogueSystem 程序集"), _styleWarn);

            // ② Templates/Scripts/Editor 泄漏（只在出包时才炸的那条）
            if (_templateLeakFiles.Length == 0)
            {
                EditorGUILayout.LabelField(Tr("  ✓ 编辑器模板脚本未混入运行时程序集"), _styleOk);
            }
            else
            {
                EditorGUILayout.LabelField(
                    Fmt("  ⚠ 有 {0} 个纯编辑器脚本被并入了运行时程序集 DialogueSystem：{1}",
                        _templateLeakFiles.Length, string.Join(", ", _templateLeakFiles)),
                    _styleWarn);
                EditorGUILayout.LabelField(
                    Tr("它们使用 UnityEditor 却没有 #if UNITY_EDITOR 保护。编辑器下编译不报错，" +
                       "但出包（Build）时会失败。按下方文档补一个 DialogueSystemTemplatesEditor.asmdef 即可。"),
                    EditorStyles.wordWrappedMiniLabel);
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(Tr("查看 asmdef 设置说明"), GUILayout.Height(22)))
                OpenDoc(SetupDocPath);
            if (GUILayout.Button(Tr("重新检测"), GUILayout.Width(80), GUILayout.Height(22)))
                RefreshPreflight();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        #endregion

        #region 插件支持（编译宏）

        private void DrawMacroSection()
        {
            EditorGUILayout.LabelField(Tr("插件支持（编译宏）"), EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                Tr("按项目实际接入的外部系统启用。TextMeshPro / Localization / Addressables 那一组 ATK_* 宏" +
                   "是项目级全局设定，在 Ale Toolkit 欢迎窗口配置。"),
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(2);

            DrawMacroToggle(
                "Fs Game Framework", VnFrameworkDefines.FsGameFramework, VnFrameworkDefines.PackageFs,
                ref _fsEnabled, _fsInstalled,
                Tr("启用后接入 Fs GameFramework 的相关系统功能。本版本只接入音频系统：" +
                   "剧情演出的音频接缝 VnStoryAudio 会转调 AudioManager，BGM 按通道播放（同通道交叉淡入淡出），" +
                   "环境音 / 音效 / 语音按 Key 播放。关闭时四个接口为空操作——不报错、不出声，演出流程照常推进。"),
                Tr("  ⚠ 未检测到 {0} 的音频系统"),
                Tr("尚未检测到 Fs GameFramework 的音频系统（Fs.GameFramework.Common.AudioSystem）。\n" +
                   "启用宏后，VnStoryAudio 将无法编译。\n\n确定要继续启用吗？"));
        }

        private void DrawMacroToggle(string titleName, string define, string runtimeName,
            ref bool enabled, bool runtimeInstalled, string description,
            string missingLabel, string warnDialog)
        {
            EnsureStyles();
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUI.BeginChangeCheck();
            bool newEnabled = EditorGUILayout.ToggleLeft(
                $"{titleName}  ({define})", enabled, EditorStyles.boldLabel);
            if (EditorGUI.EndChangeCheck())
            {
                if (newEnabled && !runtimeInstalled)
                {
                    if (!EditorUtility.DisplayDialog(Tr("警告"), warnDialog, Tr("确定"), Tr("取消")))
                        newEnabled = false;
                }
                if (newEnabled != enabled)
                {
                    enabled = newEnabled;
                    DefineUtils.ApplyDefine(enabled, define);
                    _pendingRecompile = true;
                }
            }

            if (runtimeInstalled)
                EditorGUILayout.LabelField(Fmt("  ✓ {0} 已安装", runtimeName), _styleOk);
            else
                EditorGUILayout.LabelField(string.Format(missingLabel, runtimeName), _styleWarn);

            EditorGUILayout.LabelField(description, EditorStyles.wordWrappedMiniLabel);

            if (_pendingRecompile)
            {
                EditorGUILayout.LabelField(Tr("  ⏳ 宏定义已更改，等待 Unity 重新编译…"), _styleWarn);
                if (!EditorApplication.isCompiling) _pendingRecompile = false;
            }

            EditorGUILayout.EndVertical();
        }

        #endregion

        #region 文档

        private void DrawDocSection()
        {
            EditorGUILayout.LabelField(Tr("文档"), EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(Tr("查看说明文档"), GUILayout.Height(24)))
                OpenDoc(ReadmePath);
            if (GUILayout.Button(Tr("查看使用文档"), GUILayout.Height(24)))
                OpenDoc(ManualPath);
            EditorGUILayout.EndHorizontal();
        }

        private static void OpenDoc(string packageRelativePath)
        {
            string absolutePath = System.IO.Path.GetFullPath(packageRelativePath);

            if (System.IO.File.Exists(absolutePath))
            {
                Application.OpenURL("file:///" + absolutePath.Replace('\\', '/'));
            }
            else
            {
                EditorUtility.DisplayDialog(Tr("文档未找到"),
                    Fmt("未能找到文档文件：\n{0}", packageRelativePath), Tr("确定"));
            }
        }

        #endregion

        #region Logo 与页脚

        /// <summary>
        /// <c>FilterMode.Point</c> 让放大时像素边缘保持锐利。结果缓存，避免每帧重复 I/O。
        /// Logo 不存在时静默跳过绘制（<c>Docs~</c> 不被 AssetDatabase 索引，只能走文件系统读取）。
        /// </summary>
        private Texture2D GetLogoTexture()
        {
            if (_logoTexture) return _logoTexture;
            if (_logoLoadAttempted) return null;

            _logoLoadAttempted = true;

            string logoPath = System.IO.Path.GetFullPath(LogoPath);
            if (!System.IO.File.Exists(logoPath)) return null;

            byte[] bytes = System.IO.File.ReadAllBytes(logoPath);
            var tex = new Texture2D(64, 64, TextureFormat.RGBA32, false, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            if (tex.LoadImage(bytes))
                _logoTexture = tex;
            else
                DestroyImmediate(tex);

            return _logoTexture;
        }

        private void DrawFooter()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            EditorGUI.BeginChangeCheck();
            bool newAutoShow = EditorGUILayout.ToggleLeft(Tr("启动时自动显示"), _autoShow, GUILayout.Width(140));
            if (EditorGUI.EndChangeCheck())
            {
                _autoShow = newAutoShow;
                EditorPrefs.SetBool(VnFrameworkEditorPrefs.WelcomeAutoShow, _autoShow);
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(4);
        }

        #endregion
    }
}
