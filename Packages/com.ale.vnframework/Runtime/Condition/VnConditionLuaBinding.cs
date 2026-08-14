using System;
using System.Collections.Generic;
using System.Globalization;
using Ale.Condition;
using UnityEngine;
using UnityEngine.Scripting;

namespace Ale.VnFramework.Conditions
{
    /// <summary>
    /// 单个判定器在 Lua 侧的绑定。每个判定器一个实例，作为 <c>Lua.RegisterFunction</c> 的 <c>target</c>；
    /// 函数体从 <see cref="Eval0"/>…<see cref="Eval8"/> 这族方法里**按参数个数**挑一个。
    ///
    /// <para><b>为什么是一族定长方法而不是 <c>params object[]</c></b>：Dialogue System 的
    /// <c>LuaMethodFunction.InvokeMethod</c> 直接 <c>Method.Invoke(target, args)</c>，
    /// 既不做变参展开也没有默认值，实参个数与签名差一个就抛 <c>TargetParameterCountException</c>。</para>
    ///
    /// <para><b>为什么形参一律 <see cref="object"/></b>：Lua 侧的数字传进来是装箱的
    /// <see cref="float"/>（不是 <c>double</c>，见 <c>LuaInterpreterExtensions.LuaValueToObject</c>）。
    /// 声明成 <c>int</c> 会因窄化当场抛异常；声明成 <c>double</c> 又挡不住使用者手写的字符串。
    /// 统一收成 object 再由本类自己转换，还能在转换失败时给出可读告警而不是抛栈。</para>
    /// </summary>
    internal sealed class VnConditionLuaBinding
    {
        private static readonly object[] NoArgs = new object[0];

        private readonly IConditionEvaluator _evaluator;
        private readonly IReadOnlyList<ConditionParamDef> _schema;
        private readonly string _luaFunctionName;

        internal VnConditionLuaBinding(IConditionEvaluator evaluator, string luaFunctionName)
        {
            _evaluator = evaluator;
            _luaFunctionName = luaFunctionName;
            _schema = evaluator.ParamSchema ?? Array.Empty<ConditionParamDef>();
        }

        // ── Lua 入口。只经 MethodInfo 反射调用，代码里零引用 —— 对 IL2CPP 裁剪器而言
        //    是「没有调用方的方法」，故一律 [Preserve]（与 VnStoryManager 的 DS 消息处理器同理）。
        [Preserve] public bool Eval0() => Invoke(NoArgs);
        [Preserve] public bool Eval1(object a0) => Invoke(new[] { a0 });
        [Preserve] public bool Eval2(object a0, object a1) => Invoke(new[] { a0, a1 });
        [Preserve] public bool Eval3(object a0, object a1, object a2) => Invoke(new[] { a0, a1, a2 });
        [Preserve] public bool Eval4(object a0, object a1, object a2, object a3) => Invoke(new[] { a0, a1, a2, a3 });
        [Preserve] public bool Eval5(object a0, object a1, object a2, object a3, object a4) => Invoke(new[] { a0, a1, a2, a3, a4 });
        [Preserve] public bool Eval6(object a0, object a1, object a2, object a3, object a4, object a5) => Invoke(new[] { a0, a1, a2, a3, a4, a5 });
        [Preserve] public bool Eval7(object a0, object a1, object a2, object a3, object a4, object a5, object a6) => Invoke(new[] { a0, a1, a2, a3, a4, a5, a6 });
        [Preserve] public bool Eval8(object a0, object a1, object a2, object a3, object a4, object a5, object a6, object a7) => Invoke(new[] { a0, a1, a2, a3, a4, a5, a6, a7 });

        private bool Invoke(object[] args)
        {
            try
            {
                List<ConditionParam> parameters;
                if (!TryBuildParameters(args, out parameters)) return false;
                return _evaluator.Evaluate(parameters, VnConditionBridge.Context);
            }
            catch (Exception e)
            {
                // 绝不让异常穿回 Lua 解释器：DS 的 LuaMethodFunction 捕获后原样 rethrow，
                // 轻则打断本次链接评估、重则中断整段对话。判 false 更安全也更好查。
                VnConditionLog.WarnOnce("throw:" + _luaFunctionName,
                    $"{_luaFunctionName} 求值时抛异常，本条件按不成立处理。详见下一条异常日志。");
                Debug.LogException(e);
                return false;
            }
        }

        private bool TryBuildParameters(object[] args, out List<ConditionParam> parameters)
        {
            parameters = new List<ConditionParam>(_schema.Count);
            for (int i = 0; i < _schema.Count; i++)
            {
                // 元数由注册时挑的 EvalN 保证，理论上不会短缺；缺位按 null 走默认值，不抛。
                var raw = i < args.Length ? args[i] : null;
                ConditionParam param;
                if (!TryBuildParameter(_schema[i], raw, out param))
                {
                    parameters = null;
                    return false;
                }
                parameters.Add(param);
            }
            return true;
        }

        private bool TryBuildParameter(ConditionParamDef def, object raw, out ConditionParam param)
        {
            param = new ConditionParam(def.id, def.type, def.isArray, def.enumTypeRef);

            if (!def.isArray) return TrySetElement(def, param, raw, 0);

            // Dialogue System 的参数类型里没有数组，生成器把数组参数映射成一个 '|' 分隔的字符串。
            var text = ToText(raw);
            if (string.IsNullOrEmpty(text)) return true; // 空串 = 空数组，合法

            var parts = text.Split('|');
            for (int i = 0; i < parts.Length; i++)
            {
                if (!TrySetElement(def, param, parts[i], i)) return false;
            }
            return true;
        }

