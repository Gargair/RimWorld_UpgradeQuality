using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace UpgradeQuality
{
    public class UpgradeQuality : Mod
    {
        public UpgradeQuality(ModContentPack content) :base(content) {
            //UpgradeQualityUtility.LogMessage(LogLevel.Information, "Starting up");
            Settings = GetSettings<UpgradeQualitySettings>();
        }

        public override string SettingsCategory()
        {
            return "UpgQlty.ModName".Translate();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Settings.DoWindowContents(inRect);
        }

        public static UpgradeQualitySettings Settings;
    }
}
