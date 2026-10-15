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
                // Flag states change while the menu is closed; re-read them on open
                _flagInputs.Clear();
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
            myStyle.normal.textColor = Color.white;
            GUILayout.BeginArea(new Rect(10, 20, windowRect.width - 20, windowRect.height - 30));
            {
                GUILayout.BeginVertical();
                {
                    _accelerateMode = GUILayout.Toggle(_accelerateMode, "開啟加速");
                    
                    GUILayout.Label("   加速速度", myStyle);
                    speedInput = GUILayout.TextField(speedInput);
                    float.TryParse(speedInput, out speed);
                    dice = GUILayout.Toggle(dice, "控制骰子");
                    GUILayout.Label("    骰子數字", myStyle);
                    diceInput = GUILayout.TextField(diceInput);
                    int.TryParse(diceInput, out diceNumber);
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
                    currentStatValue = GUILayout.TextField(currentStatValue);

                    // Test button (example)
                    if (GUILayout.Button("設定數值"))
                    {
                        PlayerStatManagerData.Instance.Stats.Set(
                            gameStatTypes[selectedItemIndex],
                            int.Parse(currentStatValue)
                        );
                    }
                }
                GUILayout.EndHorizontal();

                FlagCollectionData[] flagsCollection = MissionManagerData.Instance.FlagsCollection;

                GUILayout.BeginHorizontal();
                GUILayout.Label("搜尋旗標", myStyle, GUILayout.ExpandWidth(false));
                _flagSearch = GUILayout.TextField(_flagSearch);
                GUILayout.EndHorizontal();

                float flagDropDownHeight = Mathf.Min(400);
                GUILayout.BeginHorizontal("box");
                flagScrollPosition = GUILayout.BeginScrollView(
                    flagScrollPosition,
                    GUILayout.Width(400),
                    GUILayout.Height(flagDropDownHeight)
                );
                foreach (FlagCollectionData collectionData in flagsCollection)
                {
                    foreach (FlagData flagData in collectionData.List)
                    {
                        if (!MatchesFlagSearch(flagData))
                            continue;

                        GUILayout.Label(
                            "Flag: " + flagData.name + " State: " + flagData.State,
                            myStyle
                        );
                        GUILayout.Label("Note: " + flagData.DevNote, myStyle);
                        GUILayout.BeginHorizontal();
                        if (GUILayout.Button("+1"))
                        {
                            flagData.State += 1;
                            _flagInputs.Remove(flagData);
                        }

                        if (GUILayout.Button("-1"))
                        {
                            flagData.State -= 1;
                            _flagInputs.Remove(flagData);
                        }

                        if (!_flagInputs.TryGetValue(flagData, out string flagInput))
                            flagInput = flagData.State.ToString();
                        _flagInputs[flagData] = GUILayout.TextField(flagInput, GUILayout.Width(80));
                        if (GUILayout.Button("設定") && int.TryParse(_flagInputs[flagData], out int newState))
                        {
                            flagData.State = newState;
                        }
                        GUILayout.EndHorizontal();
                    }
                }
                GUILayout.EndScrollView();
                GUILayout.EndHorizontal();
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
            switch (gameStatTypes[selectedItemIndex])
            {
                case GameStatType.體力:
                    currentStatValue = PlayerStatManagerData
                        .Instance.Stats.Get(GameStatType.體力)
                        .Value.ToString();
                    break;
                case GameStatType.內力:
                    currentStatValue = PlayerStatManagerData
                        .Instance.Stats.Get(GameStatType.內力)
                        .Value.ToString();
                    break;
                case GameStatType.輕功:
                    currentStatValue = PlayerStatManagerData
                        .Instance.Stats.Get(GameStatType.輕功)
                        .Value.ToString();
                    break;
                case GameStatType.銀兩:
                    currentStatValue = PlayerStatManagerData
                        .Instance.Stats.Get(GameStatType.銀兩)
                        .Value.ToString();
                    break;
                case GameStatType.魅力:
                    currentStatValue = PlayerStatManagerData
                        .Instance.Stats.Get(GameStatType.魅力)
                        .Value.ToString();
                    break;
                case GameStatType.學問:
                    currentStatValue = PlayerStatManagerData
                        .Instance.Stats.Get(GameStatType.學問)
                        .Value.ToString();
                    break;
                case GameStatType.心理衛生:
                    currentStatValue = PlayerStatManagerData
                        .Instance.Stats.Get(GameStatType.心理衛生)
                        .Value.ToString();
                    break;
                case GameStatType.命運:
                    currentStatValue = PlayerStatManagerData
                        .Instance.Stats.Get(GameStatType.命運)
                        .Value.ToString();
                    break;
                case GameStatType.性情:
                    currentStatValue = PlayerStatManagerData
                        .Instance.Stats.Get(GameStatType.性情)
                        .Value.ToString();
                    break;
                case GameStatType.處世:
                    currentStatValue = PlayerStatManagerData
                        .Instance.Stats.Get(GameStatType.處世)
                        .Value.ToString();
                    break;
                case GameStatType.修養:
                    currentStatValue = PlayerStatManagerData
                        .Instance.Stats.Get(GameStatType.修養)
                        .Value.ToString();
                    break;
                case GameStatType.道德:
                    currentStatValue = PlayerStatManagerData
                        .Instance.Stats.Get(GameStatType.道德)
                        .Value.ToString();
                    break;
                case GameStatType.嘴力:
                    currentStatValue = PlayerStatManagerData
                        .Instance.Stats.Get(GameStatType.嘴力)
                        .Value.ToString();
                    break;
                case GameStatType.門派規模:
                    currentStatValue = PlayerStatManagerData
                        .Instance.Stats.Get(GameStatType.門派規模)
                        .Value.ToString();
                    break;
                case GameStatType.門派名聲:
                    currentStatValue = PlayerStatManagerData
                        .Instance.Stats.Get(GameStatType.門派名聲)
                        .Value.ToString();
                    break;
                case GameStatType.門派人數:
                    currentStatValue = PlayerStatManagerData
                        .Instance.Stats.Get(GameStatType.門派人數)
                        .Value.ToString();
                    break;
                case GameStatType.向心力:
                    currentStatValue = PlayerStatManagerData
                        .Instance.Stats.Get(GameStatType.向心力)
                        .Value.ToString();
                    break;
                case GameStatType.鍛造:
                    currentStatValue = PlayerStatManagerData
                        .Instance.Stats.Get(GameStatType.鍛造)
                        .Value.ToString();
                    break;
                case GameStatType.毒藥:
                    currentStatValue = PlayerStatManagerData
                        .Instance.Stats.Get(GameStatType.毒藥)
                        .Value.ToString();
                    break;
                case GameStatType.行動次數:
                    currentStatValue = PlayerStatManagerData
                        .Instance.Stats.Get(GameStatType.行動次數)
                        .Value.ToString();
                    break;
                case GameStatType.個人貢獻度:
                    currentStatValue = PlayerStatManagerData
                        .Instance.Stats.Get(GameStatType.個人貢獻度)
                        .Value.ToString();
                    break;
                case GameStatType.廚藝:
                    currentStatValue = PlayerStatManagerData
                        .Instance.Stats.Get(GameStatType.廚藝)
                        .Value.ToString();
                    break;
                case GameStatType.抗毒:
                    currentStatValue = PlayerStatManagerData
                        .Instance.Stats.Get(GameStatType.抗毒)
                        .Value.ToString();
                    break;
                case GameStatType.抗麻:
                    currentStatValue = PlayerStatManagerData
                        .Instance.Stats.Get(GameStatType.抗麻)
                        .Value.ToString();
                    break;
                case GameStatType.稱號:
                    currentStatValue = PlayerStatManagerData
                        .Instance.Stats.Get(GameStatType.稱號)
                        .Value.ToString();
                    break;
                case GameStatType.愛人:
                    currentStatValue = PlayerStatManagerData
                        .Instance.Stats.Get(GameStatType.愛人)
                        .Value.ToString();
                    break;
                case GameStatType.額外行動次數_1:
                    currentStatValue = PlayerStatManagerData
                        .Instance.Stats.Get(GameStatType.額外行動次數_1)
                        .Value.ToString();
                    break;
                case GameStatType.娘化:
                    currentStatValue = PlayerStatManagerData
                        .Instance.Stats.Get(GameStatType.娘化)
                        .Value.ToString();
                    break;
                case GameStatType.陰陽內功:
                    currentStatValue = PlayerStatManagerData
                        .Instance.Stats.Get(GameStatType.陰陽內功)
                        .Value.ToString();
                    break;
                case GameStatType.防禦:
                    currentStatValue = PlayerStatManagerData
                        .Instance.Stats.Get(GameStatType.防禦)
                        .Value.ToString();
                    break;
                case GameStatType.變心:
                    currentStatValue = PlayerStatManagerData
                        .Instance.Stats.Get(GameStatType.變心)
                        .Value.ToString();
                    break;
                case GameStatType.武學點數:
                    currentStatValue = PlayerStatManagerData
                        .Instance.Stats.Get(GameStatType.武學點數)
                        .Value.ToString();
                    break;
                case GameStatType.全武學點數:
                    currentStatValue = PlayerStatManagerData
                        .Instance.Stats.Get(GameStatType.全武學點數)
                        .Value.ToString();
                    break;
                case GameStatType.門派資產:
                    currentStatValue = PlayerStatManagerData
                        .Instance.Stats.Get(GameStatType.門派資產)
                        .Value.ToString();
                    break;
                case GameStatType.門派貢獻:
                    currentStatValue = PlayerStatManagerData
                        .Instance.Stats.Get(GameStatType.門派貢獻)
                        .Value.ToString();
                    break;
                case GameStatType.全鍛造點數:
                    currentStatValue = PlayerStatManagerData
                        .Instance.Stats.Get(GameStatType.全鍛造點數)
                        .Value.ToString();
                    break;
                case GameStatType.全毒藥點數:
                    currentStatValue = PlayerStatManagerData
                        .Instance.Stats.Get(GameStatType.全毒藥點數)
                        .Value.ToString();
                    break;
                case GameStatType.全貢獻度:
                    currentStatValue = PlayerStatManagerData
                        .Instance.Stats.Get(GameStatType.全貢獻度)
                        .Value.ToString();
                    break;
                case GameStatType.額外行動次數_2:
                    currentStatValue = PlayerStatManagerData
                        .Instance.Stats.Get(GameStatType.額外行動次數_2)
                        .Value.ToString();
                    break;
                case GameStatType.額外行動次數_3:
                    currentStatValue = PlayerStatManagerData
                        .Instance.Stats.Get(GameStatType.額外行動次數_3)
                        .Value.ToString();
                    break;
                case GameStatType.武功刀劍:
                    currentStatValue = PlayerStatManagerData
                        .Instance.Stats.Get(GameStatType.武功刀劍)
                        .Value.ToString();
                    break;
                case GameStatType.武功暗器:
                    currentStatValue = PlayerStatManagerData
                        .Instance.Stats.Get(GameStatType.武功暗器)
                        .Value.ToString();
                    break;
                case GameStatType.武功拳掌:
                    currentStatValue = PlayerStatManagerData
                        .Instance.Stats.Get(GameStatType.武功拳掌)
                        .Value.ToString();
                    break;
                case GameStatType.武功腿法:
                    currentStatValue = PlayerStatManagerData
                        .Instance.Stats.Get(GameStatType.武功腿法)
                        .Value.ToString();
                    break;
                case GameStatType.武功奇門:
                    currentStatValue = PlayerStatManagerData
                        .Instance.Stats.Get(GameStatType.武功奇門)
                        .Value.ToString();
                    break;
                case GameStatType.武功軟兵器:
                    currentStatValue = PlayerStatManagerData
                        .Instance.Stats.Get(GameStatType.武功軟兵器)
                        .Value.ToString();
                    break;
                case GameStatType.武功槍棍:
                    currentStatValue = PlayerStatManagerData
                        .Instance.Stats.Get(GameStatType.武功槍棍)
                        .Value.ToString();
                    break;
                case GameStatType.武功內功:
                    currentStatValue = PlayerStatManagerData
                        .Instance.Stats.Get(GameStatType.武功內功)
                        .Value.ToString();
                    break;
                case GameStatType.儒學:
                    currentStatValue = PlayerStatManagerData
                        .Instance.Stats.Get(GameStatType.儒學)
                        .Value.ToString();
                    break;
                case GameStatType.道學:
                    currentStatValue = PlayerStatManagerData
                        .Instance.Stats.Get(GameStatType.道學)
                        .Value.ToString();
                    break;
                case GameStatType.佛學:
                    currentStatValue = PlayerStatManagerData
                        .Instance.Stats.Get(GameStatType.佛學)
                        .Value.ToString();
                    break;
                case GameStatType.戰役預設血量:
                    currentStatValue = PlayerStatManagerData
                        .Instance.Stats.Get(GameStatType.戰役預設血量)
                        .Value.ToString();
                    break;
                case GameStatType.攻擊爆擊:
                    currentStatValue = PlayerStatManagerData
                        .Instance.Stats.Get(GameStatType.攻擊爆擊)
                        .Value.ToString();
                    break;
                case GameStatType.暗器爆擊:
                    currentStatValue = PlayerStatManagerData
                        .Instance.Stats.Get(GameStatType.暗器爆擊)
                        .Value.ToString();
                    break;
                default:
                    currentStatValue = ""; // Default value if no match found
                    break;
            }
        }
    }
}