        private bool TrySetElement(ConditionParamDef def, ConditionParam param, object raw, int index)
        {
            switch (def.type)
            {
                case ConditionParamType.String:
                    param.SetString(ToText(raw), index);
                    return true;

                case ConditionParamType.Float:
                    param.SetFloat(ToDouble(raw), index);
                    return true;

                case ConditionParamType.Bool:
                    param.SetBool(ToBool(raw), index);
                    return true;

                case ConditionParamType.Int:
                case ConditionParamType.Enum:
                    long ordinal;
                    if (!TryResolveOrdinal(def, raw, out ordinal)) return false;
                    param.SetInt(ordinal, index);
                    return true;

                default:
                    param.SetString(ToText(raw), index);
                    return true;
            }
        }

        /// <summary>
        /// 把 Lua 侧传来的值解析成 <see cref="ConditionParam.ints"/> 要的整数。
        /// 三种来源：固定选项的**标签**、枚举的**成员名**、以及普通数字。
        /// </summary>
        private bool TryResolveOrdinal(ConditionParamDef def, object raw, out long value)
        {
            value = 0L;

            // ① 固定选项（如「大于 / 大于等于 / 等于 …」）。Toolkit 存的是索引，但 DS 的参数类型里
            //    没有通用枚举下拉，生成器把它映射成 String 传标签，这里转回索引。
            if (def.choices != null && def.choices.Length > 0)
            {
                var label = ToText(raw);
                if (label != null)
                {
                    var trimmed = label.Trim();
                    for (int i = 0; i < def.choices.Length; i++)
                    {
                        if (string.Equals(def.choices[i], trimmed, StringComparison.Ordinal))
                        {
                            value = i;
                            return true;
                        }
                    }
                }

                // 也接受手写的数字索引，方便老脚本与手工微调。
                double numeric;
                if (TryToDouble(raw, out numeric) && numeric >= 0d && numeric < def.choices.Length)
                {
                    value = (long)numeric;
                    return true;
                }

                // 拼错标签必须报出来并判 false：静默放行会让条件形同虚设。
                VnConditionLog.WarnOnce($"choice:{_luaFunctionName}:{def.id}:{label}",
                    $"{_luaFunctionName} 的参数「{def.label}」取值 \"{label}\" 不是合法选项。" +
                    $"可选：{string.Join(" / ", def.choices)}。本条件按不成立处理。");
                return false;
            }

            // ② 枚举成员名。核心不解释 enumTypeRef（注释原话「核心不解释」），这里尽力而为。
            if (def.type == ConditionParamType.Enum && !string.IsNullOrEmpty(def.enumTypeRef))
            {
                var enumType = ResolveEnumType(def.enumTypeRef);
                var text = ToText(raw);
                if (enumType != null && !string.IsNullOrEmpty(text))
                {
                    try
                    {
                        value = Convert.ToInt64(Enum.Parse(enumType, text.Trim(), true), CultureInfo.InvariantCulture);
                        return true;
                    }
                    catch (ArgumentException)
                    {
                        // 不是成员名，落到 ③ 按数字再试一次。
                    }
                }
            }

            // ③ 普通整数。
            double d;
            if (TryToDouble(raw, out d))
            {
                value = (long)Math.Round(d, MidpointRounding.AwayFromZero);
                return true;
            }

            VnConditionLog.WarnOnce($"int:{_luaFunctionName}:{def.id}",
                $"{_luaFunctionName} 的参数「{def.label}」需要一个整数，实际收到 \"{ToText(raw)}\"。" +
                "本条件按不成立处理。");
            return false;
        }

        // ── 类型转换 ────────────────────────────────────────────────────────────────

        private static readonly Dictionary<string, Type> EnumTypeCache = new Dictionary<string, Type>();

        private static Type ResolveEnumType(string typeRef)
        {
            Type cached;
            if (EnumTypeCache.TryGetValue(typeRef, out cached)) return cached;

            var type = Type.GetType(typeRef, false);
            if (type == null)
            {
                // enumTypeRef 通常不带程序集限定，只能挨个程序集找。结果进缓存，不会重复扫描。
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    type = assembly.GetType(typeRef, false);
                    if (type != null) break;
                }
            }
            if (type != null && !type.IsEnum) type = null;

            EnumTypeCache[typeRef] = type;
            return type;
        }

        private static string ToText(object raw)
        {
            if (raw == null) return null;
            var s = raw as string;
            return s ?? Convert.ToString(raw, CultureInfo.InvariantCulture);
        }

        private static bool TryToDouble(object raw, out double value)
        {
            value = 0d;
            if (raw == null) return false;
            if (raw is float f) { value = f; return true; }
            if (raw is double d) { value = d; return true; }
            if (raw is bool b) { value = b ? 1d : 0d; return true; }

            var s = raw as string;
            if (s != null) return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

            try { value = Convert.ToDouble(raw, CultureInfo.InvariantCulture); return true; }
            catch (Exception) { return false; }
        }

        private static double ToDouble(object raw)
        {
            double value;
            return TryToDouble(raw, out value) ? value : 0d;
        }

        private static bool ToBool(object raw)
        {
            if (raw is bool b) return b;

            double numeric;
            if (TryToDouble(raw, out numeric)) return Math.Abs(numeric) > double.Epsilon;

            return string.Equals(ToText(raw), "true", StringComparison.OrdinalIgnoreCase);
        }
    }
}
