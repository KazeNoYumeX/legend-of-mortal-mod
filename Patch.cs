using HarmonyLib;
using Mortal.Core;
using UnityEngine;
using Mortal.Battle;

namespace MortalMod
{
    public class Patch
    {
        [HarmonyPrefix, HarmonyPatch(typeof(Mortal.Story.CheckPointManager), "Dice")]
        public static bool ModifyDiceNumber(ref int random)
        {
            if (!Plugin.Instance.dice)
                return true;

            if (Plugin.Instance.diceNumber > 0 && Plugin.Instance.diceNumber < 100)
                random = Plugin.Instance.diceNumber;

            return true;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(Mortal.Core.DevelopmentOnly), "Start")]
        public static bool DevelopmentOnly(ref Mortal.Core.DevelopmentOnly instance)
        {
            bool isActive = Traverse.Create(instance).Field("_active").GetValue<bool>();
            if (!Debug.isDebugBuild && isActive)
                instance.gameObject.SetActive(false);
            return false;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(Mortal.Combat.CombatResultTestButton), "Start")]
        public static bool TestButton(ref Mortal.Combat.CombatResultTestButton instance)
        {
            if (!Debug.isDebugBuild)
                instance.gameObject.SetActive(false);
            return false;
        }

        //改為測試
        [HarmonyPostfix, HarmonyPatch(typeof(Debug), "isDebugBuild", MethodType.Getter)]
        public static void IsDebugBuild(ref bool result)
        {
            result = true;
        }

        //左下角FPS
        [HarmonyPrefix, HarmonyPatch(typeof(FpsDisplayController), "Update")]
        public static bool FpsDisplayController()
        {
            return false;
        }

        //左上角資源監視器Profiler
        [HarmonyPrefix, HarmonyPatch(typeof(ProfilerRecorderController), "OnGUI")]
        public static bool ProfilerRecorderController()
        {
            return false;
        }

        //戰役結束顯示滑鼠
        [HarmonyPostfix, HarmonyPatch(typeof(GameLevelManager), "ContinueGame")]
        public static void ShowMouse(ref GameLevelManager instance)
        {
            if(instance.IsGameOver)
                Traverse.Create(instance).Method("ShowMouseCursor").GetValue();
        }
    }
}
