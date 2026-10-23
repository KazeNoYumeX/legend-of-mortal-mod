using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.Mono;
using HarmonyLib;
using Mortal.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LegendOfMortalMod
{
    [BepInPlugin("LegendOfMortalMod", "活俠傳修改選單", "1.5.0")]
    public class Plugin : BaseUnityPlugin
    {
        private bool _battleButtons;
        private bool _diceButtons;
        private readonly GUIStyle myStyle = new GUIStyle();
        public bool unlimitedSpend;
        public bool unlimitedResources;
        private bool _accelerateMode;
        private float _skipConfirmUntil;
        private Harmony _harmony;

        private Vector2Int lastScreenSize;
        private ConfigEntry<KeyCode> MenuToggleKey;
        private ConfigEntry<float> UiScale;
        private ConfigEntry<bool> BlockMenuClicks;
        private ConfigEntry<bool> StorySaveAnywhere;
        private float _guiScale = 1;

        // Invisible uGUI panel under the menu window, so clicks on the menu don't reach the game UI behind it
        private RectTransform _clickBlocker;
        private static readonly string[] TabNames = { "基本", "角色屬性", "好感度", "旗標", "書籍", "貴重品", "雜物", "技能", "講經堂" };

        // 講經堂 records live in the global save (Save_universe.dat), which the game only writes at certain points
        private bool _saveUniverse;
        internal bool universeLoaded;
        private int _tab;
        private int _tabInput;

        // 選單最小化：只留基本頁，視窗縮到內容大小；還原時回到原本的大小
        private bool _minimized;
        private bool _minimizedInput;
        private float _minimizedHeight = 300;
        private Vector2 _fullSize;

        // 心上人名單是否展開；和分頁一樣等下一次 Layout 才切換
        private bool _loverList;
        private bool _loverListInput;
        private GUIStyle _loverLabelStyle;
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
        private const float NameWidth = 200,
            RangeWidth = 120,
            StateWidth = 60,
            InputWidth = 80,
            SetWidth = 50,
            StepWidth = 35,
            LockWidth = 50;
        private bool _showMenu;
        private float speed = 2;
        private string speedInput = "2";
        private Rect windowRect;
        private static readonly LeanLocalizationResolver Locale = new LeanLocalizationResolver();
        public static Plugin Instance { get; private set; }
        internal static ManualLogSource Log { get; private set; }

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            MenuToggleKey = Config.Bind("LegendOfMortalMod", "MenuToggleKey", KeyCode.F3, "Menu Toggle Key");
            UiScale = Config.Bind(
                "LegendOfMortalMod",
                "UiScale",
                1f,
                "Menu size multiplier, on top of automatic scaling to screen height (1080p = 1x)"
            );
            BlockMenuClicks = Config.Bind(
                "LegendOfMortalMod",
                "BlockMenuClicks",
                true,
                "Keep clicks on the menu from reaching the game behind it"
            );

            StorySaveAnywhere = Config.Bind(
                "LegendOfMortalMod",
                "StorySaveAnywhere",
                false,
                "Allow saving anywhere in a story; loading replays the story from its start"
            );

            _harmony = Harmony.CreateAndPatchAll(typeof(Patch));
            SceneManager.activeSceneChanged += OnSceneChanged;
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

            // Once per frame, so 全部解鎖 writes the file once
            if (_saveUniverse && universeLoaded && SaveSystem.Instance != null)
            {
                _saveUniverse = false;
                SaveSystem.Instance.SaveUniverseData();
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
                    if (!Mathf.Approximately(Time.timeScale, 10) && !Mathf.Approximately(Time.timeScale, 2))
                    {
                        SetTimeScale(1);
                    }
                }
            }

            // Test controls left in the scenes; keep them hidden
            SetActive("[UI]/MainUI/Layer_1/TestDayButton", false);
            SetActive("[UI]/MainUI/Layer_1/TestNightButton", false);
            SetActive("[UI]/MainUI/Layer_5/TestAnimationPanel", false);
            SetActive("[UI]/TopPanel/MenuPanel/TestWin", false);
            SetActive("[UI]/TopPanel/MenuPanel/TestLose", false);

            DiceControl.Update(_diceButtons);
            StorySave.Enabled = StorySaveAnywhere.Value;
            StorySave.Update();
            UpdateClickBlocker();
        }

        private void UpdateClickBlocker()
        {
            bool block = _showMenu && BlockMenuClicks.Value;
            if (_clickBlocker == null)
            {
                if (!block)
                    return;
                GameObject canvas = new GameObject(
                    "LegendOfMortalMod.ClickBlocker",
                    typeof(Canvas),
                    typeof(GraphicRaycaster)
                );
                DontDestroyOnLoad(canvas);
                canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.GetComponent<Canvas>().sortingOrder = short.MaxValue;
                GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
                panel.transform.SetParent(canvas.transform, false);
                panel.GetComponent<Image>().color = Color.clear;
                _clickBlocker = panel.GetComponent<RectTransform>();
                // IMGUI measures from the top left, in menu units scaled by _guiScale
                _clickBlocker.anchorMin = _clickBlocker.anchorMax = _clickBlocker.pivot = new Vector2(0, 1);
            }
            _clickBlocker.gameObject.SetActive(block);
            _clickBlocker.anchoredPosition = new Vector2(windowRect.x, -windowRect.y) * _guiScale;
            _clickBlocker.sizeDelta = windowRect.size * _guiScale;
        }

        private void OnGUI()
        {
            if (!_showMenu && !_battleButtons)
                return;

            Matrix4x4 oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(_guiScale, _guiScale, 1));
            if (_battleButtons)
                BattleResult.DrawButtons(_guiScale);
            if (_showMenu)
            {
                ApplyMinimized();
                windowRect = GUI.Window(123456, windowRect, DoMyWindow, "活俠傳 Mod");
            }
            GUI.matrix = oldMatrix;
        }

        private void ApplyMinimized()
        {
            if (_minimized)
            {
                if (_fullSize == Vector2.zero)
                    _fullSize = windowRect.size;
                // Two 200 + 120 columns, plus the area's 10 padding on each side
                windowRect.size = new Vector2(360, _minimizedHeight);
            }
            else if (_fullSize != Vector2.zero)
            {
                windowRect.size = _fullSize;
                _fullSize = Vector2.zero;
            }
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
            SceneManager.activeSceneChanged -= OnSceneChanged;
            DiceControl.Clear();
        }

        private static void OnSceneChanged(Scene before, Scene after) => StorySave.SceneChanged();

        internal static IEnumerable<object> Items(object obj, string field)
        {
            if (obj != null && Traverse.Create(obj).Field(field).GetValue() is IEnumerable list)
            {
                foreach (object item in list)
                {
                    if (item != null)
                        yield return item;
                }
            }
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
            // A minimized window takes the new full size when restored
            _fullSize = Vector2.zero;
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

            if (!_minimized)
                _tabInput = GUILayout.Toolbar(_tabInput, TabNames);
            // Layout and the events after it must draw the same controls, so tab and filter changes wait for the next Layout
            if (Event.current.type == EventType.Layout)
            {
                _minimized = _minimizedInput;
                _loverList = _loverListInput;
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
            // Grouped: time and action points, spending, overlays, the menu itself, then 心上人 last so its list
            // opens downward without pushing other rows
            // Each row: a 200-wide control, then a 120-wide value field, so the columns line up
            GUILayout.BeginHorizontal();
            bool accelerate = GUILayout.Toggle(_accelerateMode, "遊戲加速", GUILayout.Width(200));
            // Update leaves 2x alone (the game's own battle speed), so switching off at 2x would stay at 2x
            if (_accelerateMode && !accelerate && Time.timeScale != 0)
                Time.timeScale = 1;
            _accelerateMode = accelerate;
            speedInput = GUILayout.TextField(speedInput, GUILayout.Width(120));
            float.TryParse(speedInput, out speed);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            RoundControl.Unlimited = GUILayout.Toggle(RoundControl.Unlimited, "行動點無限", GUILayout.Width(200));
            // 只在大地圖可用；第二次按下 3 秒內才執行
            GUI.enabled = RoundControl.CanSkip;
            bool confirming = Time.unscaledTime < _skipConfirmUntil;
            if (GUILayout.Button(confirming ? "確定嗎？" : "跳過本旬", GUILayout.Width(120)))
            {
                if (confirming)
                {
                    _skipConfirmUntil = 0;
                    RoundControl.Skip();
                }
                else
                    _skipConfirmUntil = Time.unscaledTime + 3f;
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            GUILayout.Space(8);
            // 修練 / 煉丹 / 鍛造面板的「最大消費」次數不限
            bool spend = GUILayout.Toggle(unlimitedSpend, "無限消費");
            if (spend != unlimitedSpend)
            {
                unlimitedSpend = spend;
                // 面板開著時勾選也馬上套用；取消則等下次打開面板
                if (spend)
                    Patch.RefreshSpendPanels();
            }
            bool resources = GUILayout.Toggle(unlimitedResources, "無限鍛造/煉丹/修練點數/銀兩");
            if (resources != unlimitedResources)
            {
                unlimitedResources = resources;
                // 面板開著時也馬上更新點數和按鈕
                if (resources)
                {
                    Patch.FillResources();
                    Patch.RefreshSpendPanels();
                }
            }

            // Set-and-forget options stay out of the minimized menu
            if (!_minimized)
            {
                GUILayout.Space(8);
                // 單挑隨時顯示；團體戰只在暫停選單顯示
                _battleButtons = GUILayout.Toggle(_battleButtons, "顯示戰鬥判定按鈕");
                _diceButtons = GUILayout.Toggle(_diceButtons, "骰子畫面顯示指定按鈕");
                StorySaveAnywhere.Value = GUILayout.Toggle(StorySaveAnywhere.Value, "劇情中隨時存檔");
            }

            GUILayout.Space(8);
            if (!_minimized)
                BlockMenuClicks.Value = GUILayout.Toggle(BlockMenuClicks.Value, "選單不透明");
            _minimizedInput = GUILayout.Toggle(_minimizedInput, "選單最小化");

            GUILayout.Space(8);
            DrawLoverRow();
            if (_minimized && Event.current.type == EventType.Repaint)
                _minimizedHeight = GUILayoutUtility.GetLastRect().yMax + 30;
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
                RectOffset fieldMargin = GUI.skin.textField.margin,
                    fieldPadding = GUI.skin.textField.padding;
                RectOffset buttonMargin = GUI.skin.button.margin,
                    buttonPadding = GUI.skin.button.padding;
                _textStyle = new GUIStyle(myStyle)
                {
                    margin = new RectOffset(0, 0, fieldMargin.top, fieldMargin.bottom),
                    padding = new RectOffset(0, 0, fieldPadding.top, fieldPadding.bottom),
                };
                _headerInputStyle = new GUIStyle(GUI.skin.textField)
                {
                    normal = { background = null },
                    fontSize = myStyle.fontSize,
                };
                _headerButtonStyle = new GUIStyle(GUI.skin.button)
                {
                    normal = { background = null },
                    fontSize = myStyle.fontSize,
                    margin = new RectOffset(buttonMargin.left, buttonMargin.right, fieldMargin.top, fieldMargin.bottom),
                    padding = new RectOffset(
                        buttonPadding.left,
                        buttonPadding.right,
                        fieldPadding.top,
                        fieldPadding.bottom
                    ),
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
            else if (_tab == 5 || _tab == 6)
            {
                if (GUILayout.Button("全持有", GUILayout.Width(LockWidth + 10)))
                {
                    ItemDataCollection items = _tab == 5 ? ItemDatabase.Instance.Special : ItemDatabase.Instance.Miscs;
                    foreach (ItemData item in items.List)
                    {
                        if (item.CurrentCount == 0 && MatchesSearch(ItemName(item), ""))
                            ApplyValue(item, 1);
                    }
                }
            }
            else if (_tab == 8)
            {
                if (GUILayout.Button("全部解鎖", GUILayout.Width(LockWidth + 30)))
                    ForLibraryEntries(
                        (item, note) =>
                        {
                            if (item.Count == 0 && MatchesSearch(LibraryTitle(item), note))
                                ApplyValue(item, 1);
                        }
                    );
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
                        DrawValueRow(stat, stat.StatType.ToString(), StatNote(stat), $"{stat.Min} ~ {stat.Max}");
                    break;
                case 2:
                    foreach (RelationshipStat relationship in manager.Relationships.List)
                        DrawValueRow(
                            relationship,
                            relationship.Type.ToString(),
                            RelationshipNote(relationship),
                            "0 ~ 100"
                        );
                    break;
                case 3:
                    foreach (FlagCollectionData collectionData in MissionManagerData.Instance.FlagsCollection)
                    foreach (FlagData flagData in collectionData.List)
                        DrawValueRow(flagData, flagData.name, flagData.DevNote, "");
                    break;
                case 4:
                    foreach (Book book in ItemDatabase.Instance.Books.List)
                        DrawValueRow(book, ItemName(book), BookNote(book), "");
                    break;
                case 5:
                    foreach (ItemData item in ItemDatabase.Instance.Special.List)
                        DrawValueRow(item, ItemName(item), "", "");
                    break;
                case 6:
                    foreach (ItemData item in ItemDatabase.Instance.Miscs.List)
                        DrawValueRow(item, ItemName(item), "", "");
                    break;
                case 7:
                    foreach (PlayerTalentData talent in manager.Talents.List)
                        DrawValueRow(talent, TalentName(talent), TalentNote(talent), $"0 ~ {TalentMaxLevel(talent)}");
                    break;
                case 8:
                    ForLibraryEntries((item, note) => DrawValueRow(item, LibraryTitle(item), note, ""));
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

            // 講經堂 entries are unlock only: one button while the count is still 0. The unused columns are
            // blank cells styled like the header's, so the columns line up with the other rows
            if (key is LibraryItemData)
            {
                GUILayout.Label("", _headerInputStyle, GUILayout.Width(InputWidth));
                if (value == 0)
                {
                    if (GUILayout.Button("解鎖", GUILayout.Width(SetWidth)))
                        ApplyValue(key, value + 1);
                }
                else
                    GUILayout.Label("", _headerButtonStyle, GUILayout.Width(SetWidth));
                GUILayout.Label("", _headerButtonStyle, GUILayout.Width(StepWidth));
                GUILayout.Label("", _headerButtonStyle, GUILayout.Width(StepWidth));
                GUILayout.Label("", _headerButtonStyle, GUILayout.Width(LockWidth));
                GUILayout.Space(0);
                GUILayout.EndHorizontal();
                return;
            }

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
            if (
                GUILayout.Toggle(locked, locked ? "已鎖定" : "鎖定", GUI.skin.button, GUILayout.Width(LockWidth))
                != locked
            )
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

        private static string _loverValues;
        private static string _loverLang;
        private static readonly List<int> LoverIds = new List<int>();
        private static readonly List<string> LoverNames = new List<string>();

        // 愛人 holds a lover id; the game names each one under PlayerInfo/Lover/<id>
        private static void LoadLovers(GameStat stat)
        {
            // Looked up again after a language switch, and until the locale is loaded
            string lang = Locale.GetCurrentLang();
            if (LoverIds.Count > 0 && lang == _loverLang)
                return;
            _loverLang = lang;
            LoverIds.Clear();
            LoverNames.Clear();
            StringBuilder values = new StringBuilder();
            for (int id = stat.Min; id <= stat.Max; id++)
            {
                string key = "PlayerInfo/Lover/" + id;
                string lover = Locale.GetString(key);
                if (string.IsNullOrEmpty(lover) || lover == key)
                    continue;
                LoverIds.Add(id);
                LoverNames.Add(lover);
                values.Append($"\n={id}，{lover}");
            }
            _loverValues = values.ToString().TrimStart('\n');
        }

        // Listed like the flags' notes
        private static string StatNote(GameStat stat)
        {
            if (stat.StatType != GameStatType.愛人)
                return stat.Name;
            LoadLovers(stat);
            return _loverValues;
        }

        // 心上人：下拉選單，按鈕顯示目前的愛人，展開的名單直接排在按鈕下面
        private void DrawLoverRow()
        {
            if (PlayerStatManagerData.Instance == null)
                return;
            GameStat stat = PlayerStatManagerData.Instance.Stats.Get(GameStatType.愛人);
            LoadLovers(stat);
            int current = LoverIds.IndexOf(stat.Value);
            // The skin's label font is much larger; a toggle without its box keeps the other rows' font and indent
            if (_loverLabelStyle == null)
                _loverLabelStyle = new GUIStyle(GUI.skin.toggle)
                {
                    normal = { background = null },
                    hover = { background = null },
                    active = { background = null },
                };
            GUILayout.BeginHorizontal();
            GUILayout.Label("切換心上人", _loverLabelStyle, GUILayout.Width(200));
            GUILayout.BeginVertical(GUILayout.Width(120));
            string name = current >= 0 ? LoverNames[current] : stat.Value.ToString();
            if (GUILayout.Button(name + (_loverList ? " ▲" : " ▼")))
                _loverListInput = !_loverListInput;
            if (_loverList)
            {
                int picked = GUILayout.SelectionGrid(current, LoverNames.ToArray(), 1);
                if (picked != current)
                {
                    stat.SetValue(LoverIds[picked]);
                    _loverListInput = false;
                }
            }
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        private static string RelationshipNote(RelationshipStat relationship) =>
            relationship.Active ? "已結識" : "未結識";

        private void ForShownRelationships(Action<RelationshipStat> action)
        {
            foreach (RelationshipStat relationship in PlayerStatManagerData.Instance.Relationships.List)
            {
                if (MatchesSearch(relationship.Type.ToString(), RelationshipNote(relationship)))
                    action(relationship);
            }
        }

        private static string ItemName(ItemData item)
        {
            string name = Locale.GetString(item.GetNameKey());
            return string.IsNullOrEmpty(name) ? item.Id : name;
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

        // 生死簿 and 汗青書 in 講經堂; the list name is the note, so searching it shows one list
        private static void ForLibraryEntries(Action<LibraryItemData, string> action)
        {
            LibrarySystem library = LibrarySystem.Instance;
            if (library == null || !Instance.UniverseLoaded(library))
                return;
            foreach (LibraryItemData item in library.Dead.List)
            {
                if (item != null)
                    action(item, "生死簿");
            }
            foreach (LibraryItemData item in library.EndGame.List)
            {
                if (item != null)
                    action(item, "汗青書");
            }
        }

        // The game loads the global save at startup, often before our patches see it; all counts start at 0,
        // so any recorded entry also means it is loaded
        private bool UniverseLoaded(LibrarySystem library)
        {
            foreach (
                LibraryItemCollection collection in new[]
                {
                    library.Dead,
                    library.EndGame,
                    library.Achievement,
                    library.EndCg,
                }
            )
            {
                if (universeLoaded)
                    break;
                universeLoaded = collection != null && collection.List.Exists(item => item != null && item.Count > 0);
            }
            return universeLoaded;
        }

        private static string LibraryTitle(LibraryItemData item)
        {
            string name = Locale.GetString(item.GetTitleKey());
            return string.IsNullOrEmpty(name) ? item.name : name;
        }

        // 全持有 only gives books not held yet; 歸零 skips practiced books, since the save drops a book
        // with no copies and its 修練 progress with it
        private void SetAllBooks(bool own)
        {
            foreach (Book book in ItemDatabase.Instance.Books.List)
            {
                if (!MatchesSearch(ItemName(book), BookNote(book)))
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
                case LibraryItemData entry:
                    return entry.Count;
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
                case LibraryItemData entry:
                    entry.Set(value);
                    Instance._saveUniverse = true;
                    break;
                default:
                    ((FlagData)key).State = value;
                    break;
            }
        }

        private void SetTimeScale(float targetSpeed)
        {
            if (speed > 0.0f)
                Time.timeScale = targetSpeed;
        }
    }
}
