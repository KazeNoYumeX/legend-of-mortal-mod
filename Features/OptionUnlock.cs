using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Fungus;
using HarmonyLib;
using Mortal.Core;
using UnityEngine;
using UnityEngine.UI;
using static LegendOfMortalMod.Features.GameText;

namespace LegendOfMortalMod.Features
{
    // 快速解鎖：鎖住的選項（「解鎖條件：？？？」）旁邊放「快速解鎖」，把沒滿足的條件改到滿足，選項當場解鎖。
    // 議題、走訪這類選單會把條件不滿足的選項整列藏起來，選單標題旁的「另有 N 個選項未滿足條件」列出它們和條件，
    // 也能快速解鎖。解鎖後選它走的是遊戲自己的分支（遊戲加選項時就把點擊事件掛在按鈕上了）
    internal static class OptionUnlock
    {
        // 劇情腳本裡鎖住的選項寫成 t[列] = 條件變數 and "開放時的文字" or "~鎖住時的文字"
        private static readonly Regex ConditionCall = new(
            @"local\s+(\w+)\s*=\s*checkpointmanager\.Condition\(\s*""([^""]+)""\s*\)"
        );
        private static readonly Regex LockedEntry = new(
            @"\w+\[(\d+)\]\s*=\s*(\w+)\s+and\s+""([^""]*)""\s+or\s+""~([^""]*)"""
        );

        private sealed class Entry
        {
            public int Row;
            public string Condition,
                Open,
                Locked;
        }

        // 最近幾段有鎖住選項的腳本，最新的在前；選單可能是前一段腳本開的
        private static readonly List<List<Entry>> Recent = new();
        private static readonly Dictionary<Transform, Button> Buttons = new();
        private static bool _enabled;

        public static bool Enabled
        {
            get => _enabled;
            set
            {
                if (_enabled == value)
                    return;
                _enabled = value;
                // 選單已經開著時也馬上加上 / 拿掉按鈕
                foreach (NormalOptionButton row in Object.FindObjectsOfType<NormalOptionButton>())
                    row.UpdateContent();
                _dirty = true;
            }
        }

        public static void ScriptLoaded(string lua)
        {
            Dictionary<string, string> conditions = new();
            foreach (Match match in ConditionCall.Matches(lua ?? ""))
                conditions[match.Groups[1].Value] = match.Groups[2].Value;
            List<Entry> entries = LockedEntry
                .Matches(lua ?? "")
                .Cast<Match>()
                .Where(match => conditions.ContainsKey(match.Groups[2].Value))
                .Select(match => new Entry
                {
                    Row = int.Parse(match.Groups[1].Value),
                    Condition = conditions[match.Groups[2].Value],
                    Open = match.Groups[3].Value,
                    Locked = match.Groups[4].Value,
                })
                .ToList();
            if (entries.Count == 0)
                return;
            Recent.Insert(0, entries);
            if (Recent.Count > 3)
                Recent.RemoveAt(Recent.Count - 1);
        }

        // 鎖住的選項也照表的順序加進選單，所以它在選單按鈕裡的位置就是表的列號；鎖住的文字也要一樣。
        // 同一段腳本裡對到兩個不同的選項就不回答
        private static Entry Find(MenuDialog menu, Component row, string source)
        {
            int index = menu == null ? -1 : System.Array.IndexOf(menu.CachedButtons, row.GetComponent<Button>());
            string locked = (source ?? "").TrimStart('~');
            foreach (List<Entry> entries in Recent)
            {
                List<Entry> found = entries
                    .Where(entry => entry.Row == index + 1 && entry.Locked == locked)
                    .GroupBy(entry => entry.Condition + "\t" + entry.Open)
                    .Select(group => group.First())
                    .ToList();
                if (found.Count == 1)
                    return found[0];
                if (found.Count > 1)
                    return null;
            }
            return null;
        }

        private static Text Source(Component row) => Traverse.Create(row).Field("_source").GetValue<Text>();

        // ---- 鎖住的選項：NormalOptionButton.UpdateContent 之後 ----

