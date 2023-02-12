using RimWorld;
using UnityEngine;
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

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref Factor_Awful_Poor, "Factor_Awful_Poor", 1);
            Scribe_Values.Look(ref Factor_Poor_Normal, "Factor_Poor_Normal", 2);
            Scribe_Values.Look(ref Factor_Normal_Good, "Factor_Normal_Good", 3);
            Scribe_Values.Look(ref Factor_Good_Excellent, "Factor_Good_Excellent", 4);
            Scribe_Values.Look(ref Factor_Excellent_Masterwork, "Factor_Excellent_Masterwork", 5);
            Scribe_Values.Look(ref Factor_Masterwork_Legendary, "Factor_Masterwork_Legendary", 6);
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
            list.Begin(canvas);
            list.Label("UpgQlty.Labels.Settings.MaterialMultiplier".Translate());
            list.Gap(6f);

            BuildMaterialSlider(list, ref Factor_Awful_Poor, awfulString, poorString);
            BuildMaterialSlider(list, ref Factor_Poor_Normal, poorString, normalString);
            BuildMaterialSlider(list, ref Factor_Normal_Good, normalString, goodString);
            BuildMaterialSlider(list, ref Factor_Good_Excellent, goodString, excellentString);
            BuildMaterialSlider(list, ref Factor_Excellent_Masterwork, excellentString, masterworkString);
            BuildMaterialSlider(list, ref Factor_Masterwork_Legendary, masterworkString, legendaryString, false);

            list.End();
        }

        private void BuildSlider(Listing_Standard listing_Standard, ref float valueRef, float minValue, float maxValue, TaggedString labelText, TaggedString tooltipText, bool withGap)
        {
            var contentRect = listing_Standard.GetRect(Text.LineHeight + 50f);
            var topRect = contentRect.TopPartPixels(Text.LineHeight);
            var labelRect = topRect.LeftHalf();
            var textInput = topRect.RightHalf();
            var sliderRect = contentRect.BottomPartPixels(50f);
            Widgets.Label(labelRect, labelText);
            string inputBuffer = valueRef.ToString();
            Widgets.TextFieldNumeric(textInput, ref valueRef, ref inputBuffer, minValue, maxValue);
            Widgets.HorizontalSlider(sliderRect, ref valueRef, new FloatRange(minValue, maxValue));
            TooltipHandler.TipRegion(labelRect, tooltipText);
            if (withGap)
            {
                listing_Standard.Gap(1f);
            }
        }

        private void BuildMaterialSlider(Listing_Standard listing_Standard, ref float matRef, string catFromText, string catToText, bool withGap = true)
        {
            var labelText = "UpgQlty.Labels.Settings.MaterialsNeededFor".Translate(catFromText, catToText, matRef.ToString());
            var tooltiptext = "UpgQlty.Tooltips.Settings.MaterialsNeededTooltip".Translate();
            BuildSlider(listing_Standard, ref matRef, 0.1f, 10f, labelText, tooltiptext, withGap);
        }
    }
}