using UnityEngine;
using Verse;

namespace UpgradeQuality
{
    public class UpgradeQuality : Mod
    {
        public UpgradeQuality(ModContentPack content) : base(content)
        {
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

        public static UpgradeQualitySettings Settings { get; set; }
    }
}
