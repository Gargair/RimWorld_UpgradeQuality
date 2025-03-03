using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using UpgradeQuality.Building;
using Verse;

namespace UpgradeQuality
{
    public class UpgradeQualitySettings : ModSettings
    {
        public float Factor_Awful_Poor = 1;
        public float Factor_Poor_Normal = 2;
        public float Factor_Normal_Good = 3;
        public float Factor_Good_Excellent = 4;
        public float Factor_Excellent_Masterwork = 5;
        public float Factor_Masterwork_Legendary = 6;
        public QualityCategory MaxQuality = QualityCategory.Legendary;
        public bool IsKeepOptionEnabled = false;
        public bool LimitItemQualityToWorkbench = true;
        private string AwfulBuffer = null;
        private string PoorBuffer = null;
        private string NormalBuffer = null;
        private string GoodBuffer = null;
        private string ExcellentBuffer = null;
        private string MasterworkBuffer = null;

        private Vector2 ScrollPosition = Vector2.zero;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref Factor_Awful_Poor, "Factor_Awful_Poor", 1);
            Scribe_Values.Look(ref Factor_Poor_Normal, "Factor_Poor_Normal", 2);
            Scribe_Values.Look(ref Factor_Normal_Good, "Factor_Normal_Good", 3);
            Scribe_Values.Look(ref Factor_Good_Excellent, "Factor_Good_Excellent", 4);
            Scribe_Values.Look(ref Factor_Excellent_Masterwork, "Factor_Excellent_Masterwork", 5);
            Scribe_Values.Look(ref Factor_Masterwork_Legendary, "Factor_Masterwork_Legendary", 6);
            Scribe_Values.Look(ref IsKeepOptionEnabled, "IsKeepOptionEnabled", false);
            Scribe_Values.Look(ref LimitItemQualityToWorkbench, "LimitItemQualityToWorkbench", true);
            Scribe_Values.Look(ref MaxQuality, "MaxQuality", QualityCategory.Legendary);
        }

        public void DoWindowContents(Rect canvas)
        {
            var awfulString = QualityCategory.Awful.GetLabel();
            var poorString = QualityCategory.Poor.GetLabel();
            var normalString = QualityCategory.Normal.GetLabel();
            var goodString = QualityCategory.Good.GetLabel();
            var excellentString = QualityCategory.Excellent.GetLabel();
            var masterworkString = QualityCategory.Masterwork.GetLabel();
            var legendaryString = QualityCategory.Legendary.GetLabel();
            var list = new Listing_Standard();
            Rect innerRect = new Rect();
            innerRect.x = 0;
            innerRect.y = 0;
            innerRect.height = Text.LineHeight + 6f + 6 * (Text.LineHeight + 70f + 1f) + 3 * Text.LineHeight + 20f;
            innerRect.width = canvas.width - 20f;
            Widgets.BeginScrollView(canvas, ref ScrollPosition, innerRect);
            list.Begin(innerRect);
            var labelRect = list.GetRect(Text.LineHeight);
            Widgets.Label(labelRect, "UpgQlty.Labels.Settings.MaterialMultiplier".Translate());
            list.Gap(6f);

            BuildMaterialSlider(list, ref Factor_Awful_Poor, ref AwfulBuffer, awfulString, poorString);
            BuildMaterialSlider(list, ref Factor_Poor_Normal, ref PoorBuffer, poorString, normalString);
            BuildMaterialSlider(list, ref Factor_Normal_Good, ref NormalBuffer, normalString, goodString);
            BuildMaterialSlider(list, ref Factor_Good_Excellent, ref GoodBuffer, goodString, excellentString);
            BuildMaterialSlider(list, ref Factor_Excellent_Masterwork, ref ExcellentBuffer, excellentString, masterworkString);
            BuildMaterialSlider(list, ref Factor_Masterwork_Legendary, ref MasterworkBuffer, masterworkString, legendaryString);
            BuildCheckBox(list, ref IsKeepOptionEnabled, "UpgQlty.Labels.Settings.IsKeepOptionEnabled", "UpgQlty.Tooltips.Settings.IsKeepOptionEnabled");
            BuildCheckBox(list, ref LimitItemQualityToWorkbench, "UpgQlty.Labels.Settings.LimitItemQualityToWorkbench", "UpgQlty.Tooltips.Settings.LimitItemQualityToWorkbench");
            BuildQualitySelector(list);

            list.End();
            Widgets.EndScrollView();
        }

        private void BuildSlider(Listing_Standard listing_Standard, ref float valueRef, ref string inputBuffer, float minValue, float maxValue, TaggedString labelText, TaggedString tooltipText, bool withGap)
        {
            var contentRect = listing_Standard.GetRect(Text.LineHeight + 70f);
            var topRect = contentRect.TopPartPixels(Text.LineHeight);
            var labelRect = topRect.LeftHalf();
            var textInput = topRect.RightHalf();
            var sliderRect = contentRect.BottomPartPixels(50f);
            Widgets.Label(labelRect, labelText);
            Widgets.TextFieldNumeric(textInput, ref valueRef, ref inputBuffer, minValue, maxValue);
            Widgets.HorizontalSlider(sliderRect, ref valueRef, new FloatRange(minValue, maxValue));
            TooltipHandler.TipRegion(labelRect, tooltipText);
            if (withGap)
            {
                listing_Standard.Gap(1f);
            }
        }

        private void BuildMaterialSlider(Listing_Standard listing_Standard, ref float matRef, ref string intBuf, string catFromText, string catToText, bool withGap = true)
        {
            var labelText = "UpgQlty.Labels.Settings.MaterialsNeededFor".Translate(catFromText, catToText, matRef.ToString());
            var tooltiptext = "UpgQlty.Tooltips.Settings.MaterialsNeededTooltip".Translate();
            BuildSlider(listing_Standard, ref matRef, ref intBuf, 0.01f, 20f, labelText, tooltiptext, withGap);
        }

        private void BuildCheckBox(Listing_Standard listing_Standard, ref bool option, string labelKey, string tooltipKey)
        {
            var contentRect = listing_Standard.GetRect(Text.LineHeight);
            var labelRect = contentRect.LeftHalf();
            var checkBox = contentRect.RightHalf();
            Widgets.Label(labelRect, labelKey.Translate());
            TooltipHandler.TipRegion(labelRect, tooltipKey.Translate());
            Widgets.Checkbox(checkBox.position, ref option, Text.LineHeight);
        }

        private void BuildQualitySelector(Listing_Standard listing_Standard)
        {
            var contentRect = listing_Standard.GetRect(Text.LineHeight);
            var labelRect = contentRect.LeftHalf();
            var selector = contentRect.RightHalf();
            Widgets.Label(labelRect, "UpgQlty.Labels.Settings.MaxQuality".Translate());
            TooltipHandler.TipRegion(labelRect, "UpgQlty.Tooltips.Settings.MaxQuality".Translate());

            // Implementation for quality picker dropdown
            if (Widgets.ButtonText(selector, MaxQuality.GetLabel()))
            {
                var options = new List<FloatMenuOption>();

                // Add each quality level as an option
                foreach (QualityCategory quality in QualityUtility.AllQualityCategories)
                {
                    options.Add(new FloatMenuOption(quality.GetLabel(), () => {
                        MaxQuality = quality;
                    }));
                }

                Find.WindowStack.Add(new FloatMenu(options) { vanishIfMouseDistant = true });
            }
        }
    }
}