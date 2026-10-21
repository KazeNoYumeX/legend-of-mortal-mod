using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Mortal.Battle;
using Mortal.Combat;
using Mortal.Core;
using UnityEngine;
using SceneController = Mortal.Core.SceneController;

namespace LegendOfMortalMod
{
    // 戰鬥畫面上方中央的半透明結算按鈕，走遊戲自己的結算流程，劇情旗標和真的打完一樣。
    // 改寫自 Toufff/zhaohuo-system 的 CombatResult，Copyright (c) 2026 Toufff，MIT 授權，見 THIRD-PARTY-NOTICES.md
    internal static class BattleResult
    {
        private const int Win = 0, Lose = 1, Stalemate = 2;
        private static readonly string[] Labels = { "判定勝利", "判定落敗", "判定平手" };
        private const int ForcedEnemyWin = 1; // Mortal.Battle.BattleForceResult
        private const float ConfirmTime = 3f;
        // 第一次按下的按鈕，ConfirmTime 秒內再按一次才結算
        private static int _pending = -1;
        private static float _pendingUntil;
        private static int _endedCombat = -1; // 已經結束的單挑，避免重複結算
        private static object _drawLevel;
        private static bool _drawAvailable;

        // 在 OnGUI 裡、已套用選單縮放的座標下呼叫
        public static void DrawButtons(float scale)
        {
            if (SceneController.Instance != null && SceneController.Instance.IsLoading)
                return;

            // 團體戰執行中滑鼠被鎖住，只在暫停選單時顯示
            GameLevelManager battle = GameLevelManager.Instance;
            bool battleReady =
                battle != null
                && Traverse.Create(battle).Property("IsReady").GetValue() is true
                && !battle.IsGameOver
                && Traverse.Create(battle).Property("IsPause").GetValue() is true;
            CombatManager combat = battle == null ? Traverse.Create(typeof(CombatManager)).Property("Instance").GetValue<CombatManager>() : null;
            bool combatReady =
                combat != null
                && Time.timeScale > 0
                && Traverse.Create(combat).Property("EnableHotKey").GetValue() is true
                && Traverse.Create(combat).Field("_combatLevel").GetValue() != null
                && combat.GetInstanceID() != _endedCombat;
            if (!battleReady && !combatReady)
            {
                _pending = -1;
                return;
            }
            if (_pending >= 0 && Time.unscaledTime > _pendingUntil)
                _pending = -1;

            int[] choices = combatReady && HasDraw(combat) ? new[] { Win, Stalemate, Lose } : new[] { Win, Lose };
            const float width = 96, height = 28, gap = 8;
            float x = (Screen.width / scale - width * choices.Length - gap * (choices.Length - 1)) / 2;
            Color oldColor = GUI.color;
            GUI.color = new Color(1, 1, 1, 0.6f);
            for (int i = 0; i < choices.Length; i++)
            {
                bool asking = _pending == choices[i];
                if (!GUI.Button(new Rect(x + i * (width + gap), 6, width, height), asking ? "再按一次確認" : Labels[choices[i]]))
                    continue;
                if (!asking)
                {
                    _pending = choices[i];
                    _pendingUntil = Time.unscaledTime + ConfirmTime;
                    continue;
                }
                _pending = -1;
                try
                {
                    if (battleReady)
                        FinishBattle(battle, choices[i] == Win);
                    else if (choices[i] == Stalemate)
                        FinishDraw(combat);
                    else
                        FinishCombat(combat, choices[i] == Win);
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning("BattleResult: " + ex.GetBaseException().Message);
                }
            }
            GUI.color = oldColor;
        }

        private static void FinishBattle(GameLevelManager battle, bool win)
        {
            // 輸了劇情還能接下去的關卡用 EnemyWin，其他關卡輸了就是死亡畫面
            GameOverType type = win ? GameOverType.FriendWin
                : StoryLoss(Traverse.Create(battle).Field("_levelData").GetValue()) ? GameOverType.EnemyWin
                : GameOverType.PlayerDie;
            Traverse.Create(battle).Method("PauseGame", false).GetValue();
            Traverse.Create(battle).Method("ShowGameOver", type, type != GameOverType.PlayerDie).GetValue();
        }

