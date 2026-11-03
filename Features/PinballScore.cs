using HarmonyLib;
using Mortal.Pinball;
using UnityEngine;
using UnityEngine.UI;

namespace LegendOfMortalMod.Features
{
    // 彈珠台：「總分」下面一行「總分改為：[輸入框] 套用」，輸入任意分數後按 Enter 或套用。分數經過遊戲的
    // AddScore，兩行分數顯示都會更新；彈珠台之後的劇情只看總分落在哪一檔（≥100、≥60、≥30、更低），
    // 改完按遊戲的「離開」即可。機台被打壞（它自己的劇情）後不顯示
    internal static class PinballScore
    {
        private const int MaxScore = 999999;
        public static bool Enabled;
        private static PinballManager _manager;
        private static RectTransform _row;
        private static InputField _input;
        private static Text _placeholder;

        // 輸入框有焦點時，按鍵不要發射彈珠
        public static bool Editing => _input != null && _input.isFocused;

        public static void Update()
        {
            PinballManager table = PinballManager.Instance;
            if (table == null)
            {
                _manager = null;
                return;
            }
            // 每次進彈珠台場景都是新的 manager，舊的一行跟著舊場景消失
            if (table != _manager)
                Attach(table);
            if (_row == null)
                return;
            Text total = _row.parent.GetComponent<Text>();
            bool show =
                Enabled && total.isActiveAndEnabled && !Traverse.Create(table).Field("_isBroken").GetValue<bool>();
            if (_row.gameObject.activeSelf != show)
                _row.gameObject.SetActive(show);
            if (show && !_input.isFocused)
                _placeholder.text = Traverse.Create(table).Field("_totalScore").GetValue<int>().ToString();
        }

        private static void Attach(PinballManager table)
        {
            _manager = table;
            _row = null;
            Text total = Traverse.Create(table).Field("_totalText").GetValue<Text>();
            if (total == null)
            {
                Plugin.Log.LogWarning("彈珠台：找不到總分文字");
                return;
            }
            // 比 50 的分數字小一半，放在總分那一行的正下方、從它的文字開頭開始
            int size = Mathf.Max(12, Mathf.RoundToInt(total.fontSize * 0.5f));
            float height = size * 1.6f,
                gap = size * 0.4f;
            _row = new GameObject(
                "LegendOfMortalMod.PinballScore",
                typeof(RectTransform)
            ).GetComponent<RectTransform>();
            _row.SetParent(total.transform, false);
            _row.anchorMin = _row.anchorMax = new Vector2(0, 0.5f);
            _row.pivot = new Vector2(0, 1);
            _row.anchoredPosition = new Vector2(0, -total.fontSize * 0.7f);

            Text caption = Label("Caption", _row, total.font, size, total.color, TextAnchor.MiddleLeft);
            caption.text = "總分改為：";
            float x = Place(caption.rectTransform, 0, caption.preferredWidth, height) + gap;

            GameObject field = new("Input", typeof(RectTransform), typeof(Image), typeof(InputField));
            field.transform.SetParent(_row, false);
            field.GetComponent<Image>().color = new Color(0.15f, 0.18f, 0.21f, 0.9f);
            _input = field.GetComponent<InputField>();
            Color white = new(0.94f, 0.95f, 0.96f, 1);
            Text text = Label("Text", field.transform, total.font, size, white, TextAnchor.MiddleCenter);
            _placeholder = Label(
                "Placeholder",
                field.transform,
                total.font,
                size,
                white * new Color(1, 1, 1, 0.45f),
                TextAnchor.MiddleCenter
            );
            foreach (Text stretch in new[] { text, _placeholder })
            {
                stretch.rectTransform.anchorMin = Vector2.zero;
                stretch.rectTransform.anchorMax = Vector2.one;
                stretch.rectTransform.offsetMin = stretch.rectTransform.offsetMax = Vector2.zero;
            }
            _input.textComponent = text;
            _input.placeholder = _placeholder;
            _input.contentType = InputField.ContentType.IntegerNumber;
            _input.characterLimit = 6;
            _input.navigation = new Navigation { mode = Navigation.Mode.None };
            // 點別的地方也會結束輸入；只有 Enter 才算套用
            _input.onEndEdit.AddListener(_ =>
            {
                if (Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.KeypadEnter))
                    Submit();
            });
            x = Place((RectTransform)field.transform, x, size * 5f, height) + gap;

            Button apply = UiButton.Create(_row, total.font, "套用");
            apply.GetComponentInChildren<Text>().fontSize = size;
            apply.onClick.AddListener(Submit);
            x = Place((RectTransform)apply.transform, x, size * 3.4f, height);
            _row.sizeDelta = new Vector2(x, height);
            _row.gameObject.SetActive(false);
        }

        private static Text Label(string name, Transform parent, Font font, int size, Color color, TextAnchor alignment)
        {
            GameObject obj = new(name, typeof(RectTransform), typeof(Text));
            obj.transform.SetParent(parent, false);
            Text text = obj.GetComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            return text;
        }

        private static float Place(RectTransform rect, float x, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(x, 0);
            return x + width;
        }

        private static void Submit()
        {
            if (_input == null || _manager == null || !int.TryParse(_input.text, out int target))
                return;
            target = Mathf.Clamp(target, 0, MaxScore);
            _input.text = "";
            // AddScore 也會加到本局分數，降低總分時本局會變負的；只改總分，再用遊戲自己的方式更新文字
            Traverse t = Traverse.Create(_manager);
            int before = t.Field("_totalScore").GetValue<int>();
            t.Field("_totalScore").SetValue(target);
            t.Method("UpdateTotalText").GetValue();
            Plugin.Log.LogInfo($"彈珠台：總分 {before} → {target}");
        }
    }
}
