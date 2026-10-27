using System.Linq;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace LegendOfMortalMod.Features
{
    // 存檔備註：遊戲存檔、讀檔畫面的每個手動存檔欄位，存檔時間下方右側加一個輸入框，點了就能寫一行字。
    // 備註跟著欄位走（SlotData），同一格重複存檔不會清掉，刪除存檔時一起刪
    internal static class SaveNotes
    {
        private const string Note = "note";
        private const string FieldName = "ModSaveNote";

        // SetupText 每次打開畫面都會跑；輸入框只建一次，之後更新內容。空欄位沒有存檔可寫，隱藏
        public static void Setup(Component panel, bool hasSave, string slot, Text[] times)
        {
            // 畫面剛打開時整個欄位可能還沒啟用，看自己的 activeSelf
            Text time = times?.FirstOrDefault(text => text != null && text.gameObject.activeSelf);
            if (time == null)
                return;
            Transform existing = time.transform.Find(FieldName);
            InputField field = existing != null ? existing.GetComponent<InputField>() : Create(panel, time);
            field.gameObject.SetActive(hasSave);
            field.SetTextWithoutNotify(SlotData.Get(slot, Note));
        }

        private static InputField Create(Component panel, Text time)
        {
            GameObject root = new GameObject(FieldName, typeof(RectTransform), typeof(Image), typeof(InputField));
            root.transform.SetParent(time.transform, false);
            // 時間那一行的正下方，靠右
            RectTransform rect = (RectTransform)root.transform;
            rect.anchorMin = new Vector2(0, 0);
            rect.anchorMax = new Vector2(1, 0);
            rect.pivot = new Vector2(0.5f, 1);
            rect.sizeDelta = new Vector2(0, time.fontSize + 10);
            rect.anchoredPosition = new Vector2(0, -2);
            Image background = root.GetComponent<Image>();
            background.color = new Color(1, 1, 1, 0.06f);

            InputField field = root.GetComponent<InputField>();
            field.targetGraphic = background;
            field.lineType = InputField.LineType.SingleLine;
            field.characterLimit = 40;
            field.textComponent = Label(root, time, "Text", time.color);
            field.placeholder = Label(
                root,
                time,
                "Placeholder",
                new Color(time.color.r, time.color.g, time.color.b, 0.35f)
            );
            ((Text)field.placeholder).text = "備註…";
            field.onEndEdit.AddListener(value =>
                SlotData.Set(Traverse.Create(panel).Field("_slot").GetValue<string>(), Note, value)
            );
            return field;
        }

        private static Text Label(GameObject parent, Text style, string name, Color color)
        {
            GameObject child = new GameObject(name, typeof(RectTransform), typeof(Text));
            child.transform.SetParent(parent.transform, false);
            RectTransform rect = (RectTransform)child.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(6, 0);
            rect.offsetMax = new Vector2(-6, 0);
            Text text = child.GetComponent<Text>();
            text.font = style.font;
            text.fontSize = style.fontSize;
            text.color = color;
            text.alignment = TextAnchor.MiddleRight;
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            return text;
        }
    }
}