        public static void RowUpdated(NormalOptionButton row)
        {
            if (Buttons.TryGetValue(row.transform, out Button shown) && shown != null)
                shown.gameObject.SetActive(false);
            if (!_enabled || row.GetComponent<Button>().interactable)
                return;
            Entry entry = Find(row.GetComponentInParent<MenuDialog>(), row, Source(row)?.text);
            if (entry == null || Plan(StoryFlags.ConditionData(entry.Condition)) == null)
                return;
            Text line = Traverse.Create(row).Field("_text").GetValue<Text[]>()?.FirstOrDefault();
            if (line == null)
                return;
            if (shown == null)
            {
                // 換場景後舊的選項列都被刪了，順便清掉
                foreach (Transform gone in Buttons.Keys.Where(key => key == null).ToList())
                    Buttons.Remove(gone);
                Buttons[row.transform] = shown = UiButton.Create(row.transform, line.font, "快速解鎖");
            }
            shown.onClick.RemoveAllListeners();
            shown.onClick.AddListener(() =>
            {
                if (Apply(entry))
                    Open(row, entry);
            });
            // 放在列的右邊，避開右端的雲紋
            RectTransform rect = (RectTransform)shown.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1, 0.5f);
            rect.sizeDelta = new Vector2(line.fontSize * 5f, line.fontSize * 1.4f);
            rect.anchoredPosition = new Vector2(-((RectTransform)row.transform).rect.width * 0.12f, 0);
            shown.GetComponentInChildren<Text>().fontSize = Mathf.RoundToInt(line.fontSize * 0.7f);
            shown.gameObject.SetActive(true);
        }

        // 換成開放的選項：按鈕可按、文字換回開放時的，再讓遊戲重畫這一列
        private static void Open(Component row, Entry entry)
        {
            row.GetComponent<Button>().interactable = true;
            if (Source(row) is Text source)
                source.text = entry.Open;
            row.gameObject.SetActive(true);
            ((IMenuDialogOption)row).UpdateContent();
        }

        // ---- 讓條件成立要改的值 ----

        // 一個要改的值：條件裡看到的值（屬性是加成後的）從 From 改成 To
        private sealed class Change
        {
            public ScriptableObject Asset;
            public int From,
                To;
        }

        // ConditionResultData 的各組條件任一組成立就好，挑要改的值最少的那組；做不到回傳 null，已經成立回傳空的
        private static List<Change> Plan(object condition) =>
            Plugin
                .Items(condition, "_items")
                .Select(PlanGroup)
                .Where(plan => plan != null)
                .OrderBy(plan => plan.Count)
                .FirstOrDefault();

        // 一組條件：「且」每項都要成立（同一個值不能要兩種數字），「或」挑最省的一項
        private static List<Change> PlanGroup(object item)
        {
            List<List<Change>> parts = Plugin.Items(item, "_items").Select(PlanCompare).ToList();
            if (Field(item, "_logicOp") == "OR")
                return parts.Where(part => part != null).OrderBy(part => part.Count).FirstOrDefault();
            if (parts.Count == 0 || parts.Contains(null))
                return null;
            List<Change> changes = new();
            foreach (Change change in parts.SelectMany(part => part))
            {
                Change same = changes.Find(other => other.Asset == change.Asset);
                if (same == null)
                    changes.Add(change);
                else if (same.To != change.To)
                    return null;
            }
            return changes;
        }

        // 「值 比較 常數」：把值改到剛好成立；常數在左邊就把比較反過來
        private static List<Change> PlanCompare(object compare)
        {
            if (Check(compare))
                return new List<Change>();
            Traverse t = Traverse.Create(compare);
            StatGroupVariable left = t.Field("_value1").GetValue<StatGroupVariable>(),
                right = t.Field("_value2").GetValue<StatGroupVariable>();
            StatCompareType op = t.Field("_compareOp").GetValue<StatCompareType>();
            if (Constant(right) is int c)
                return Reach(left, op, c);
            if (Constant(left) is int d)
                return Reach(right, Swap(op), d);
            return null;
        }

