using System;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LegendOfMortalMod.Features
{
    // 存檔備註與格數：讀檔、存檔頁的手動存檔格數（遊戲是 20 格），以及每一格的備註。
    // 格子就是存檔檔名的號碼；調小、關掉或移除 mod 後，多出來的存檔只是不顯示，檔案還在。
    // 備註存在 SlotData（BepInEx\config），不寫進遊戲存檔；刪除存檔時一起刪掉
    internal static class SaveSlots
    {
        public const int Min = 20,
            Max = 99;
        private const string NoteName = "Note";

        // 太長會超出存檔格
        private const int NoteLength = 20;
        public static bool Enabled = true;
        public static int Count = 40;
        private static int Shown => Enabled ? Count : Min;

        // 讀檔、存檔頁在 Awake 依 _slotCount 建格子
        public static void Expand(MonoBehaviour panel) => Traverse.Create(panel).Field("_slotCount").SetValue(Shown);

        // 頁面建好之後才改了格數或開關：打開時照新的格數重建
        public static void Fit(MonoBehaviour panel)
        {
            Traverse t = Traverse.Create(panel);
            if (t.Field("_slotCount").GetValue<int>() == Shown)
                return;
            try
            {
                t.Field("_slotCount").SetValue(Shown);
                t.Method("CreateSlot").GetValue();
                t.Method("SetButtonNavigation").GetValue();
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"存檔格數：重建失敗 {ex.GetBaseException().Message}");
            }
        }

        // ---- 備註：LoadSlotPanel / SaveSlotPanel.Setup 之後 ----

        // 有存檔的格子在存檔時間那一行的右端放「備註」按鈕，備註顯示在存檔時間下面那一行。都放在格子底下而不是
        // 文字底下：滑鼠移過去時遊戲會改文字的樣子，跟著動的按鈕就點不到
        public static void SlotSetup(MonoBehaviour slot)
        {
            Traverse t = Traverse.Create(slot);
            string id = t.Field("_slot").GetValue<string>();
            Text[] times = t.Field("_timeText").GetValue<Text[]>() ?? Array.Empty<Text>();
            Transform existing = slot.transform.Find("LegendOfMortalMod.Note");
            Transform shownNote = slot.transform.Find("LegendOfMortalMod.NoteText");
            bool show = Enabled && t.Property("HasSaveData").GetValue<bool>() && times.Length > 0;
            if (!show)
            {
                existing?.gameObject.SetActive(false);
                shownNote?.gameObject.SetActive(false);
                return;
            }
            Text time = times[0];
            RectTransform line = time.rectTransform;
            RectTransform owner = (RectTransform)slot.transform;
            int size = Mathf.Max(14, Mathf.RoundToInt(time.fontSize * 0.8f));
            Vector2 buttonSize = new(size * 3.2f, size * 1.5f);
            Vector3 start = owner.InverseTransformPoint(
                line.TransformPoint(new Vector3(line.rect.xMin, line.rect.center.y))
            );
            float right = owner.rect.xMax - size * 0.5f;

            Button button =
                existing != null
                    ? existing.GetComponent<Button>()
                    : UiButton.Create(slot.transform, time.font, "備註", "LegendOfMortalMod.Note");
            // 最後畫，蓋在格子的反白、外框上面才點得到
            button.transform.SetAsLastSibling();
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => Edit(slot, id));
            RectTransform rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = owner.pivot;
            rect.pivot = new Vector2(1, 0.5f);
            rect.sizeDelta = buttonSize;
            rect.anchoredPosition = new Vector2(right, start.y);
            button.GetComponentInChildren<Text>().fontSize = size;
            button.gameObject.SetActive(true);

            string note = SlotData.Get(id, NoteName);
            if (note.Length == 0)
            {
                shownNote?.gameObject.SetActive(false);
                return;
            }
            Text text = shownNote != null ? shownNote.GetComponent<Text>() : NoteText(slot.transform);
            text.font = time.font;
            text.fontSize = size;
            text.color = time.color;
            RectTransform place = text.rectTransform;
            float width = right - start.x;
            place.anchorMin = place.anchorMax = owner.pivot;
            place.pivot = new Vector2(0, 0.5f);
            place.sizeDelta = new Vector2(width, size * 1.4f);
            place.anchoredPosition = new Vector2(start.x, start.y - time.fontSize * 1.2f);
            // 從存檔時間的左邊到格子右端，放不下就從後面截掉、加「…」（完整的在編輯視窗看）
            for (int keep = note.Length; keep >= 0; keep--)
            {
                text.text = keep == note.Length ? note : note.Substring(0, keep) + "…";
                if (text.preferredWidth <= width)
                    break;
            }
            text.gameObject.SetActive(true);
        }

        private static Text NoteText(Transform slot)
        {
            GameObject obj = new("LegendOfMortalMod.NoteText", typeof(RectTransform), typeof(Text));
            obj.transform.SetParent(slot, false);
            Text text = obj.GetComponent<Text>();
            text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.supportRichText = false;
            text.raycastTarget = false;
            return text;
        }

        private static MonoBehaviour _editing;
        private static string _editingSlot;
        private static string _draft = "";
        private static bool _focus;
        private static bool _navigation;
        private static GUIStyle _title;
        private static GUIStyle _field;
        private static GUIStyle _button;
        private static Texture2D _fill;

        // 編輯備註時擋住滑鼠和按鍵，免得 Enter 讀了選到的存檔、Delete 開了刪除確認
        public static bool Editing => (object)_editing != null;

        private static void Edit(MonoBehaviour slot, string id)
        {
            _editing = slot;
            _editingSlot = id;
            _draft = SlotData.Get(id, NoteName);
            _focus = true;
            if (EventSystem.current != null)
            {
                _navigation = EventSystem.current.sendNavigationEvents;
                EventSystem.current.sendNavigationEvents = false;
            }
        }

        private static void Close(bool save)
        {
            if (save)
                SlotData.Set(_editingSlot, NoteName, _draft.Trim());
            MonoBehaviour slot = _editing;
            _editing = null;
            if (EventSystem.current != null)
                EventSystem.current.sendNavigationEvents = _navigation;
            if (save && slot != null)
                Traverse.Create(slot).Method("Setup").GetValue();
        }

        // OnGUI 裡、選單縮放之前畫，用螢幕像素
        public static void DrawEditor()
        {
            // 沒在編輯；Unity 的 == null 對已刪除的物件也成立，所以先用 object 比
            if ((object)_editing == null)
                return;
            // 頁面被關掉（例如按了 Esc）或格子被刪掉就不存，但要把選單操作還回去
            if (_editing == null || !_editing.isActiveAndEnabled)
            {
                Close(false);
                return;
            }
            float k = Screen.height / 1080f;
            if (_title == null)
            {
                _fill = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
                _fill.SetPixel(0, 0, new Color(0.05f, 0.06f, 0.07f, 0.96f));
                _fill.Apply();
            }
            _title = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(20 * k),
                normal = { textColor = new Color(0.91f, 0.83f, 0.66f) },
            };
            _field = new GUIStyle(GUI.skin.textField) { fontSize = Mathf.RoundToInt(20 * k) };
            _button = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(18 * k) };

            Rect box = new((Screen.width - 640 * k) / 2, (Screen.height - 190 * k) / 2, 640 * k, 190 * k);
            float pad = 20 * k;
            GUI.DrawTexture(box, _fill);
            GUI.Label(
                new Rect(box.x + pad, box.y + pad, box.width - pad * 2, 32 * k),
                $"第 {_editingSlot} 格的備註（最多 {NoteLength} 字）",
                _title
            );
            GUI.SetNextControlName("LegendOfMortalMod.Note");
            _draft = GUI.TextField(
                new Rect(box.x + pad, box.y + pad + 44 * k, box.width - pad * 2, 40 * k),
                _draft,
                NoteLength,
                _field
            );
            if (_focus)
            {
                GUI.FocusControl("LegendOfMortalMod.Note");
                _focus = false;
            }
            Event e = Event.current;
            bool enter = e.type == EventType.KeyDown && e.keyCode is KeyCode.Return or KeyCode.KeypadEnter;
            float y = box.yMax - pad - 40 * k,
                w = 120 * k;
            if (GUI.Button(new Rect(box.xMax - pad - w * 3 - 20 * k, y, w, 40 * k), "儲存", _button) || enter)
                Close(true);
            else if (GUI.Button(new Rect(box.xMax - pad - w * 2 - 10 * k, y, w, 40 * k), "清除", _button))
            {
                _draft = "";
                Close(true);
            }
            else if (GUI.Button(new Rect(box.xMax - pad - w, y, w, 40 * k), "取消", _button))
                Close(false);
            if (enter)
                e.Use();
        }
    }
}
