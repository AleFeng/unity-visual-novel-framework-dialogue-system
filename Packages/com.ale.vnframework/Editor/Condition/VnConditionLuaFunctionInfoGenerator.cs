using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Ale.Condition;
using Ale.Condition.Editor;
using Ale.VnFramework.Conditions;
using PixelCrushers.DialogueSystem;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace Ale.VnFramework.Editor.ConditionSupport
{
    /// <summary>
    /// 把 Toolkit 的条件判定器登记成 Dialogue System 的自定义 Lua 函数信息资产
    /// （<see cref="CustomLuaFunctionInfo"/>），使它们出现在节点 <b>Conditions ▸ Custom</b> 下拉里。
    ///
    /// <para><b>为什么非要有这份资产</b>：条件向导枚举函数走的是
    /// <c>AssetDatabase.FindAssets("t:CustomLuaFunctionInfo")</c>（见 <c>LuaWizardBase</c>），
    /// <b>它根本不读 Lua 环境</b>。所以哪怕函数已经注册好了，没有资产也一个都不会出现在下拉里。</para>
    ///
    /// <para><b>为什么写进 <c>Assets/</c> 而不是包内</b>：使用者经 git URL 安装时包目录落在
    /// <c>Library/PackageCache</c>，只读，写不进去。</para>
    ///
    /// <para><b>两级触发</b>：首次创建走菜单并弹框征得同意（沿用
    /// <c>VnFrameworkDemoAddressables</c> 立下的「绝不擅自改写使用者资产」规矩）；
    /// 资产一旦存在，即视为已授权，此后每次脚本重编译都比对内容、<b>有差异才写</b>。
    /// 后者是必需的 —— 第三方系统新增判定器后若还要使用者记得来点一次按钮，
    /// 漏点的表现是「新条件不出现在下拉里」，且毫无提示。</para>
    /// </summary>
    internal static class VnConditionLuaFunctionInfoGenerator
    {
        private const string LogPrefix = "[VnCondition]";

        /// <summary>
        /// 资产名。用于在工程里认领「这份是我们生成的」。
        /// ⚠️ 改名会让本工具认不出它，从而在默认位置再建一份；但位置可以随意搬。
        /// ⚠️ 也绝不能叫 <c>DialogueSystemLuaFunctionInfo</c> —— 那个名字被 Dialogue System 视为
        /// 内置函数表，会跑进 <b>Misc</b> 下拉而不是 <b>Custom</b>（见 <c>LuaWizardBase</c> 的 isBuiltin 判定）。
        /// </summary>
        internal const string AssetName = "VnFrameworkConditionLuaFunctions";

        private const string DefaultFolder = "Assets/AleVnFramework";
        private static string DefaultPath => DefaultFolder + "/" + AssetName + ".asset";

        // ── 触发入口 ────────────────────────────────────────────────────────────────

        [MenuItem("Tools/Ale Toolkit/VN Framework/Generate Condition Lua Functions", priority = 1021)]
        private static void GenerateFromMenu()
        {
            var existing = FindAsset();
            if (existing == null)
            {
                var proceed = EditorUtility.DisplayDialog(
                    "生成条件函数登记资产",
                    $"将创建：\n{DefaultPath}\n\n" +
                    "Dialogue System 的条件向导只认工程内的 CustomLuaFunctionInfo 资产（它不读 Lua 环境），" +
                    "有了这份资产，Toolkit 的判定器才会出现在节点的 Conditions ▸ Custom 下拉里。\n\n" +
                    "创建之后，判定器增减时本资产会在脚本重编译后自动同步，无需再手动点。",
                    "创建", "取消");
                if (!proceed) return;
            }

            string report;
            ApplyGeneration(out report);
            Debug.Log($"{LogPrefix} {report}");
            EditorUtility.DisplayDialog("生成条件函数登记资产", report, "好");
        }

        /// <summary>
        /// 脚本重编译后同步登记资产。
        ///
        /// <para>⚠️ <b>同步执行，不要改回 <c>EditorApplication.delayCall</c> 推迟。</b>
        /// <c>delayCall</c> 依赖 <c>EditorApplication.update</c> 走一拍，而编辑器失焦时它可能长时间不推进
        /// ——「在外部 IDE 改完代码、Unity 在后台重编译」恰恰是最常见的情形，同步就此静默失效，
        /// 直到使用者切回 Unity 才补上。实测在无人值守的编辑器里根本不触发。</para>
        ///
        /// <para>此处写资产是安全的：<see cref="DidReloadScripts"/> 晚于域重载与
        /// <c>InitializeOnLoad</c>，AssetDatabase 已完全可用；而「有差异才写」+ 生成结果确定，
        /// 保证了写入触发的再导入不会引出第二次写入，不存在循环。</para>
        /// </summary>
        [DidReloadScripts]
        private static void AutoSync()
        {
            // 资产不存在 = 使用者从没主动创建过 → 什么都不做，绝不擅自往他的 Assets/ 里写东西。
            if (FindAsset() == null) return;

            try
            {
                string report;
                if (ApplyGeneration(out report)) Debug.Log($"{LogPrefix} {report}");
            }
            catch (Exception e)
            {
                // 同步失败不该把编辑器搞崩，也不该拦住这次重编译。
                Debug.LogWarning($"{LogPrefix} 自动同步条件函数登记资产失败，详见下一条异常日志。");
                Debug.LogException(e);
            }
        }

        // ── 执行体（无模态框，可被自动化直接调用）────────────────────────────────────

        /// <summary>生成 / 同步登记资产。返回是否**真的写了**磁盘。</summary>
        internal static bool ApplyGeneration(out string report)
        {
            // TypeCache 在重编译后会变，缓存必须刷新，否则新增的判定器同步不进来。
            ConditionEvaluatorCatalog.Rebuild();

            List<string> notes;
            var records = BuildRecords(out notes);

            var asset = FindAsset();
            bool wrote;
            string action;

            if (asset == null)
            {
                EnsureFolder(DefaultFolder);
                asset = ScriptableObject.CreateInstance<CustomLuaFunctionInfo>();
                asset.conditionFunctions = records.ToArray();
                asset.scriptFunctions = Array.Empty<CustomLuaFunctionInfoRecord>();
                AssetDatabase.CreateAsset(asset, DefaultPath);
                AssetDatabase.SaveAssets();
                wrote = true;
                action = "已创建";
            }
            else if (RecordsEqual(asset.conditionFunctions, records))
            {
                wrote = false;
                action = "无变化";
            }
            else
            {
                // 只动 conditionFunctions：scriptFunctions 不归本工具管，别顺手清掉使用者填的东西。
                asset.conditionFunctions = records.ToArray();
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssets();
                wrote = true;
                action = "已更新";
            }

            report = Describe(action, records.Count, notes, AssetDatabase.GetAssetPath(asset));
            return wrote;
        }

        private static string Describe(string action, int count, List<string> notes, string path)
        {
            var sb = new StringBuilder();
            sb.Append(action).Append("：登记了 ").Append(count).Append(" 个判定器 → ").Append(path);
            if (notes.Count > 0)
            {
                sb.Append("\n跳过 ").Append(notes.Count).Append(" 个：");
                foreach (var n in notes) sb.Append("\n  · ").Append(n);
            }
            if (count > 0)
            {
                sb.Append("\n\n在节点的 Conditions 里点「...」打开向导 → 选 Custom → ")
                  .Append(VnConditionNaming.MenuRoot).Append(" 分组。")
                  .Append("\n⚠️ 向导只在打开时扫一次资产，已经开着的话要关掉重开才看得到变化。");
            }
            return sb.ToString();
        }

        // ── 记录构建 ────────────────────────────────────────────────────────────────

        internal static List<CustomLuaFunctionInfoRecord> BuildRecords(out List<string> notes)
        {
            notes = new List<string>();
            var records = new List<CustomLuaFunctionInfoRecord>();
            var claimed = new Dictionary<string, string>();

            // ConditionEvaluatorCatalog.All 已按 Category、Key 排序 —— 顺序稳定是「重复生成结果
            // 逐字节一致」的前提，也是自动同步不制造版本控制噪音的前提。
            foreach (var evaluator in ConditionEvaluatorCatalog.All)
            {
                string luaName;
                int parameterCount;
                string skipReason;
                if (!VnConditionNaming.TryPlan(evaluator, out luaName, out parameterCount, out skipReason))
                {
                    if (evaluator != null && !string.IsNullOrEmpty(evaluator.Key))
                        notes.Add($"「{evaluator.Key}」{skipReason}");
                    continue;
                }

                string owner;
                if (claimed.TryGetValue(luaName, out owner))
                {
                    notes.Add($"「{evaluator.Key}」与「{owner}」映射出同一个函数名 {luaName}");
                    continue;
                }
                claimed[luaName] = evaluator.Key;

                var schema = evaluator.ParamSchema;
                var parameters = new CustomLuaParameterType[parameterCount];
                for (int i = 0; i < parameterCount; i++) parameters[i] = MapParameter(schema[i]);

                records.Add(new CustomLuaFunctionInfoRecord
                {
                    functionName = VnConditionNaming.ToMenuPath(evaluator.Category, luaName),
                    parameters = parameters,
                    // 必须是 Bool：向导会据此补上 " == true"，而 Lua.Result.asBool 对非 boolean
                    // 走的是 ToString() == "True"，返回数字会被判成 false。
                    returnValue = CustomLuaReturnType.Bool,
                });
            }

            return records;
        }

        /// <summary>
        /// Toolkit 参数类型 → Dialogue System 参数类型。
        /// ⚠️ 这里的约定必须与 <c>VnConditionLuaBinding</c> 的取值端一致。
        /// </summary>
        private static CustomLuaParameterType MapParameter(ConditionParamDef def)
        {
            // 数组：DS 的参数类型里没有数组，退化成一个 '|' 分隔的字符串。
            if (def.isArray) return CustomLuaParameterType.String;

            switch (def.type)
            {
                case ConditionParamType.Bool:
                    return CustomLuaParameterType.Bool;

                case ConditionParamType.Float:
                    return CustomLuaParameterType.Double;

                case ConditionParamType.String:
                    return CustomLuaParameterType.String;

                case ConditionParamType.Int:
                case ConditionParamType.Enum:
                    // 有固定选项（如「大于 / 大于等于 / …」）→ 传**标签**。DS 没有通用枚举下拉，
                    // 传标签至少让生成的 Lua 一眼看得懂，也比裸索引抗得住选项顺序调整。
                    if (def.choices != null && def.choices.Length > 0) return CustomLuaParameterType.String;
                    // 无选项的 Enum 传成员名；纯 Int 传数字。
                    return def.type == ConditionParamType.Enum
                        ? CustomLuaParameterType.String
                        : CustomLuaParameterType.Double;

                default:
                    return CustomLuaParameterType.String;
            }
        }

        // ── 资产读写 ────────────────────────────────────────────────────────────────

        /// <summary>按资产名在全工程找我们生成的那一份（允许使用者把它搬到别处）。</summary>
        internal static CustomLuaFunctionInfo FindAsset()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:" + nameof(CustomLuaFunctionInfo)))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.Equals(Path.GetFileNameWithoutExtension(path), AssetName, StringComparison.Ordinal)) continue;

                var asset = AssetDatabase.LoadAssetAtPath<CustomLuaFunctionInfo>(path);
                if (asset != null) return asset;
            }
            return null;
        }

        private static bool RecordsEqual(CustomLuaFunctionInfoRecord[] existing, List<CustomLuaFunctionInfoRecord> fresh)
        {
            if (existing == null) return fresh.Count == 0;
            if (existing.Length != fresh.Count) return false;

            for (int i = 0; i < existing.Length; i++)
            {
                var a = existing[i];
                var b = fresh[i];
                if (a == null) return false;
                if (!string.Equals(a.functionName, b.functionName, StringComparison.Ordinal)) return false;
                if (a.returnValue != b.returnValue) return false;

                var pa = a.parameters ?? Array.Empty<CustomLuaParameterType>();
                var pb = b.parameters ?? Array.Empty<CustomLuaParameterType>();
                if (pa.Length != pb.Length) return false;
                for (int j = 0; j < pa.Length; j++) if (pa[j] != pb[j]) return false;
            }
            return true;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;

            var parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            var leaf = Path.GetFileName(folder);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(leaf)) return;

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
