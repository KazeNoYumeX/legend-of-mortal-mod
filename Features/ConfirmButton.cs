using System;
using UnityEngine;

namespace LegendOfMortalMod.Features
{
    // 要按兩下的 IMGUI 按鈕：第一下把文字換成「確定嗎？」，3 秒內再按一次才回傳 true。
    // 以按鈕文字區分，同時只有一個按鈕在等確認。
    internal static class ConfirmButton
    {
        private const string Asking = "確定嗎？";
        private const float Seconds = 3f;
        private static string _pending;
        private static float _until;

        public static bool Layout(string label, params GUILayoutOption[] options) =>
            Confirmed(label, GUILayout.Button(IsAsking(label) ? Asking : label, options));

        public static bool Draw(Rect rect, string label) =>
            Confirmed(label, GUI.Button(rect, IsAsking(label) ? Asking : label));

        // 按鈕不再顯示時呼叫，免得重新出現時只按一下就執行；只清這些按鈕，別的按鈕照常等確認
        public static void Cancel(string[] labels)
        {
            if (Array.IndexOf(labels, _pending) >= 0)
                _pending = null;
        }

        private static bool IsAsking(string label) => _pending == label && Time.unscaledTime < _until;

        private static bool Confirmed(string label, bool clicked)
        {
            if (!clicked)
                return false;
            if (IsAsking(label))
            {
                _pending = null;
                return true;
            }
            _pending = label;
            _until = Time.unscaledTime + Seconds;
            return false;
        }
    }
}