        private static int? Constant(StatGroupVariable group)
        {
            List<object> values = Plugin.Items(group, "_value").ToList();
            return values.Count == 1 && Field(values[0], "_checkType") == nameof(StatCheckType.常數)
                ? ((StatValueReference)values[0]).GetValue()
                : null;
        }

        private static StatCompareType Swap(StatCompareType op) =>
            op switch
            {
                StatCompareType.Less => StatCompareType.Greater,
                StatCompareType.LessEqual => StatCompareType.GreaterEqual,
                StatCompareType.Greater => StatCompareType.Less,
                StatCompareType.GreaterEqual => StatCompareType.LessEqual,
                _ => op,
            };

        // 一個值就直接改；幾項相加（例如六道加起來 > 1）就把差額補在其中一項：往上補挑第一個還是 0 的，
        // 往下扣挑最大的，不扣到 0 以下
        private static List<Change> Reach(StatGroupVariable group, StatCompareType op, int c)
        {
            int target = op switch
            {
                StatCompareType.Greater or StatCompareType.NotEqual => c + 1,
                StatCompareType.Less => c - 1,
                _ => c,
            };
            List<object> values = Plugin.Items(group, "_value").ToList();
            if (values.Count != 1 && Field(group, "_groupType") != "Sum")
                return null;
            int gap = target - group.GetResult();
            List<(ScriptableObject Asset, int Value)> writable = values
                .Select(Writable)
                .Where(w => w.Asset != null)
                .ToList();
            if (writable.Count == 0)
                return null;
            (ScriptableObject Asset, int Value) chosen =
                gap < 0 ? writable.OrderByDescending(w => w.Value).First()
                : writable.Any(w => w.Value == 0) ? writable.First(w => w.Value == 0)
                : writable[0];
            int value = chosen.Value + gap;
            if (values.Count > 1 && value < 0)
                return null;
            return new List<Change>
            {
                new()
                {
                    Asset = chosen.Asset,
                    From = chosen.Value,
                    To = value,
                },
            };
        }

        // 選單能改的值和它在條件裡的值；負號的值不處理
        private static (ScriptableObject Asset, int Value) Writable(object reference)
        {
            Traverse t = Traverse.Create(reference);
            if (t.Field("_negative").GetValue<bool>())
                return (null, 0);
            ScriptableObject asset = t.Property("CheckType").GetValue<StatCheckType>() switch
            {
                StatCheckType.角色數值 => t.Field("_stat").GetValue<GameStat>(),
                StatCheckType.好感度 => t.Field("_relationship").GetValue<RelationshipStat>(),
                StatCheckType.旗標 => t.Field("_flag").GetValue<FlagData>(),
                StatCheckType.書籍 => t.Field("_book").GetValue<ScriptableObject>(),
                StatCheckType.雜物 => t.Field("_misc").GetValue<ScriptableObject>(),
                StatCheckType.貴重品 => t.Field("_specialItem").GetValue<ScriptableObject>(),
                StatCheckType.技能 => t.Field("_talentData").GetValue<PlayerTalentData>(),
                _ => null,
            };
            return asset == null ? (null, 0) : (asset, ((StatValueReference)reference).GetValue());
        }

        private static bool Apply(Entry entry)
        {
            object condition = StoryFlags.ConditionData(entry.Condition);
            return Write(Plan(condition), () => Met(condition), $"快速解鎖「{Name(entry)}」");
        }

        // 本段劇情的分支判定：只看這一個分支（一組條件）能不能靠改數值成立，前面的分支不管。
        // Switch 取第一個成立的分支，前面的也成立時仍走前面的；判定在腳本跑到那行時才做一次，已經做過就要下次進劇情才生效。
        // 只認目前與最近的劇情腳本，大地圖事件的條件（MissionManagerData）不在範圍內
        public static bool CanSet(object branch) => PlanGroup(branch) is { Count: > 0 };

        public static void SetBranch(object branch, string label)
        {
            List<Change> plan = PlanGroup(branch);
            // 清單 0.5 秒才更新一次，按鈕還在時再按一次已經沒有要改的，不寫進回想
            if (plan is { Count: > 0 })
                Write(plan, () => Check(branch), $"分支設定「{label}」");
        }

