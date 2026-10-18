using System.Collections.Generic;
using HarmonyLib;
using Mortal.Battle;
using Mortal.Combat;
using Mortal.Core;
using Mortal.Story;
using UnityEngine;

namespace LegendOfMortalMod
{
    public class Patch
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(CheckPointManager), "Dice")]
        public static bool ModifyDiceNumber(ref int random)
        {
            if (!Plugin.Instance.dice)
                return true;

            if (Plugin.Instance.diceNumber > 0 && Plugin.Instance.diceNumber < 100)
                random = Plugin.Instance.diceNumber;

            return true;
        }

        // 練功 / 煉丹 / 鍛造面板的「最大消費」次數來源，面板打開時記下來
        private static readonly HashSet<StatConvertCollectionData> SpendLimitSources = new HashSet<StatConvertCollectionData>();

        [HarmonyPrefix]
        [HarmonyPatch(typeof(MartialPanel), "Open")]
        public static void RecordMartialSpendLimit(MartialPanel __instance)
        {
            SpendLimitSources.Add(Traverse.Create(__instance).Field("_maxDataConvert").GetValue<StatConvertCollectionData>());
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(UpgradePanel), "Open")]
        public static void RecordUpgradeSpendLimit(UpgradePanel __instance)
        {
            SpendLimitSources.Add(Traverse.Create(__instance).Field("_maxDataConvert").GetValue<StatConvertCollectionData>());
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(StatConvertCollectionData), "GetSummary")]
        public static bool ModifySpendLimit(StatConvertCollectionData __instance, ref float __result)
        {
            if (!Plugin.Instance.spendLimit || Plugin.Instance.spendLimitNumber <= 0 || !SpendLimitSources.Contains(__instance))
                return true;

            __result = Plugin.Instance.spendLimitNumber;
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(DevelopmentOnly), "Start")]
        public static bool DevelopmentOnly(ref DevelopmentOnly __instance)
        {
            bool isActive = Traverse.Create(__instance).Field("_active").GetValue<bool>();
            if (!Debug.isDebugBuild && isActive)
                __instance.gameObject.SetActive(false);
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(CombatResultTestButton), "Start")]
        public static bool TestButton(ref CombatResultTestButton __instance)
        {
            if (!Debug.isDebugBuild)
                __instance.gameObject.SetActive(false);
            return false;
        }
        
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Debug), "isDebugBuild", MethodType.Getter)]
        public static void isDebugBuild(ref bool __result)
        {
            __result = true;
        }
        
        [HarmonyPrefix]
        [HarmonyPatch(typeof(FpsDisplayController), "Update")]
        public static bool FpsDisplayController()
        {
            return false;
        }

        // 左上角資源監視器 Profiler
        [HarmonyPrefix]
        [HarmonyPatch(typeof(ProfilerRecorderController), "OnGUI")]
        public static bool ProfilerRecorderController()
        {
            return false;
        }

        // 左下角劇情 key 除錯輸入框，遊戲在 isDebugBuild 時才顯示
        [HarmonyPostfix]
        [HarmonyPatch(typeof(StoryMainUI), "Awake")]
        public static void HideDebugStoryKey(ref StoryMainUI __instance)
        {
            Traverse.Create(__instance).Field("_debugStoryKey").GetValue<Component>()?.gameObject.SetActive(false);
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
