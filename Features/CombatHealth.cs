using HarmonyLib;
using Mortal.Battle;
using Mortal.Combat;
using UnityEngine;

namespace LegendOfMortalMod.Features
{
    // 單挑中改雙方血量，好觸發「血量低於多少」的事件；放在基本頁戰鬥資訊的上方
    internal static class CombatHealth
    {
        private static string _playerInput = "";
        private static string _enemyInput = "";

        // Only during a 單挑 (no GameLevelManager); called while the battle info shows, so it stays the same
        // between Layout and Repaint
        public static void Draw()
        {
            if (GameLevelManager.Instance != null)
                return;
            CombatManager combat = Traverse
                .Create(typeof(CombatManager))
                .Property("Instance")
                .GetValue<CombatManager>();
            if (combat == null || combat.PlayerAction == null || combat.EnemyAction == null)
                return;
            DrawRow("我方", combat.PlayerAction, ref _playerInput);
            DrawRow("敵方", combat.EnemyAction, ref _enemyInput);
        }

        private static void DrawRow(string side, CombatActionController action, ref string input)
        {
            CombatStatController stat = action.Stat;
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{side}血量 {stat.CurrentHealth} / {stat.MaxHealthValue}", GUILayout.Width(200));
            input = GUILayout.TextField(input, GUILayout.Width(66));
            // At least 1: 0 kills mid-turn, and ending a fight is what 判定勝利/落敗 is for
            if (GUILayout.Button("設定", GUILayout.Width(50)) && int.TryParse(input, out int value) && !action.IsDead)
            {
                value = Mathf.Clamp(value, 1, stat.MaxHealthValue);
                // The game's own path: clamps, updates the bar and the weak pose
                action.StartCoroutine(action.ModifyHealth(value - stat.CurrentHealth));
                input = "";
            }
            GUILayout.EndHorizontal();
        }
    }
}
