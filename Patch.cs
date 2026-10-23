using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using Mortal.Battle;
using Mortal.Combat;
using Mortal.Core;
using Mortal.Story;
using UnityEngine;
using UnityEngine.UI;

namespace LegendOfMortalMod
{
    public class Patch
    {
        // 行動點 / 銀兩 / 修練鍛造煉丹點數無限：扣點都走 AddValue 負值；換旬、跳過本旬和鎖定用 Set，不受影響
        [HarmonyPrefix]
        [HarmonyPatch(typeof(GameStat), "AddValue")]
        public static bool KeepStats(GameStat __instance, int value)
        {
            if (value >= 0)
                return true;
            if (__instance.StatType == GameStatType.行動次數)
                return !RoundControl.Unlimited;
            return !Plugin.Instance.unlimitedResources
                || (__instance.StatType != GameStatType.銀兩 && !PanelPoints.Contains(__instance));
        }

        // 骰子畫面開啟、擲骰、關閉，給 DiceControl 的指定按鈕用
        [HarmonyPrefix]
        [HarmonyPatch(typeof(DiceMenuDialog), "ExecuteRoll")]
        public static void DiceOpen(DiceMenuDialog __instance, string[] options, string checkName)
        {
            DiceControl.Begin(__instance, checkName, options);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(DiceMenuDialog), "Roll")]
        public static void DiceRoll(DiceMenuDialog __instance)
        {
            DiceControl.BeginRoll(__instance);
        }

        // 指定的擲骰跳過轉盤動畫
        [HarmonyPrefix]
        [HarmonyPatch(typeof(DiceMenuDialog), "StartSpin")]
        public static bool DiceFastSpin(DiceMenuDialog __instance, ref IEnumerator __result)
        {
            if (!DiceControl.FastRoll(__instance))
                return true;
            __result = DiceControl.FastSpin(__instance);
            return false;
        }

        // 指定的擲骰出結果後不等「繼續」(也略過命運點重擲的詢問)
        [HarmonyPostfix]
        [HarmonyPatch(typeof(DiceMenuDialog), "GetNextInput")]
        public static void DiceFastContinue(DiceMenuDialog __instance, ref bool __result)
        {
            if (DiceControl.FastRolled(__instance))
                __result = true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(DiceMenuDialog), "OnDisable")]
        public static void DiceClose(DiceMenuDialog __instance)
        {
            DiceControl.End(__instance);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(CheckPointManager), "Dice")]
        public static void BeforeDice(string name, ref int random)
        {
            DiceControl.BeforeDice(name, ref random);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CheckPointManager), "Dice")]
        public static void AfterDice(string name, DiceCheckResult __result)
        {
            DiceControl.AfterDice(name, __result);
        }

        // 修練（武學點數）、鍛造 / 煉丹面板扣的點數，面板打開時記下來給無限點數用
        private static readonly HashSet<GameStat> PanelPoints = new HashSet<GameStat>();

        // 無限點數 / 銀兩：銀兩和記下的點數設成最大值，錢不夠或點數 0 也能用
        public static void FillResources()
        {
            if (PlayerStatManagerData.Instance != null)
            {
                GameStat money = PlayerStatManagerData.Instance.Stats.Get(GameStatType.銀兩);
                money.SetValue(money.Max);
            }
            foreach (GameStat points in PanelPoints)
                points.SetValue(points.Max);
        }

        private static void AddPanelPoints(GameStat points)
        {
            PanelPoints.Add(points);
            if (Plugin.Instance.unlimitedResources)
                points.SetValue(points.Max);
        }

        // 無限消費：修練面板用遊戲自己的無限模式（劇情修練用的 mode 1）
        [HarmonyPrefix]
        [HarmonyPatch(typeof(MartialPanel), "Open")]
        public static void MartialOpen(MartialPanel __instance, ref int mode)
        {
            AddPanelPoints(Traverse.Create(__instance).Field("_martialPoint").GetValue<GameStat>());
            if (Plugin.Instance.unlimitedSpend)
                mode = 1;
        }

        // 面板開著時才勾選，下次刷新就套用
        [HarmonyPrefix]
        [HarmonyPatch(typeof(MartialPanel), "UpdateMartialPointInfo")]
        public static void MartialRefresh(MartialPanel __instance)
        {
            if (Plugin.Instance.unlimitedSpend)
                Traverse.Create(__instance).Field("_mode").SetValue(1);
        }

        public static void RefreshSpendPanels()
        {
            foreach (MartialPanel panel in Object.FindObjectsOfType<MartialPanel>())
            {
                Traverse t = Traverse.Create(panel);
                t.Method("UpdateMartialPointInfo").GetValue();
                // Also re-enables the learn buttons; needs a selected book
                if (t.Field("_currentItem").GetValue() != null)
                    t.Method("UpdateCurrentStatus").GetValue();
            }
            foreach (UpgradePanel panel in Object.FindObjectsOfType<UpgradePanel>())
            {
                Traverse t = Traverse.Create(panel);
                if (t.Field("_currentSelectedItem").GetValue() == null)
                    continue;
                t.Method("UpdateStatus").GetValue();
                t.Method("UpdateButton").GetValue();
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(UpgradePanel), "Open")]
        public static void UpgradeOpen(UpgradePanel __instance)
        {
            AddPanelPoints(Traverse.Create(__instance).Field("_pointData").GetValue<GameStat>());
        }

        // 鍛造 / 煉丹面板沒有無限模式：每次刷新把剩餘次數補滿，顯示改成 ∞
        [HarmonyPrefix]
        [HarmonyPatch(typeof(UpgradePanel), "UpdateStatus")]
        public static void UpgradeRefresh(UpgradePanel __instance)
        {
            if (Plugin.Instance.unlimitedSpend)
                Traverse.Create(__instance).Field("_currentCount").SetValue(99999);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UpgradePanel), "UpdateStatus")]
        public static void UpgradeRefreshText(UpgradePanel __instance)
        {
            if (Plugin.Instance.unlimitedSpend)
                Traverse.Create(__instance).Field("_maxCountText").GetValue<Text>().text =
                    LocalizationManager.Instance.LocaleResolver.GetString("System/Infinity");
        }

        // 遊戲讀過全域存檔才會存它 (LoadUniverseData 結尾也會存)；在那之前講經堂是空的，存了會蓋掉真的紀錄
        [HarmonyPostfix]
        [HarmonyPatch(typeof(SaveSystem), "SaveUniverseData")]
        public static void UniverseSaved()
        {
            Plugin.Instance.universeLoaded = true;
        }

        // 左上角資源監視器 Profiler
        [HarmonyPrefix]
        [HarmonyPatch(typeof(ProfilerRecorderController), "OnGUI")]
        public static bool ProfilerRecorderController()
        {
            return false;
        }

        // 戰役結束顯示滑鼠
        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameLevelManager), "ContinueGame")]
        public static void ShowMouse(ref GameLevelManager __instance)
        {
            if (__instance.IsGameOver)
                Traverse.Create(__instance).Method("ShowMouseCursor").GetValue();
        }
    }
}
