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
