using RimWorld;
using System.Collections.Generic;
using Verse;

namespace UpgradeQuality.Building
{
    public class Comp_UpgradeQuality_Building : ThingComp
    {
        public QualityCategory desiredQuality;
        public List<ThingDefCountClass> neededResources;
        public bool keepQuality;

        public CompProperties_UpgradeQuality_Building Props => (CompProperties_UpgradeQuality_Building)props;
        public Frame_UpgradeQuality_Building placedFrame;

        private DesignationManager DesignationManager => parent?.Map?.designationManager;
        private bool HasUpgradeDesignation => DesignationManager?.DesignationOn(parent, UpgradeQualityDefOf.Designations.IncreaseQuality_Building) != null;
        private bool needDesignationAfterSpawn = false;
        private CompQuality CompQuality => parent?.GetComp<CompQuality>();

        public Comp_UpgradeQuality_Building() { }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (!HasUpgradeDesignation)
            {
                if (parent.Faction == Faction.OfPlayer && (parent.GetInnerIfMinified() is Verse.Building))
                {
                    yield return CreateChangeBuildingGizmo();
                }
            }
            yield break;
        }

        private Command CreateChangeBuildingGizmo()
        {
            return new Command_UpgradeQuality_Building();
        }

        public void SetDesiredQualityTo(QualityCategory desiredQuality, bool keepQuality)
        {
            if (CompQuality?.Quality < desiredQuality)
            {
                this.desiredQuality = desiredQuality;
                this.keepQuality = keepQuality;
                PlaceFrame();
                if (DesignationManager == null)
                {
                    needDesignationAfterSpawn = true;
                    return;
                }
                var designation = DesignationManager.DesignationOn(parent, UpgradeQualityDefOf.Designations.IncreaseQuality_Building);
                if (designation == null)
                {
                    DesignationManager.AddDesignation(new Designation(parent, UpgradeQualityDefOf.Designations.IncreaseQuality_Building));
                }
            }
            else if (keepQuality)
            {
                this.desiredQuality = desiredQuality;
                this.keepQuality = keepQuality;
            }
            else
            {
                CancelUpgrade();
            }
        }

        public void PlaceFrame()
        {
            if (placedFrame != null && placedFrame.Spawned)
            {
                placedFrame.Destroy(DestroyMode.Cancel);
                placedFrame = null;
            }
            InitializeResources();
            UpgradeQualityUtility.LogMessage(LogLevel.Debug, "Creating Frame");
            Frame_UpgradeQuality_Building frame = new Frame_UpgradeQuality_Building();
            frame.def = FrameUtility.GetFrameDefForThingDef(parent.def);
            frame.SetStuffDirect(parent.Stuff);
            frame.PostMake();
            frame.PostPostMake();
            frame.StyleSourcePrecept = parent.StyleSourcePrecept;
            frame.thingToChange = parent;
            frame.SetFactionDirect(parent.Faction);
            UpgradeQualityUtility.LogMessage(LogLevel.Debug, "Placing Frame");
            placedFrame = (Frame_UpgradeQuality_Building)GenSpawn.Spawn(frame, parent.Position, parent.Map, parent.Rotation, WipeMode.Vanish);
        }

        public void CancelUpgrade()
        {
            UpgradeQualityUtility.LogMessage(LogLevel.Debug, "CancelUpgrade");
            desiredQuality = QualityCategory.Awful;
            neededResources = null;
            keepQuality = false;
            if (DesignationManager != null)
            {
                var des = DesignationManager.DesignationOn(parent, UpgradeQualityDefOf.Designations.IncreaseQuality_Building);
                if (des != null)
                {
                    DesignationManager.RemoveDesignation(des);
                };
            }
            if (placedFrame != null)
            {
                UpgradeQualityUtility.LogMessage(LogLevel.Debug, "Killing Frame");
                if (placedFrame.Spawned)
                {
                    placedFrame.Destroy(DestroyMode.Cancel);
                }
                placedFrame = null;
            }
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            InitializeResources();
            if (needDesignationAfterSpawn)
            {
                if (DesignationManager != null)
                {
                    needDesignationAfterSpawn = false;
                    DesignationManager.AddDesignation(new Designation(parent, UpgradeQualityDefOf.Designations.IncreaseQuality_Building));
                }
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look<QualityCategory>(ref desiredQuality, "UpgQlty.desiredQuality");
            Scribe_References.Look<Frame_UpgradeQuality_Building>(ref placedFrame, "UpgQlty.placedFrame");
            Scribe_Values.Look(ref keepQuality, "UpgQlty.keepQuality");
        }

        public override string CompInspectStringExtra()
        {
            if (HasUpgradeDesignation)
            {
                return "UpgQlty.Labels.UpgradingTo".Translate(QualityUtility.GetLabel(desiredQuality));
            }
            return base.CompInspectStringExtra();
        }

        private void InitializeResources()
        {
            if (this.CompQuality != null && this.CompQuality.Quality < this.desiredQuality)
            {
                neededResources = UpgradeQualityUtility.GetNeededResources(this.parent);
            }
            else
            {
                neededResources = null;
            }
        }

        public bool IsStillActive()
        {
            if (HasUpgradeDesignation)
            {
                return true;
            }
            if (placedFrame != null)
            {
                UpgradeQualityUtility.LogMessage(LogLevel.Debug, "Found Frame without designation.");
                CancelUpgrade();
            }
            if (keepQuality && CompQuality != null && CompQuality.Quality < desiredQuality)
            {
                return true;
            }
            return false;
        }

        public void CheckAndDoUpgrade()
        {
            if (!HasUpgradeDesignation && this.parent.HitPoints >= this.parent.MaxHitPoints)
            {
                if (keepQuality && CompQuality != null && CompQuality.Quality < desiredQuality)
                {
                    SetDesiredQualityTo(desiredQuality, keepQuality);
                }
            }
        }

    }
}
