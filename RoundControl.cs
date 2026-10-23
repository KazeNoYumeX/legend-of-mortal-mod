using Mortal.Core;
using UnityEngine;

namespace LegendOfMortalMod
{
    // 「行動點無限」
    // 改寫自 Toufff/zhaohuo-system 的 RoundUnlimited，Copyright (c) 2026 Toufff，MIT 授權，見 THIRD-PARTY-NOTICES.md
    internal static class RoundControl
    {
        private static bool _unlimited;

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
    }
}