        // 寫入計畫的值，寫完再檢查一次；不成立就改回原本的值。改了什麼寫進回想和記錄檔
        private static bool Write(List<Change> plan, System.Func<bool> holds, string what)
        {
            if (plan == null)
                return false;
            List<(ScriptableObject Asset, int Value)> before = plan.Select(c => (c.Asset, Plugin.GetValue(c.Asset)))
                .ToList();
            foreach (Change change in plan)
            {
                // 條件比的是加成後的值，選單改的是原本的值
                int value = change.Asset is GameStat stat ? stat.Value + change.To - stat.FinalValue : change.To;
                Plugin.Instance.ApplyValue(change.Asset, value);
            }
            string text = $"{what}：{Describe(plan)}";
            if (!holds())
            {
                // 例如要的值超過屬性上限，改了也不會成立
                foreach ((ScriptableObject asset, int value) in before)
                    Plugin.Instance.ApplyValue(asset, value);
                Plugin.Log.LogWarning($"{text} 改完仍不成立，已改回");
                return false;
            }
            Plugin.Log.LogInfo(text);
            try
            {
                StoryFlags.AddToLog($"<size=18><color=#BCA136>{text}</color></size>");
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogWarning($"寫進回想失敗 {ex.GetBaseException().Message}");
            }
            return true;
        }

        // 「修養 3→5、Talk_Option_01 1→0」；條件本來就成立時沒有要改的
        private static string Describe(List<Change> plan) =>
            plan.Count == 0
                ? "條件已成立，不用改"
                : string.Join("、", plan.Select(c => $"{ValueName(c.Asset)} {c.From}→{c.To}"));

        private static string ValueName(ScriptableObject asset) =>
            asset switch
            {
                GameStat stat => stat.StatType.ToString(),
                RelationshipStat relationship => $"{relationship.Type}好感",
                FlagData flag => Flag(flag),
                ItemData item => Plugin.ItemName(item),
                PlayerTalentData talent => Plugin.TalentName(talent),
                _ => asset.name,
            };

        // 選項的名稱；議題選項的文字是「類型+key+行動+貢獻」
        private static string Name(Entry entry)
        {
            string[] parts = entry.Open.Split('+');
            string key = parts.Length == 4 ? parts[1] : entry.Open;
            string name = LocalizationManager.Instance.LocaleResolver.GetStoryText(key);
            return Regex.Replace(string.IsNullOrEmpty(name) ? key : name, "<[^>]+>", "").Trim();
        }

        // 不用 Execute：它會累計判定次數
        private static bool Met(object condition) => Plugin.Items(condition, "_items").Any(Check);

        // ---- 被藏起來的選項 ----

        private sealed class Hidden
        {
            public string Name,
                Need,
                Change;
            public bool Met,
                Writable;
            public BreakOptionButton Row;
            public Entry Entry;
        }

        private static MenuDialog _menu;
        private static bool _dirty;
        private static bool _expanded;
        private static List<Hidden> _hidden = new();
        private static GUIStyle _caption,
            _hint,
            _name,
            _need,
            _button;
        private static Texture2D _fill;
        private static float _styleScale;

        // 上一次畫的範圍（螢幕像素，左上原點），Plugin 用它擋住底下遊戲的點擊
        public static Rect PopupRect { get; private set; }

        public static void BreakRowUpdated(Component row)
        {
            MenuDialog menu = row.GetComponentInParent<MenuDialog>();
            if (menu == null)
                return;
            if (menu != _menu)
                _expanded = false;
            _menu = menu;
            _dirty = true;
        }

