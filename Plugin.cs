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
    [BepInPlugin("LegendOfMortalMod", "活俠傳修改選單", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        public bool dice;
        public int diceNumber = 50;
        private readonly GUIStyle myStyle = new GUIStyle();
        private string currentStatValue = "";
        private string diceInput = "50";
        private Vector2 flagScrollPosition = Vector2.zero;
        private GameStatType[] gameStatTypes; // Array to hold all enum values
        private bool _accelerateMode;
        private Harmony _harmony;
        private string[] items; // Array to hold string representations of enum values

        private Vector2Int lastScreenSize;
        private ConfigEntry<KeyCode> MenuToggleKey;
        private ConfigEntry<float> UiScale;
        private float _guiScale = 1;
        private Vector2 scrollPosition = Vector2.zero;
        private int selectedItemIndex;
        private bool _showDropdown;
        private Rect _dropdownRect;
        private readonly Dictionary<FlagData, string> _flagInputs = new Dictionary<FlagData, string>();
        private string _flagSearch = "";
        private string _flagSearchInput = "";
        private GUIStyle _headerCellStyle;

        // Flag list column widths, shared by the header and the rows
        private const float FlagNameWidth = 200, FlagStateWidth = 60, FlagInputWidth = 80, FlagSetWidth = 50, FlagStepWidth = 35;
        private bool _showMenu;
        private float speed = 1;
        private string speedInput = "1";
        private Rect windowRect;
        private bool winLose;
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

        private void Start()
        {
            gameStatTypes = (GameStatType[])Enum.GetValues(typeof(GameStatType));
            items = new string[gameStatTypes.Length];
            for (int i = 0; i < gameStatTypes.Length; i++)
            {
                items[i] = gameStatTypes[i].ToString();
            }

            // Initialize currentStatValue with the default value
            UpdateCurrentStatValue();
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
                // Unsaved flag edits would look like real values on reopen; drop them
                _flagInputs.Clear();            }

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
            if (_showDropdown)
            {
                GUI.Window(123457, _dropdownRect, DrawDropdown, GUIContent.none, GUI.skin.box);
                GUI.BringWindowToFront(123457);
            }
            GUI.matrix = oldMatrix;
        }

        private void DrawDropdown(int windowID)
        {
            scrollPosition = GUILayout.BeginScrollView(scrollPosition);
            for (int i = 0; i < items.Length; i++)
            {
                if (GUILayout.Button(items[i]))
                {
                    selectedItemIndex = i;
                    _showDropdown = false;
                    UpdateCurrentStatValue();
                }
            }
            GUILayout.EndScrollView();
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
            {
                GUILayout.BeginVertical();
                {
                    GUILayout.Label("基本設定", myStyle);
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
                }
                GUILayout.EndVertical();
                GUILayout.BeginHorizontal();
                {
                    // ComboBox button; the list itself is drawn by DrawDropdown as a popup window
                    if (GUILayout.Button(items[selectedItemIndex], GUILayout.Width(200)))
                    {
                        _showDropdown = !_showDropdown;
                    }

                    // Place the popup under the button. Offsets match the BeginArea above.
                    if (Event.current.type == EventType.Repaint)
                    {
                        Rect button = GUILayoutUtility.GetLastRect();
                        _dropdownRect = new Rect(
                            windowRect.x + 10 + button.x,
                            windowRect.y + 20 + button.yMax,
                            button.width,
                            Mathf.Min(items.Length * 25 + 10, 300)
                        );
                    }

                    // Display selected item text field
                    currentStatValue = GUILayout.TextField(currentStatValue, GUILayout.Width(120));

                    // Test button (example)
                    if (GUILayout.Button("設定數值") && int.TryParse(currentStatValue, out int statValue))
                    {
                        PlayerStatManagerData.Instance.Stats.Set(gameStatTypes[selectedItemIndex], statValue);
                    }
                }
                GUILayout.EndHorizontal();

                GUILayout.Label("測試", myStyle);
                if (GUILayout.Button("開啟/關閉 修改資源"))
                {
                    if (!GameObject.Find("[UI]/TopPanel/StatusPanel/TestPanel").activeSelf)
                    {
                        SetActive("[UI]/TopPanel/StatusPanel/TestPanel", true);
                        SetActive("[UI]/TopPanel/StatusPanel/TestPanel/Flags", true);
                    }
                    else
                        SetActive("[UI]/TopPanel/StatusPanel/TestPanel", false);
                }
                winLose = GUILayout.Toggle(winLose, "單挑直接勝利/失敗");
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

                FlagCollectionData[] flagsCollection = MissionManagerData.Instance.FlagsCollection;

                GUILayout.Label("旗標", myStyle);
                GUILayout.BeginHorizontal();
                GUILayout.Label("搜尋", myStyle, GUILayout.ExpandWidth(false));
                _flagSearchInput = GUILayout.TextField(_flagSearchInput);
                // Layout and the events after it must see the same rows, so filter changes wait for the next Layout
                if (Event.current.type == EventType.Layout)
                    _flagSearch = _flagSearchInput;
                GUILayout.EndHorizontal();

                // The list fills the rest of the window; one row per flag, the note shrinks first
                GUILayout.BeginVertical("box", GUILayout.ExpandHeight(true));

                // Header stays outside the scroll view so it doesn't scroll away. Its widths and margins
                // copy the row cells below, plus room for the always-shown vertical scrollbar.
                _headerCellStyle ??= new GUIStyle(myStyle) { margin = GUI.skin.button.margin };
                GUILayout.BeginHorizontal();
                GUILayout.Label("名稱", myStyle, GUILayout.Width(FlagNameWidth));
                GUILayout.Label("備註", myStyle, GUILayout.MinWidth(0), GUILayout.ExpandWidth(true));
                GUILayout.Label("目前", myStyle, GUILayout.Width(FlagStateWidth));
                GUILayout.Label("新數值", _headerCellStyle, GUILayout.Width(FlagInputWidth));
                GUILayout.Label("操作", _headerCellStyle, GUILayout.Width(FlagSetWidth + 2 * FlagStepWidth + 2 * GUI.skin.button.margin.left));
                GUILayout.Space(GUI.skin.verticalScrollbar.fixedWidth + GUI.skin.verticalScrollbar.margin.left);
                GUILayout.EndHorizontal();

                flagScrollPosition = GUILayout.BeginScrollView(flagScrollPosition, false, true, GUILayout.ExpandHeight(true));
                foreach (FlagCollectionData collectionData in flagsCollection)
                {
                    foreach (FlagData flagData in collectionData.List)
                    {
                        if (!MatchesFlagSearch(flagData))
                            continue;

                        GUILayout.BeginHorizontal();
                        GUILayout.Label(flagData.name, myStyle, GUILayout.Width(FlagNameWidth));
                        GUILayout.Label(flagData.DevNote ?? "", myStyle, GUILayout.MinWidth(0), GUILayout.ExpandWidth(true));
                        string state = flagData.State.ToString();
                        GUILayout.Label(state, myStyle, GUILayout.Width(FlagStateWidth));

                        // Show the live state unless the user is typing a new one
                        string shown = _flagInputs.TryGetValue(flagData, out string typed) ? typed : state;
                        string edited = GUILayout.TextField(shown, GUILayout.Width(FlagInputWidth));
                        if (edited != shown)
                            _flagInputs[flagData] = edited;

                        if (GUILayout.Button("設定", GUILayout.Width(FlagSetWidth)) && int.TryParse(edited, out int newState))
                        {
                            flagData.State = newState;
                            _flagInputs.Remove(flagData);
                        }

                        if (GUILayout.Button("+1", GUILayout.Width(FlagStepWidth)))
                        {
                            flagData.State += 1;
                            _flagInputs.Remove(flagData);
                        }

                        if (GUILayout.Button("-1", GUILayout.Width(FlagStepWidth)))
                        {
                            flagData.State -= 1;
                            _flagInputs.Remove(flagData);
                        }
                        GUILayout.EndHorizontal();
                    }
                }
                GUILayout.EndScrollView();
                GUILayout.EndVertical();
            }
            GUILayout.EndArea();
            GUI.DragWindow();
        }

        private bool MatchesFlagSearch(FlagData flagData)
        {
            return _flagSearch.Length == 0
                || flagData.name.IndexOf(_flagSearch, StringComparison.OrdinalIgnoreCase) >= 0
                || (flagData.DevNote ?? "").IndexOf(_flagSearch, StringComparison.OrdinalIgnoreCase) >= 0;
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

        private void UpdateCurrentStatValue()
        {
            currentStatValue = PlayerStatManagerData.Instance.Stats.Get(gameStatTypes[selectedItemIndex]).Value.ToString();
        }
    }
}
