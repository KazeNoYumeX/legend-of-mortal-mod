using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Mortal.Core;
using Mortal.Story;
using UnityEngine;
using UnityEngine.UI;

namespace LegendOfMortalMod.Features
{
    // 骰子畫面每個選項左邊加一顆「指定」按鈕，按下就擲骰並落在那個選項，跳過轉盤動畫。結果仍由遊戲自己結算。
    // 改寫自 Toufff/zhaohuo-system 的 GameAdapter / NativeDicePresenter，Copyright (c) 2026 Toufff，MIT 授權，見 THIRD-PARTY-NOTICES.md
    internal static class DiceControl
    {
        private const float ButtonHeight = 32,
            MinButtonWidth = 76;
        private static DiceMenuDialog _dialog;
        private static string _name = "";
        private static int _optionCount;
        private static bool _awaitingFirstRoll,
            _rollRequested,
            _rolling,
            _observedActive,
            _failed;
        private static int? _pendingRaw,
            _pendingBranch,
            _forcedBranch;

        // 指定的那次擲骰不播轉盤、不等「繼續」，直接出結果
        private static bool _fastRoll,
            _fastRolled;

        // Index 1..選項數：能落在該選項的原始點數，可能超出轉盤範圍
        private static int?[] _raws = new int?[0];
        private static RectTransform _root;
        private static readonly List<Button> NativeRows = new List<Button>();
        private static readonly List<Button> Buttons = new List<Button>();

        public static void Begin(DiceMenuDialog dialog, string name, string[] options)
        {
            Clear();
            _dialog = dialog;
            _name = name;
            _optionCount = options?.Length ?? 0;
            _awaitingFirstRoll = true;
        }

        public static void End(DiceMenuDialog dialog)
        {
            if (ReferenceEquals(dialog, _dialog))
                Clear();
        }

        public static void BeginRoll(DiceMenuDialog dialog)
        {
            if (!ReferenceEquals(dialog, _dialog))
                return;
            _awaitingFirstRoll = false;
            _rollRequested = false;
            _rolling = true;
        }

        // CheckPointManager.Dice 也會被其他地方呼叫，只有真的擲骰才套用指定
        public static void BeforeDice(string name, ref int random)
        {
            if (!_rolling || name != _name)
                return;
            _rolling = false;
            _forcedBranch = _pendingBranch;
            if (_pendingRaw.HasValue)
            {
                random = _pendingRaw.Value;
                Traverse.Create(_dialog).Field("_currentRandomValue").SetValue(random);
            }
            _pendingRaw = null;
            _pendingBranch = null;
        }

        // 加成算法和遊戲不一致時，仍以指定的選項為準
        public static void AfterDice(string name, DiceCheckResult result)
        {
            if (_forcedBranch.HasValue && name == _name)
                result.Result = _forcedBranch.Value;
            _forcedBranch = null;
        }

        public static bool FastRoll(DiceMenuDialog dialog) => _fastRoll && ReferenceEquals(dialog, _dialog);

        public static bool FastRolled(DiceMenuDialog dialog) => _fastRolled && ReferenceEquals(dialog, _dialog);

        // 取代轉盤：遊戲在 Roll 裡已經算好結果，直接顯示在轉盤上並選中該選項，和轉完時一樣
        public static IEnumerator FastSpin(DiceMenuDialog dialog)
        {
            _fastRoll = false;
            object wheel = Traverse.Create(dialog).Field("_diceWheel").GetValue();
            int result = Traverse.Create(dialog).Field("_resultCount").GetValue<int>();
            if (wheel != null)
                Traverse.Create(wheel).Method("UpdateResult", result.ToString()).GetValue();
            dialog.UpdateSelection();
            _fastRolled = true;
            yield return null;
        }

        public static void Update(bool enabled)
        {
            if (_dialog == null || _failed)
                return;
            try
            {
                if (!_dialog.gameObject.activeInHierarchy)
                {
                    if (_observedActive)
                        Clear();
                    return;
                }
                _observedActive = true;
                if (_root == null && enabled)
                    Attach();
                if (_root == null)
                    return;
                _root.gameObject.SetActive(enabled);
                if (enabled)
                    Render();
            }
            catch (Exception ex)
            {
                // 沒有按鈕時骰子照遊戲原本的流程走
                Plugin.Log.LogWarning("DiceControl: " + ex.GetBaseException().Message);
                Clear();
                _failed = true;
            }
        }

        public static void Clear()
        {
            if (_root != null)
                UnityEngine.Object.Destroy(_root.gameObject);
            _root = null;
            _dialog = null;
            _name = "";
            _optionCount = 0;
            // _failed 不重設：壞過一次就到遊戲重開前都不再加按鈕，免得每次骰子都再報錯
            _awaitingFirstRoll = _rollRequested = _rolling = _observedActive = _fastRoll = _fastRolled = false;
            _pendingRaw = _pendingBranch = _forcedBranch = null;
            NativeRows.Clear();
            Buttons.Clear();
        }