        // 遊戲加進選單但整列藏起來的選項：還留在按鈕裡原本的位置
        private static List<Hidden> HiddenRows()
        {
            List<Hidden> rows = new();
            Button[] buttons = _menu.CachedButtons;
            int used = Mathf.Min(Traverse.Create(_menu).Field("nextOptionIndex").GetValue<int>(), buttons.Length);
            foreach (Button button in buttons.Take(used))
            {
                if (
                    button == null
                    || button.gameObject.activeSelf
                    || button.interactable
                    || button.GetComponent<BreakOptionButton>() is not BreakOptionButton row
                )
                    continue;
                Entry entry = Find(_menu, row, Source(row)?.text);
                object condition = entry == null ? null : StoryFlags.ConditionData(entry.Condition);
                if (condition == null)
                    continue;
                List<Change> plan = Plan(condition);
                rows.Add(
                    new Hidden
                    {
                        Name = Name(entry),
                        Need = string.Join(
                            "\n或 ",
                            Plugin.Items(condition, "_items").Select(item => $"{Mark(Check(item))} {Conditions(item)}")
                        ),
                        Met = Met(condition),
                        Writable = plan != null,
                        Change = plan == null ? "" : Describe(plan),
                        Row = row,
                        Entry = entry,
                    }
                );
            }
            return rows;
        }

        // OnGUI 裡、選單縮放之前畫，用螢幕像素
        public static void DrawHidden()
        {
            PopupRect = default;
            List<Button> visible =
                _enabled && _menu != null && _menu.isActiveAndEnabled
                    ? _menu
                        .CachedButtons.Where(b => b != null && b.gameObject.activeInHierarchy && b.interactable)
                        .ToList()
                    : new List<Button>();
            // 遊戲自己的選單、回想蓋住選單時，選單按鈕會關掉，這裡也跟著不畫
            if (visible.Count == 0)
            {
                _expanded = false;
                return;
            }
            if (_dirty && Event.current.type == EventType.Layout)
            {
                _hidden = HiddenRows();
                _dirty = false;
            }
            if (_hidden.Count == 0)
                return;

            float k = Screen.height / 1080f;
            Styles(k);
            string caption = $"另有 {_hidden.Count} 個選項未滿足條件 {(_expanded ? "▾" : "▸")}";
            const string refresh = "修改條件後點擊本按鈕重新整理選項";
            Vector2 size = new(
                Mathf.Max(_caption.CalcSize(new GUIContent(caption)).x, _hint.CalcSize(new GUIContent(refresh)).x)
                    + 24 * k,
                52 * k
            );
            Rect rows = visible.Select(b => ScreenRect((RectTransform)b.transform)).Aggregate(Union);
            // 選單大標題（「休憩」）的右邊，找不到就放在最下面一列的下方
            Text title = (
                _menu.transform.Find("Container/Title/Text") ?? _menu.transform.Find("Title/Text")
            )?.GetComponent<Text>();
            Vector2 position = new(rows.xMin, rows.yMax + 6 * k);
            if (title != null && title.isActiveAndEnabled)
            {
                Rect t = ScreenRect(title.rectTransform);
                float local = Mathf.Max(1, title.rectTransform.rect.width);
                position = new Vector2(
                    t.xMin + Mathf.Min(title.preferredWidth, local) * t.width / local + 18 * k,
                    t.center.y - size.y / 2
                );
            }
            Rect header = new(
                Mathf.Clamp(position.x, 8, Screen.width - size.x - 8),
                Mathf.Clamp(position.y, 8, Screen.height - size.y - 8),
                size.x,
                size.y
            );

            // 清單：每個選項一段，名稱可能很長（其他 mod 也會在選項文字後面加說明），換行後再接條件
            float pad = 12 * k,
                width = Mathf.Min(Mathf.Max(rows.width, 560 * k), Screen.width - 24),
                buttonWidth = 130 * k,
                gap = 8 * k;
            List<(float Name, float Need)> heights = _hidden
                .Select(row =>
                    (
                        Mathf.Max(
                            34 * k,
                            _name.CalcHeight(new GUIContent(row.Name), width - pad * 2 - buttonWidth - gap)
                        ),
                        _need.CalcHeight(new GUIContent(Body(row)), width - pad * 2)
                    )
                )
                .ToList();
            Rect box = default;
            if (_expanded)
            {
                float height = Mathf.Min(heights.Sum(h => h.Name + h.Need + gap) + pad * 2, Screen.height - 24);
                float y =
                    header.yMax + 4 * k + height <= Screen.height - 8 ? header.yMax + 4 * k : header.y - 4 * k - height;
                box = new Rect(Mathf.Clamp(header.x, 12, Screen.width - width - 12), Mathf.Max(8, y), width, height);
            }
            PopupRect = _expanded ? Union(header, box) : header;
            // 點別的地方就收起來，點擊照樣給遊戲
            if (
                Event.current.type == EventType.MouseDown
                && _expanded
                && !PopupRect.Contains(Event.current.mousePosition)
            )
                _expanded = false;

            GUI.DrawTexture(header, _fill);
            GUI.Label(new Rect(header.x, header.y + 3 * k, header.width, header.height * 0.55f), caption, _caption);
            GUI.Label(
                new Rect(header.x, header.y + header.height * 0.52f, header.width, header.height * 0.42f),
                refresh,
                _hint
            );
            if (GUI.Button(header, GUIContent.none, GUIStyle.none))
            {
                // 用選單改過數值、條件已經成立的選項直接出現；一個都沒有就展開或收起
                List<Hidden> met = HiddenRows().Where(row => row.Met).ToList();
                met.ForEach(row => Open(row.Row, row.Entry));
                _hidden = HiddenRows();
                if (met.Count == 0)
                    _expanded = !_expanded;
            }
            if (!_expanded)
                return;

            GUI.DrawTexture(box, _fill);
            GUI.BeginGroup(box);
            float top = pad;
            for (int i = 0; i < _hidden.Count; i++)
            {
                Hidden row = _hidden[i];
                GUI.Label(new Rect(pad, top, width - pad * 2 - buttonWidth - gap, heights[i].Name), row.Name, _name);
                if (
                    row.Writable
                    && GUI.Button(new Rect(width - pad - buttonWidth, top, buttonWidth, 36 * k), "快速解鎖", _button)
                    && Apply(row.Entry)
                )
                {
                    Open(row.Row, row.Entry);
                    _dirty = true;
                }
                top += heights[i].Name;
                GUI.Label(new Rect(pad, top, width - pad * 2, heights[i].Need), Body(row), _need);
                top += heights[i].Need + gap;
            }
            GUI.EndGroup();
        }

