using UnityEngine;
using UnityEngine.UI;

namespace LegendOfMortalMod.Features
{
    // 放進遊戲畫面（uGUI）的小按鈕：骰子的指定、選項的一鍵設定、彈珠台的套用共用同一種樣子
    internal static class UiButton
    {
        public static Button Create(Transform parent, Font font, string text, string name = "LegendOfMortalMod.Button")
        {
            GameObject obj = new(name, typeof(RectTransform), typeof(Image), typeof(Button));
            obj.transform.SetParent(parent, false);
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

            GameObject labelObj = new("Label", typeof(RectTransform), typeof(Text));
            labelObj.transform.SetParent(obj.transform, false);
            Text label = labelObj.GetComponent<Text>();
            label.font = font != null ? font : Resources.GetBuiltinResource<Font>("Arial.ttf");
            label.fontSize = 18;
            label.text = text;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = new Color(0.94f, 0.95f, 0.96f, 1);
            label.raycastTarget = false;
            RectTransform labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
            return button;
        }
    }
}