        private static void Attach()
        {
            NativeRows.Clear();
            if (Traverse.Create(_dialog).Property("CachedButtons").GetValue() is IEnumerable cached)
            {
                foreach (object item in cached)
                {
                    if (item is Button button && button != null && button.gameObject.activeInHierarchy)
                        NativeRows.Add(button);
                }
            }
            if (NativeRows.Count == 0)
                return;

            // 鋪滿骰子畫面的空容器，本身不擋點擊，只有自己的按鈕吃得到滑鼠
            _root = new GameObject("LegendOfMortalMod.Dice", typeof(RectTransform)).GetComponent<RectTransform>();
            _root.SetParent(_dialog.transform, false);
            _root.anchorMin = Vector2.zero;
            _root.anchorMax = Vector2.one;
            _root.sizeDelta = Vector2.zero;
            _root.SetAsLastSibling();
            Font font =
                NativeRows[0].GetComponentInChildren<Text>()?.font ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            for (int i = 0; i < NativeRows.Count; i++)
                Buttons.Add(CreateButton(font, i + 1));
        }

        private static Button CreateButton(Font font, int branch)
        {
            GameObject obj = new GameObject("Branch" + branch, typeof(RectTransform), typeof(Image), typeof(Button));
            obj.transform.SetParent(_root, false);
            Image image = obj.GetComponent<Image>();
            Button button = obj.GetComponent<Button>();
            button.targetGraphic = image;
            // 手把和鍵盤仍只選得到遊戲自己的選項
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.15f, 0.18f, 0.21f, 0.75f);
            colors.highlightedColor = new Color(0.25f, 0.32f, 0.39f, 0.9f);
            colors.pressedColor = new Color(0.22f, 0.37f, 0.51f, 1);
            colors.selectedColor = colors.normalColor;
            colors.disabledColor = new Color(0.15f, 0.16f, 0.17f, 0.4f);
            button.colors = colors;

            GameObject labelObj = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelObj.transform.SetParent(obj.transform, false);
            Text label = labelObj.GetComponent<Text>();
            label.font = font;
            label.fontSize = 18;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = new Color(0.94f, 0.95f, 0.96f, 1);
            label.raycastTarget = false;
            RectTransform labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;

            button.onClick.AddListener(() => Roll(branch));
            return button;
        }

        private static void Render()
        {
            bool canSpecify = CanSpecify();
            // 一欄按鈕放在遊戲選項左邊，避開選項超出框的文字
            float columnWidth = MinButtonWidth,
                rowsLeft = float.MaxValue;
            for (int i = 0; i < Buttons.Count; i++)
            {
                Button button = Buttons[i];
                int branch = i + 1;
                bool visible =
                    branch <= _optionCount && NativeRows[i] != null && NativeRows[i].gameObject.activeInHierarchy;
                button.gameObject.SetActive(visible);
                if (!visible)
                    continue;
                Text label = button.GetComponentInChildren<Text>();
                label.text = "指定";
                button.interactable = canSpecify;
                columnWidth = Mathf.Max(columnWidth, label.preferredWidth + 20);
                rowsLeft = Mathf.Min(rowsLeft, VisualBounds(NativeRows[i]).xMin);
            }
            float columnX = Mathf.Max(_root.rect.xMin + 12, rowsLeft - columnWidth - 10);
            for (int i = 0; i < Buttons.Count; i++)
            {
                if (!Buttons[i].gameObject.activeSelf)
                    continue;
                RectTransform rect = (RectTransform)Buttons[i].transform;
                rect.anchorMin = rect.anchorMax = _root.pivot;
                rect.pivot = new Vector2(0, 1);
                rect.anchoredPosition = new Vector2(columnX, VisualBounds(NativeRows[i]).center.y + ButtonHeight / 2);
                rect.sizeDelta = new Vector2(columnWidth, ButtonHeight);
            }
        }

        // 第一次擲骰前，或遊戲顯示重擲按鈕時才能指定；按下後到真的擲骰前不能再改
        private static bool CanSpecify()
        {
            Component restart = Traverse.Create(_dialog).Field("_restartButton").GetValue<Component>();
            return !_rollRequested && (_awaitingFirstRoll || (restart != null && restart.gameObject.activeInHierarchy));
        }

        private static void Roll(int branch)
        {
            if (_dialog == null || !CanSpecify())
                return;
            try
            {
                // 按下時才算，避免每幀重找點數，也用當下的加成
                ReadBranches();
                _pendingBranch = branch;
                _pendingRaw = branch < _raws.Length ? _raws[branch] : null;
                // 只走遊戲自己的開始方式，轉盤、結果顯示和消耗都照原本的流程
                if (!_awaitingFirstRoll)
                {
                    // 和遊戲的重擲按鈕一樣：先扣命運點，再重擲
                    Traverse.Create(_dialog).Method("UseFatePoint").GetValue();
                    _dialog.PressRestartButton();
                }
                // 自動模式會自己擲，手動轉盤才要按開始
                else if (
                    Traverse.Create(AccessTools.TypeByName("SystemSettings")).Property("ManualDestiny").GetValue()
                    is true
                )
                    _dialog.PressStartButton();
            }
            catch (Exception ex)
            {
                // 擲骰沒開始就不要留著指定
                _pendingRaw = _pendingBranch = null;
                Plugin.Log.LogWarning("DiceControl: cannot roll " + ex.GetBaseException().Message);
                return;
            }
            _rollRequested = true;
            _fastRoll = true;
            _fastRolled = false;
        }

