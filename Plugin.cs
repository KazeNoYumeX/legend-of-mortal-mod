using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.Mono;
using HarmonyLib;
using Mortal.Battle;
using Mortal.Core;
using UnityEngine;

namespace LegendOfMortalMod
{
    [BepInPlugin("LegendOfMortalMod", "活俠傳修改選單", "1.3.0")]
    public class Plugin : BaseUnityPlugin
    {
        public bool dice;
        public int diceNumber = 50;
        private readonly GUIStyle myStyle = new GUIStyle();
        private string diceInput = "50";
        public bool spendLimit;
        public int spendLimitNumber = 99;
        private string spendLimitInput = "99";
        private bool _accelerateMode;
        private Harmony _harmony;

        private Vector2Int lastScreenSize;
        private ConfigEntry<KeyCode> MenuToggleKey;
        private ConfigEntry<float> UiScale;
        private float _guiScale = 1;
        private static readonly string[] TabNames = { "基本", "角色屬性", "好感度", "旗標", "書籍", "技能" };
        private int _tab;
        private int _tabInput;
        private readonly Vector2[] _tabScroll = new Vector2[TabNames.Length];
        // Unsaved typed values and locked values, keyed by GameStat / RelationshipStat / FlagData
        private readonly Dictionary<ScriptableObject, string> _inputs = new Dictionary<ScriptableObject, string>();
        private readonly Dictionary<ScriptableObject, int> _locks = new Dictionary<ScriptableObject, int>();
        private string _search = "";
        private string _searchInput = "";
        private GUIStyle _textStyle;
        private GUIStyle _headerInputStyle;
        private GUIStyle _headerButtonStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _rowStyle;

        // Table column widths, shared by the header and the rows
        private const float NameWidth = 200, RangeWidth = 120, StateWidth = 60, InputWidth = 80, SetWidth = 50, StepWidth = 35, LockWidth = 50;
        private bool _showMenu;
        private float speed = 1;
        private string speedInput = "1";
        private Rect windowRect;
        private bool winLose;
        private static readonly LeanLocalizationResolver Locale = new LeanLocalizationResolver();
        public static Plugin Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
            
            MenuToggleKey = Config.Bind(
                "LegendOfMortalMod",
                "MenuToggleKey",
                KeyCode.F3,
                "Menu Toggle Key"
            );
            UiScale = Config.Bind(
                "LegendOfMortalMod",
                "UiScale",
                1f,
                "Menu size multiplier, on top of automatic scaling to screen height (1080p = 1x)"
            );

            _harmony = Harmony.CreateAndPatchAll(typeof(Patch));
        }

        private void Update()
        {
            if (Screen.width != lastScreenSize.x || Screen.height != lastScreenSize.y)
            {
                // Update lastScreenSize with the current screen dimensions
                lastScreenSize.x = Screen.width;
                lastScreenSize.y = Screen.height;

                OnScreenSizeChanged();
            }

            if (Input.GetKeyDown(MenuToggleKey.Value))
            {
                _showMenu = !_showMenu;
                // Unsaved edits would look like real values on reopen; drop them
                _inputs.Clear();
            }

            foreach (KeyValuePair<ScriptableObject, int> locked in _locks)
            {
                if (GetValue(locked.Key) != locked.Value)
                    SetValue(locked.Key, locked.Value);
            }

            if (_accelerateMode)
            {
                if (Time.timeScale != 0 && !Mathf.Approximately(Time.timeScale, speed))
                {
                    SetTimeScale(speed);
                }
            }
            else
            {
                if (Time.timeScale != 0)
                {
                    if (
                        !Mathf.Approximately(Time.timeScale, 10)
                        && !Mathf.Approximately(Time.timeScale, 2)
                    )
                    {
                        SetTimeScale(1);
                    }
                }
            }

            // Debug builds show these test controls; keep them hidden
            SetActive("[UI]/MainUI/Layer_1/TestDayButton", false);
            SetActive("[UI]/MainUI/Layer_1/TestNightButton", false);
            SetActive("[UI]/MainUI/Layer_5/TestAnimationPanel", false);
            SetActive("[UI]/TopPanel/MenuPanel/TestWin",  winLose);
            SetActive("[UI]/TopPanel/MenuPanel/TestLose", winLose);
        }

