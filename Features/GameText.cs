using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using HarmonyLib;
using Mortal.Core;

namespace LegendOfMortalMod.Features
{
    // 把遊戲的旗標、比較條件轉成選單裡顯示的文字，戰鬥資訊和劇情旗標共用
    internal static class GameText
    {
        private static readonly Dictionary<string, string> Ops = new Dictionary<string, string>
        {
            { "Equal", "=" },
            { "NotEqual", "≠" },
            { "Less", "<" },
            { "LessEqual", "≤" },
            { "Greater", ">" },
            { "GreaterEqual", "≥" },
        };

        public static string Op(object op) =>
            op == null ? ""
            : Ops.TryGetValue(op.ToString(), out string symbol) ? symbol
            : op.ToString();

        // 備註可能好幾行，只取第一行
        public static string Flag(FlagData flag) =>
            flag == null ? "?"
            : string.IsNullOrEmpty(flag.DevNote) ? flag.name
            : $"{flag.name}（{flag.DevNote.Split('\n')[0].Trim()}）";

        public static string Field(object obj, string field) =>
            obj == null ? "" : Traverse.Create(obj).Field(field).GetValue()?.ToString() ?? "";

        // ConditionResultItem：一組以且/或連起來的 StatCompareItem
        public static string Conditions(object item) =>
            string.Join(
                Field(item, "_logicOp") == "OR" ? " 或 " : " 且 ",
                Plugin.Items(item, "_items").Select(Compare)
            );

        public static bool Check(object item) => Traverse.Create(item).Method("Check").GetValue() is true;

        public static string Mark(bool ok) => ok ? "<color=#A3B86C>✔</color>" : "<color=#CD594A>✘</color>";

        private static string Compare(object item) =>
            $"{Value(Traverse.Create(item).Field("_value1").GetValue<StatGroupVariable>())} "
            + $"{Op(Traverse.Create(item).Field("_compareOp").GetValue())} "
            + Value(Traverse.Create(item).Field("_value2").GetValue<StatGroupVariable>());

        // 常數只顯示數字，旗標顯示名稱和備註，其他顯示資料名稱；後面附目前的值
        private static string Value(StatGroupVariable variable)
        {
            if (variable == null)
                return "?";
            int result = variable.GetResult();
            if (variable.name.StartsWith("常數_"))
                return result.ToString();
            FlagData flag = Plugin
                .Items(variable, "_value")
                .Select(reference => Traverse.Create(reference).Field("_flag").GetValue<FlagData>())
                .FirstOrDefault(found => found != null);
            string label =
                flag != null && variable.name.StartsWith("旗標_")
                    ? Flag(flag)
                    : Regex.Replace(variable.name, "^(數值|時間|旗標)_", "");
            return $"{label}({result})";
        }
    }
}