        // 敗北、時間到會判負，或關卡事件會強制敵方獲勝
        private static bool StoryLoss(object level)
        {
            if (level == null)
                return false;
            if (Traverse.Create(level).Field("_victoryDefeat").GetValue() is true || Traverse.Create(level).Field("_victoryTimeout").GetValue() is true)
                return true;
            foreach (object data in Plugin.Items(level, "_events"))
            foreach (object action in Plugin.Items(data, "_actions"))
            {
                if (
                    action.GetType().Name == "BattleEventActionFlashGameOver"
                    && Traverse.Create(action).Field("_result").GetValue() is Enum result
                    && Convert.ToInt32(result) == ForcedEnemyWin
                )
                    return true;
            }
            return false;
        }

        private static void FinishCombat(CombatManager combat, bool win)
        {
            StopCombat(combat);
            combat.StartCoroutine(Traverse.Create(combat).Method("GameOver", win).GetValue<IEnumerator>());
        }

        // 先停掉回合迴圈，免得出招或自然結束跟結算同時跑
        private static void StopCombat(CombatManager combat)
        {
            combat.StopAllCoroutines();
            object ui = Traverse.Create(combat).Field("_uiManager").GetValue();
            if (ui != null)
                Traverse.Create(ui).Method("HideAction").GetValue();
            _endedCombat = combat.GetInstanceID();
        }

        private static bool HasDraw(CombatManager combat)
        {
            object level = Traverse.Create(combat).Field("_combatLevel").GetValue();
            if (!ReferenceEquals(level, _drawLevel))
            {
                _drawLevel = level;
                _drawAvailable = DrawEvents(combat).Any();
            }
            return _drawAvailable;
        }

        // 平手：關卡在某回合觸發、會直接載入下一場景的事件。血量或氣的門檻出口不算
        private static IEnumerable<object> DrawEvents(CombatManager combat)
        {
            object level = Traverse.Create(combat).Field("_combatLevel").GetValue();
            foreach (object data in Plugin.Items(level, "Events"))
            foreach (object item in Plugin.Items(data, "_conditionAction"))
            {
                List<object> conditions = Plugin.Items(item, "_conditions").ToList();
                if (
                    !conditions.Exists(condition => condition.GetType().Name == "CombatEventConditionRound")
                    || conditions.Exists(condition => condition.GetType().Name == "CombatEventConditionStat")
                )
                    continue;
                if (ExitActions(item).Exists(action => action.GetType().Name == "CombatEventActionLoadScene"))
                    yield return item;
            }
        }

        // 事件的動作，依單挑執行的順序排，到載入場景為止
        private static List<object> ExitActions(object item)
        {
            List<object> actions = new List<object>();
            foreach (string list in new[] { "_actions", "_afterActions", "_afterResult" })
            foreach (object action in Plugin.Items(item, list))
            {
                actions.Add(action);
                if (action.GetType().Name == "CombatEventActionLoadScene")
                    return actions;
            }
            return actions;
        }

        // 取第一個除了回合以外條件都成立的平手事件，照遊戲到那回合時的方式執行
        private static void FinishDraw(CombatManager combat)
        {
            object draw = DrawEvents(combat)
                .FirstOrDefault(item =>
                    Traverse.Create(item).Field("_logic").GetValue()?.ToString() == "OR"
                    || Plugin.Items(item, "_conditions")
                        .Where(condition => condition.GetType().Name != "CombatEventConditionRound")
                        .All(condition => Traverse.Create(condition).Method("Check", combat).GetValue() is true)
                );
            if (draw == null)
            {
                Plugin.Log.LogWarning("BattleResult: no draw event applies now");
                return;
            }
            StopCombat(combat);
            combat.StartCoroutine(RunActions(combat, ExitActions(draw)));
        }

        private static IEnumerator RunActions(CombatManager combat, List<object> actions)
        {
            foreach (object action in actions)
            {
                if (Traverse.Create(action).Method("Action", combat).GetValue() is IEnumerator step)
                    yield return step;
            }
        }
    }
}
