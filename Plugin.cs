﻿using System;
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
        private bool day;
        private string diceInput = "50";
        private Vector2 flagScrollPosition = Vector2.zero;
        private GameStatType[] gameStatTypes; // Array to hold all enum values
        private bool _accelerateMode;
        private string[] items; // Array to hold string representations of enum values

        private Vector2Int lastScreenSize;
        private ConfigEntry<KeyCode> MenuToggleKey;
        private Vector2 scrollPosition = Vector2.zero;
        private int selectedItemIndex;
        private bool _showDropdown;
        private bool _showMenu;
        private float speed = 1;
        private string speedInput = "1";
        private bool testAnimation;
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
            
            Harmony.CreateAndPatchAll(typeof(Patch));
            
            Debug.Log($"Screen.width {Screen.width} Screen.height:{Screen.height}");
            
            // Initialize window size based on screen dimensions
            float width = Screen.width * 0.5f;
            float height = Screen.height * 0.8f;
            windowRect = new Rect(
                (Screen.width - width) / 2,
                (Screen.height - height) / 2,
                width,
                height
            );
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

                // Call a method or raise an event indicating screen size change
                OnScreenSizeChanged(lastScreenSize.x, lastScreenSize.y);
            }

            if (Input.GetKeyDown(MenuToggleKey.Value))
            {
                _showMenu = !_showMenu;
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

            SetActive("[UI]/MainUI/Layer_1/TestDayButton", day);
            SetActive("[UI]/MainUI/Layer_1/TestNightButton", day);
            SetActive("[UI]/MainUI/Layer_5/TestAnimationPanel", testAnimation);
            SetActive("[UI]/TopPanel/MenuPanel/TestWin",  winLose);
            SetActive("[UI]/TopPanel/MenuPanel/TestLose", winLose);
        }

        private void OnGUI()
        {
            if (_showMenu)
            {
                windowRect = GUI.Window(123456, windowRect, DoMyWindow, "活俠傳 Mod");
            }
        }

        private void onDestroy()
        {
            Harmony.UnpatchAll();
        }

        private void OnScreenSizeChanged(float width, float height)
        {
            width *= 0.5f;
            height *= 0.8f;
            windowRect = new Rect(
                (Screen.width - width) / 2,
                (Screen.height - height) / 2,
                width,
                height
            );
            // Implement your logic here when screen size changes
            Debug.Log($"Screen size changed to: {width}x{height}");

            // Example: Adjust UI elements or perform other actions based on new screen size
            // Your code here...
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
                    day = GUILayout.Toggle(day, "測試白天晚上");
                    testAnimation = GUILayout.Toggle(testAnimation, "測試動畫");
                    winLose = GUILayout.Toggle(winLose, "單挑直接勝利/失敗");
                    if (GUILayout.Button("團體戰玩家死亡"))
                        Traverse
                            .Create(GameLevelManager.Instance)
                            .Method("ShowGameOver", GameOverType.PlayerDie, true)
                            .GetValue();
                    if (GUILayout.Button("團體戰友方獲勝"))
                        Traverse
                            .Create(GameLevelManager.Instance)
                            .Method("ShowGameOver", GameOverType.FriendWin, true)
                            .GetValue();
                    if (GUILayout.Button("團體戰敵方獲勝"))
                        Traverse
                            .Create(GameLevelManager.Instance)
                            .Method("ShowGameOver", GameOverType.EnemyWin, true)
                            .GetValue();
                    if (GUILayout.Button("團體戰時間到"))
                        Traverse
                            .Create(GameLevelManager.Instance)
                            .Method("ShowGameOver", GameOverType.Timeout, true)
                            .GetValue();
                    if (
                        GUILayout.Button(
                            "標題畫面顯示全按鈕(按兩次)(不知道會不會影響存檔謹慎使用 使用前備份)"
                        )
                    )
                        EnableTitleButton();
                }
                GUILayout.EndVertical();
                GUILayout.BeginHorizontal();
                {
                    // ComboBox button
                    if (GUILayout.Button(items[selectedItemIndex], GUILayout.Width(200)))
                    {
                        _showDropdown = !_showDropdown; // Toggle dropdown visibility
                    }

                    // Dropdown list
                    if (_showDropdown)
                    {
                        float dropdownHeight = Mathf.Min(items.Length * 20, 200); // Calculate dropdown height based on item count

                        scrollPosition = GUILayout.BeginScrollView(
                            scrollPosition,
                            GUILayout.Width(200),
                            GUILayout.Height(dropdownHeight)
                        );
                        {
                            for (int i = 0; i < items.Length; i++)
                            {
                                if (GUILayout.Button(items[i], GUILayout.ExpandWidth(true)))
                                {
                                    selectedItemIndex = i; // Set selected item index
                                    _showDropdown = false; // Close the dropdown
                                    UpdateCurrentStatValue(); // Update the displayed stat value
                                }
                            }
                        }
                        GUILayout.EndScrollView();
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
                        GUILayout.Label(
                            "Flag: " + flagData.name + " State: " + flagData.State,
                            myStyle
                        );
                        GUILayout.Label("Note: " + flagData.DevNote, myStyle);
                        if (GUILayout.Button("+1"))
                        {
                            flagData.State += 1;
                        }

                        if (GUILayout.Button("-1"))
                        {
                            flagData.State -= 1;
                        }
                    }
                }
                GUILayout.EndScrollView();
                GUILayout.EndHorizontal();
            }
            GUILayout.EndArea();
            GUI.DragWindow();
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

        private static void EnableTitleButton()
        {
            Traverse title = Traverse.Create(TitleManager.Instance);
            if (title != null)
            {
                CanvasGroup chapter = title.Field("_chapter").GetValue<CanvasGroup>();
                if (chapter)
                {
                    chapter.gameObject.SetActive(true);
                }

                CanvasGroup missions = title.Field("_missions").GetValue<CanvasGroup>();
                if (missions)
                {
                    missions.gameObject.SetActive(true);
                }

                CanvasGroup combat = title.Field("_combat").GetValue<CanvasGroup>();
                if (combat)
                {
                    combat.gameObject.SetActive(true);
                }

                CanvasGroup testPanel = title.Field("_testPanel").GetValue<CanvasGroup>();
                if (testPanel)
                {
                    testPanel.gameObject.SetActive(true);
                }

                CanvasGroup previewPanel = title.Field("_previewPanel").GetValue<CanvasGroup>();
                if (previewPanel)
                {
                    previewPanel.gameObject.SetActive(true);
                }
            }

            GameObject parentObject = GameObject.Find("UI/Layer_1/Buttons");
            if (!parentObject)
                return;

            foreach (Transform child in parentObject.transform)
            {
                // Set each child GameObject active
                child.gameObject.SetActive(true);
            }
        }
    }
}
