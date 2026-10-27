using System.Collections;
using HarmonyLib;
using Mortal.Core;
using Mortal.Free;
using UnityEngine;

namespace LegendOfMortalMod.Features
{
    // 「行動點無限」與地圖上的「跳過本旬」
    // 改寫自 Toufff/zhaohuo-system 的 RoundUnlimited / RoundSkip，Copyright (c) 2026 Toufff，MIT 授權，見 THIRD-PARTY-NOTICES.md
    internal static class RoundControl
    {
        private static bool _unlimited;
        private static bool _skipping;

        public static bool Unlimited
        {
            get => _unlimited;
            set
            {
                PlayerStatManagerData p = PlayerStatManagerData.Instance;
                // Full points when switched on, so options needing more than one are open too
                if (value && !_unlimited && p != null)
                    p.Stats.Set(GameStatType.行動次數, Mathf.Max(p.CurrentActionCount, p.GetMaxActionCount()));
                _unlimited = value;
            }
        }

        // Only on the free map, while it waits for input and no main story waits at a location
        public static bool CanSkip
        {
            get
            {
                FreeManager free = FreeManager.Instance;
                if (_skipping || free == null || free.IsProcessing || Time.timeScale == 0f)
                    return false;
                Traverse t = Traverse.Create(free);
                if (
                    t.Field("_menuOpen").GetValue<bool>()
                    || t.Field("_statOpen").GetValue<bool>()
                    || t.Field("_saveOpen").GetValue<bool>()
                )
                    return false;
                SceneController scenes = SceneController.Instance;
                if (scenes == null || scenes.IsPrepare || scenes.IsLoading)
                    return false;
                MissionManagerData missions = MissionManagerData.Instance;
                if (missions == null || (missions.MainMissionStart && missions.MainMissionPosition != PositionType.無))
                    return false;
                return PlayerStatManagerData.Instance != null && PlayerStatManagerData.Instance.CurrentActionCount > 0;
            }
        }

        public static void Skip()
        {
            if (!CanSkip)
                return;
            _skipping = true;
            FreeManager.Instance.StartCoroutine(SkipRoutine(FreeManager.Instance));
        }

        private static IEnumerator SkipRoutine(FreeManager free)
        {
            try
            {
                // As a location action starts: the map stops taking input, then the screen closes
                free.SetHotKey(false);
                free.DisableButton();
                free.SetProcessing();
                yield return new WaitForSeconds(.3f);
                yield return free.TransitionScreen();
                SaveSystem.Instance.SaveUniverseData();
                if (SpendStepwise())
                    SceneController.Instance.LoadStory();
                else
                    SceneController.Instance.LoadFree();
            }
            finally
            {
                _skipping = false;
            }
        }

        // What LuaManager.ChangeScene("Free") checks after a location's story, once per point given up: a story due
        // partway (some main missions need a set round with 2 or 1 points left) stops there, and at 0 the check is the
        // round change. Checking only once would skip those missions; checking twice would advance one twice.
        private static bool SpendStepwise()
        {
            PlayerStatManagerData stats = PlayerStatManagerData.Instance;
            MissionManagerData missions = MissionManagerData.Instance;
            int left = stats.CurrentActionCount;
            for (int value = left - 1; value >= 0; value--)
            {
                // Set, not spent: 行動點無限 blocks spending
                stats.Stats.Set(GameStatType.行動次數, value);
                missions.UpdateCheckMissions();
                if (missions.HasAnyMissionTrigger())
                {
                    Plugin.Log.LogInfo($"跳過本旬：剩 {value} 點時觸發劇情 {stats.CurrentStoryScript}");
                    return true;
                }
            }
            return false;
        }
    }
}