        // 遊戲擲骰是 Random.Range(min, max + 1) 加上各項加成，第一個符合的結果項就是落點。
        // 算不出點數的選項仍可指定，由 AfterDice 強制結果
        private static void ReadBranches()
        {
            _raws = new int?[_optionCount + 1];
            int min = Traverse.Create(_dialog).Field("_diceMin").GetValue<int>();
            int max = Traverse.Create(_dialog).Field("_diceMax").GetValue<int>();
            DiceResultConfig config = Traverse
                .Create(CheckPointManager.Instance)
                .Field("_dice")
                .GetValue<DiceResultConfig>();
            UnityEngine.Object data = null;
            if (config != null)
            {
                foreach (UnityEngine.Object item in config.List)
                {
                    if (item != null && item.name == _name)
                        data = item;
                }
            }
            if (data == null || (long)max - min > 10000)
                return;

            int addition = Addition(data);
            List<DiceResultItem> items = Plugin.Items(data, "_resultItems").Cast<DiceResultItem>().ToList();
            int BranchOf(long raw)
            {
                int found = items.FindIndex(item => item.Check((int)raw + addition));
                return found < 0 ? items.Count + 1 : found + 1;
            }
            void Record(long raw, int branch)
            {
                if (branch <= _optionCount && _raws[branch] == null)
                    _raws[branch] = (int)raw;
            }

            for (long raw = min; raw <= max; raw++)
                Record(raw, BranchOf(raw));
            // 範圍內擲不到的選項，往範圍外找一個能落在那裡的點數
            int upper = Math.Max(99, max);
            for (long step = 1; step <= 10000 && _raws.Skip(1).Any(raw => raw == null); step++)
            {
                foreach (long raw in new[] { min - step, max + step })
                {
                    if (raw <= upper && raw >= int.MinValue)
                        Record(raw, BranchOf(raw));
                }
            }
        }

        private static int Addition(UnityEngine.Object data)
        {
            int addition = 0;
            foreach (object item in Plugin.Items(data, "_propertyItems"))
            {
                Traverse traverse = Traverse.Create(item);
                float value = 0;
                switch (traverse.Property("Type").GetValue()?.ToString())
                {
                    case "數值":
                        if (traverse.Property("Stat").GetValue() is GameStat stat)
                            value =
                                stat.LevelLength == 0
                                    ? stat.FinalValue
                                    : (
                                        GameStatUtils.GetGameStatLevel(stat.FinalValue, stat.Max, stat.LevelLength)
                                        - stat.LevelLength / 2
                                    ) * 10;
                        break;
                    case "好感度":
                        if (traverse.Property("Relationship").GetValue() is RelationshipStat relationship)
                            value = relationship.Value;
                        break;
                    case "技能":
                        object talent = traverse.Property("Talent").GetValue();
                        if (talent != null && Traverse.Create(talent).Property("Level").GetValue() is int level)
                            value = level;
                        break;
                    case "旗標":
                        if (traverse.Property("Flag").GetValue() is FlagData flag)
                            value = flag.State;
                        break;
                }
                float rate = traverse.Property("Rate").GetValue() is float r ? r : 1f;
                bool negative = traverse.Property("Negative").GetValue() is true;
                addition += Mathf.RoundToInt(value * (negative ? -1 : 1) * rate);
            }
            return addition;
        }

        // 選項框加上超出框的文字 (遊戲讓選項文字溢出框外)，換算成 _root 的座標
        private static Rect VisualBounds(Button row)
        {
            Rect bounds = Bounds((RectTransform)row.transform);
            foreach (Text text in row.GetComponentsInChildren<Text>())
            {
                if (
                    !text.isActiveAndEnabled
                    || string.IsNullOrEmpty(text.text)
                    || text.horizontalOverflow != HorizontalWrapMode.Overflow
                )
                    continue;
                Rect rect = Bounds(text.rectTransform);
                float local = text.rectTransform.rect.width;
                float width = text.preferredWidth * (local > 0 ? rect.width / local : 1);
                float left = text.alignment switch
                {
                    TextAnchor.UpperCenter or TextAnchor.MiddleCenter or TextAnchor.LowerCenter => rect.center.x
                        - width / 2,
                    TextAnchor.UpperRight or TextAnchor.MiddleRight or TextAnchor.LowerRight => rect.xMax - width,
                    _ => rect.xMin,
                };
                bounds = Rect.MinMaxRect(
                    Mathf.Min(bounds.xMin, left),
                    bounds.yMin,
                    Mathf.Max(bounds.xMax, left + width),
                    bounds.yMax
                );
            }
            return bounds;
        }

        private static Rect Bounds(RectTransform rect)
        {
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector3 min = _root.InverseTransformPoint(corners[0]);
            Vector3 max = _root.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
    }
}