        private void OnGUI()
        {
            if (!_showMenu)
                return;

            Matrix4x4 oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(_guiScale, _guiScale, 1));
            windowRect = GUI.Window(123456, windowRect, DoMyWindow, "活俠傳 Mod");
            GUI.matrix = oldMatrix;
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }

        // The menu is laid out on a virtual screen 1080 / UiScale units high and scaled up to the real one
        private void OnScreenSizeChanged()
        {
            _guiScale = Screen.height / 1080f * Mathf.Max(UiScale.Value, 0.1f);
            float screenWidth = Screen.width / _guiScale;
            float screenHeight = Screen.height / _guiScale;
            float width = screenWidth * 0.5f;
            float height = screenHeight * 0.8f;
            windowRect = new Rect((screenWidth - width) / 2, (screenHeight - height) / 2, width, height);
        }

        private void SetActive(string path, bool active)
        {
            GameObject obj = GameObject.Find(path);
            if (obj != null)
            {
                obj.SetActive(active);
            }
        }

        public void DoMyWindow(int windowID)
        {
            myStyle.fontSize = 14;
            myStyle.clipping = TextClipping.Clip;
            myStyle.normal.textColor = Color.white;
            GUILayout.BeginArea(new Rect(10, 20, windowRect.width - 20, windowRect.height - 30));

            _tabInput = GUILayout.Toolbar(_tabInput, TabNames);
            // Layout and the events after it must draw the same controls, so tab and filter changes wait for the next Layout
            if (Event.current.type == EventType.Layout)
            {
                // A filter from another tab would make this one look empty
                if (_tab != _tabInput)
                    _searchInput = "";
                _tab = _tabInput;
                _search = _searchInput;
            }
            GUILayout.Space(8);

            if (_tab == 0)
                DrawBasicTab();
            else
                DrawValueTable();

            GUILayout.EndArea();
            GUI.DragWindow();
        }

        private void DrawBasicTab()
        {
            // Each row: a 200-wide control, then a 120-wide value field, so the columns line up
            GUILayout.BeginHorizontal();
            _accelerateMode = GUILayout.Toggle(_accelerateMode, "開啟加速", GUILayout.Width(200));
            speedInput = GUILayout.TextField(speedInput, GUILayout.Width(120));
            float.TryParse(speedInput, out speed);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            dice = GUILayout.Toggle(dice, "控制骰子", GUILayout.Width(200));
            diceInput = GUILayout.TextField(diceInput, GUILayout.Width(120));
            int.TryParse(diceInput, out diceNumber);
            GUILayout.EndHorizontal();

            // 練功 / 煉丹 / 鍛造的「最大消費」，面板打開時套用
            GUILayout.BeginHorizontal();
            spendLimit = GUILayout.Toggle(spendLimit, "單次消費上限", GUILayout.Width(200));
            spendLimitInput = GUILayout.TextField(spendLimitInput, GUILayout.Width(120));
            int.TryParse(spendLimitInput, out spendLimitNumber);
            GUILayout.EndHorizontal();

            GUILayout.Space(8);
            GUILayout.Label("單挑", myStyle);
            winLose = GUILayout.Toggle(winLose, "顯示直接勝利/失敗按鈕");

            GUILayout.Space(8);
            GUILayout.Label("團體戰", myStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("玩家死亡"))
                ShowGameOver(GameOverType.PlayerDie);
            if (GUILayout.Button("友方獲勝"))
                ShowGameOver(GameOverType.FriendWin);
            if (GUILayout.Button("敵方獲勝"))
                ShowGameOver(GameOverType.EnemyWin);
            if (GUILayout.Button("時間到"))
                ShowGameOver(GameOverType.Timeout);
            GUILayout.EndHorizontal();
        }

        // 角色屬性 / 好感度 / 旗標 share one table: name, note, range, current, new value, set, +1, -1, lock
        private void DrawValueTable()
        {
            // Header stays outside the scroll view so it doesn't scroll away. Each header cell uses the
            // style of the control below it minus the background, so text and spacing match exactly.
            // The header is boxed; rows get the same padding without the frame.
            // All text cells also take the value field's top/bottom margin and padding (but no left/right,
            // so columns stay put). That puts every cell's first line on the same line as the field's text.
            // GUI.skin is only readable inside OnGUI, so the styles are built on the first draw
            if (_textStyle == null)
            {
                RectOffset fieldMargin = GUI.skin.textField.margin, fieldPadding = GUI.skin.textField.padding;
                RectOffset buttonMargin = GUI.skin.button.margin, buttonPadding = GUI.skin.button.padding;
                _textStyle = new GUIStyle(myStyle)
                {
                    margin = new RectOffset(0, 0, fieldMargin.top, fieldMargin.bottom),
                    padding = new RectOffset(0, 0, fieldPadding.top, fieldPadding.bottom),
                };
                _headerInputStyle = new GUIStyle(GUI.skin.textField) { normal = { background = null }, fontSize = myStyle.fontSize };
                _headerButtonStyle = new GUIStyle(GUI.skin.button)
                {
                    normal = { background = null },
                    fontSize = myStyle.fontSize,
                    margin = new RectOffset(buttonMargin.left, buttonMargin.right, fieldMargin.top, fieldMargin.bottom),
                    padding = new RectOffset(buttonPadding.left, buttonPadding.right, fieldPadding.top, fieldPadding.bottom),
                };
                _headerStyle = new GUIStyle(GUI.skin.box) { margin = new RectOffset() };
                _rowStyle = new GUIStyle { padding = GUI.skin.box.padding };
            }

            // The list fills the rest of the window; one row per value, the note shrinks first
            GUILayout.BeginVertical("box", GUILayout.ExpandHeight(true));

            // Search sits in the box with the rows' padding, so "搜尋" lines up with the 名稱 column
            GUILayout.BeginHorizontal(_rowStyle);
            GUILayout.Label("搜尋", _textStyle, GUILayout.ExpandWidth(false));
            GUILayout.Space(8);
            _searchInput = GUILayout.TextField(_searchInput);
            if (_tab == 2)
            {
                // Only the rows the search shows
                if (GUILayout.Button("全滿", GUILayout.Width(SetWidth)))
                    ForShownRelationships(relationship => ApplyValue(relationship, 100));
                if (GUILayout.Button("歸零", GUILayout.Width(SetWidth)))
                    ForShownRelationships(relationship => ApplyValue(relationship, 0));
                if (GUILayout.Button("全部結識", GUILayout.Width(LockWidth + 30)))
                    ForShownRelationships(relationship => relationship.SetActive(true));
            }
            else if (_tab == 4)
            {
                if (GUILayout.Button("全持有", GUILayout.Width(LockWidth + 10)))
                    SetAllBooks(true);
                if (GUILayout.Button("歸零", GUILayout.Width(SetWidth)))
                    SetAllBooks(false);
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal(_headerStyle);
            GUILayout.Label("名稱", _textStyle, GUILayout.Width(NameWidth));
            GUILayout.Label("備註", _textStyle, GUILayout.MinWidth(0), GUILayout.ExpandWidth(true));
            GUILayout.Label("範圍", _textStyle, GUILayout.Width(RangeWidth));
            GUILayout.Label("目前", _textStyle, GUILayout.Width(StateWidth));
            GUILayout.Label("新數值", _headerInputStyle, GUILayout.Width(InputWidth));
            GUILayout.Label("操作", _headerButtonStyle, GUILayout.Width(SetWidth));
            GUILayout.Label("", _headerButtonStyle, GUILayout.Width(StepWidth));
            GUILayout.Label("", _headerButtonStyle, GUILayout.Width(StepWidth));
            GUILayout.Label("", _headerButtonStyle, GUILayout.Width(LockWidth));
            // Room for the always-shown vertical scrollbar of the list
            GUILayout.Space(GUI.skin.verticalScrollbar.fixedWidth + GUI.skin.verticalScrollbar.margin.left);
            GUILayout.EndHorizontal();

            _tabScroll[_tab] = GUILayout.BeginScrollView(_tabScroll[_tab], false, true, GUILayout.ExpandHeight(true));
            PlayerStatManagerData manager = PlayerStatManagerData.Instance;
            switch (_tab)
            {
                case 1:
                    foreach (GameStat stat in manager.Stats.List)
                        DrawValueRow(stat, stat.StatType.ToString(), stat.Name, $"{stat.Min} ~ {stat.Max}");
                    break;
                case 2:
                    foreach (RelationshipStat relationship in manager.Relationships.List)
                        DrawValueRow(relationship, relationship.Type.ToString(), RelationshipNote(relationship), "0 ~ 100");
                    break;
                case 3:
                    foreach (FlagCollectionData collectionData in MissionManagerData.Instance.FlagsCollection)
                    foreach (FlagData flagData in collectionData.List)
                        DrawValueRow(flagData, flagData.name, flagData.DevNote, "");
                    break;
                case 4:
                    foreach (Book book in ItemDatabase.Instance.Books.List)
                        DrawValueRow(book, BookName(book), BookNote(book), "");
                    break;
                case 5:
                    foreach (PlayerTalentData talent in manager.Talents.List)
                        DrawValueRow(talent, TalentName(talent), TalentNote(talent), $"0 ~ {TalentMaxLevel(talent)}");
                    break;
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private void DrawValueRow(ScriptableObject key, string name, string note, string range)
        {
            note = note ?? "";
            if (!MatchesSearch(name, note))
                return;

            GUILayout.BeginHorizontal(_rowStyle);
            GUILayout.Label(name, _textStyle, GUILayout.Width(NameWidth));
            if (key is RelationshipStat relationship)
            {
                if (GUILayout.Button(note, GUILayout.Width(LockWidth + 10)))
                    relationship.SetActive(!relationship.Active);
                GUILayout.FlexibleSpace();
            }
            else
                GUILayout.Label(note, _textStyle, GUILayout.MinWidth(0), GUILayout.ExpandWidth(true));
            GUILayout.Label(range, _textStyle, GUILayout.Width(RangeWidth));
            int value = GetValue(key);
            string state = value.ToString();
            GUILayout.Label(state, _textStyle, GUILayout.Width(StateWidth));

            // Show the live value unless the user is typing a new one
            string shown = _inputs.TryGetValue(key, out string typed) ? typed : state;
            string edited = GUILayout.TextField(shown, GUILayout.Width(InputWidth));
            if (edited != shown)
                _inputs[key] = edited;

            if (GUILayout.Button("設定", GUILayout.Width(SetWidth)) && int.TryParse(edited, out int newValue))
                ApplyValue(key, newValue);
            if (GUILayout.Button("+1", GUILayout.Width(StepWidth)))
                ApplyValue(key, value + 1);
            if (GUILayout.Button("-1", GUILayout.Width(StepWidth)))
                ApplyValue(key, value - 1);

            bool locked = _locks.ContainsKey(key);
            if (GUILayout.Toggle(locked, locked ? "已鎖定" : "鎖定", GUI.skin.button, GUILayout.Width(LockWidth)) != locked)
            {
                if (locked)
                    _locks.Remove(key);
                else
                    _locks[key] = value;
            }
            // Ends the row like the header's spacer does, so both meet the padding the same way
            GUILayout.Space(0);
            GUILayout.EndHorizontal();
        }

        private bool MatchesSearch(string name, string note) =>
            _search.Length == 0
            || name.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0
            || note.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0;

        private static string RelationshipNote(RelationshipStat relationship) => relationship.Active ? "已結識" : "未結識";

        private void ForShownRelationships(Action<RelationshipStat> action)
        {
            foreach (RelationshipStat relationship in PlayerStatManagerData.Instance.Relationships.List)
            {
                if (MatchesSearch(relationship.Type.ToString(), RelationshipNote(relationship)))
                    action(relationship);
            }
        }

        private static string BookName(Book book)
        {
            string name = Locale.GetString(book.GetNameKey());
            return string.IsNullOrEmpty(name) ? book.Id : name;
        }

        private static string BookNote(Book book) => book.IsMartial ? "秘笈" : "";

        private static string TalentName(PlayerTalentData talent)
        {
            string name = Locale.GetString(talent.GetIdKey());
            return string.IsNullOrEmpty(name) ? talent.Id : name;
        }

        private static string TalentNote(PlayerTalentData talent) =>
            talent.CombatSkill && talent.BattleSkill ? "決鬥、戰役"
            : talent.CombatSkill ? "決鬥"
            : talent.BattleSkill ? "戰役"
            : "";

        // A renamed field would read as 0 and clamp every skill to 0, so no field means no cap
        private static int TalentMaxLevel(PlayerTalentData talent)
        {
            Traverse field = Traverse.Create(talent).Field("_maxLevel");
            return field.FieldExists() ? field.GetValue<int>() : int.MaxValue;
        }

        // 全持有 only gives books not held yet; 歸零 skips practiced books, since the save drops a book
        // with no copies and its 修練 progress with it
        private void SetAllBooks(bool own)
        {
            foreach (Book book in ItemDatabase.Instance.Books.List)
            {
                if (!MatchesSearch(BookName(book), BookNote(book)))
                    continue;
                if (own && book.CurrentCount == 0)
                    ApplyValue(book, 1);
                else if (!own && book.CurrentMartialLevel == 0 && book.CurrentLevelPoints == 0)
                    ApplyValue(book, 0);
            }
        }

        // Sets a value from the menu; a locked value is re-locked at the new one
        private void ApplyValue(ScriptableObject key, int value)
        {
            SetValue(key, value);
            _inputs.Remove(key);
            if (_locks.ContainsKey(key))
                _locks[key] = GetValue(key);
        }

        private static int GetValue(ScriptableObject key)
        {
            switch (key)
            {
                case GameStat stat:
                    return stat.Value;
                case RelationshipStat relationship:
                    return relationship.Value;
                case ItemData item:
                    return item.CurrentCount;
                case PlayerTalentData talent:
                    return talent.Level;
                default:
                    return ((FlagData)key).State;
            }
        }

        private static void SetValue(ScriptableObject key, int value)
        {
            switch (key)
            {
                case GameStat stat:
                    stat.SetValue(value); // clamps to Min ~ Max itself
                    break;
                case RelationshipStat relationship:
                    relationship.SetValue(Mathf.Clamp(value, 0, 100));
                    break;
                case ItemData item:
                    item.SetCount(Mathf.Max(value, 0));
                    break;
                case PlayerTalentData talent:
                    talent.Setup(Mathf.Clamp(value, 0, TalentMaxLevel(talent)));
                    break;
                default:
                    ((FlagData)key).State = value;
                    break;
            }
        }

        private static void ShowGameOver(GameOverType type)
        {
            Traverse.Create(GameLevelManager.Instance).Method("ShowGameOver", type, true).GetValue();
        }

        private void SetTimeScale(float targetSpeed)
        {
            if (speed > 0.0f)
                Time.timeScale = targetSpeed;
        }
    }
}