        // 條件，以及按快速解鎖會改哪些值
        private static string Body(Hidden row) =>
            row.Writable && !row.Met ? $"　條件：{row.Need}\n　會改：{row.Change}" : $"　條件：{row.Need}";

        private static Rect Union(Rect a, Rect b) =>
            Rect.MinMaxRect(
                Mathf.Min(a.xMin, b.xMin),
                Mathf.Min(a.yMin, b.yMin),
                Mathf.Max(a.xMax, b.xMax),
                Mathf.Max(a.yMax, b.yMax)
            );

        private static Rect ScreenRect(RectTransform rect)
        {
            Canvas canvas = rect.GetComponentInParent<Canvas>()?.rootCanvas;
            Camera camera =
                canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector2 a = RectTransformUtility.WorldToScreenPoint(camera, corners[0]),
                b = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
            return Rect.MinMaxRect(a.x, Screen.height - b.y, b.x, Screen.height - a.y);
        }

        private static void Styles(float k)
        {
            if (_caption != null && Mathf.Approximately(_styleScale, k))
                return;
            _styleScale = k;
            if (_fill == null)
            {
                _fill = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
                _fill.SetPixel(0, 0, new Color(0.05f, 0.06f, 0.07f, 0.94f));
                _fill.Apply();
            }
            _name = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(18 * k),
                richText = true,
                wordWrap = true,
                normal = { textColor = new Color(0.91f, 0.83f, 0.66f) },
            };
            _need = new GUIStyle(_name) { fontSize = Mathf.RoundToInt(16 * k), normal = { textColor = Color.white } };
            _caption = new GUIStyle(_name) { wordWrap = false, alignment = TextAnchor.MiddleCenter };
            // 和左邊選項名稱一樣大的字
            _button = new GUIStyle(GUI.skin.button) { fontSize = _name.fontSize };
            _hint = new GUIStyle(_caption)
            {
                fontSize = Mathf.RoundToInt(13 * k),
                normal = { textColor = new Color(0.76f, 0.79f, 0.81f) },
            };
        }
    }
}
