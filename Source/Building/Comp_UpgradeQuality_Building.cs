using RimWorld;
using System.Collections.Generic;
using UpgradeQuality.Items;
using Verse;

namespace UpgradeQuality.Building
{
    public class Comp_UpgradeQuality_Building : ThingComp
    {
        public QualityCategory desiredQuality;
        public bool keepQuality;

        public CompProperties_UpgradeQuality_Building Props => (CompProperties_UpgradeQuality_Building)props;
        public Frame_UpgradeQuality_Building placedFrame;

        private DesignationManager DesignationManager => parent?.Map?.designationManager;
        private bool HasUpgradeDesignation => UpgradeDesignation != null;
        private Designation UpgradeDesignation => DesignationManager?.DesignationOn(parent, UpgradeQualityDefOf.IncreaseQuality_Building);
        private bool needDesignationAfterSpawn = false;
        public bool SkipRemoveDesignation = false;
        private CompQuality _compQuality;
        private CompQuality CompQuality
        {
            get
            {
                if (_compQuality == null)
                {
                    _compQuality = parent?.GetComp<CompQuality>();
                }
                return _compQuality;
            }
        }
        private readonly GameComponent_ActiveQualityCompTracker tracker = Current.Game.GetComponent<GameComponent_ActiveQualityCompTracker>();

        public Comp_UpgradeQuality_Building() { }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (!HasUpgradeDesignation)
            {
                if (UpgradeQualityUtility.CanBeUpgraded(parent))
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
            if (this.desiredQuality == desiredQuality && this.keepQuality == keepQuality)
            {
                return;
            }
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
                var designation = UpgradeDesignation;
                if (designation == null)
                {
                    DesignationManager.AddDesignation(new Designation(parent, UpgradeQualityDefOf.IncreaseQuality_Building));
                }
                tracker?.AddComponent(this);
            }
            else if (keepQuality)
            {
                this.desiredQuality = desiredQuality;
                this.keepQuality = keepQuality;
                tracker?.AddComponent(this);
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
#if DEBUG && DEBUGBUILDINGS
            UpgradeQualityUtility.LogMessage("Creating Frame");
#endif
            Frame_UpgradeQuality_Building frame = new Frame_UpgradeQuality_Building
            {
                def = FrameUtility.GetFrameDefForThingDef(parent.def)
            };
            frame.SetStuffDirect(parent.Stuff);
            frame.PostMake();
            frame.PostPostMake();
            frame.StyleSourcePrecept = parent.StyleSourcePrecept;
            frame.thingToChange = parent;
            frame.SetFactionDirect(parent.Faction);
            frame.generatedForQuality = CompQuality.Quality;
            frame.NeededResources = InitializeResources();
#if DEBUG && DEBUGBUILDINGS
            UpgradeQualityUtility.LogMessage("Placing Frame");
#endif
            placedFrame = (Frame_UpgradeQuality_Building)GenSpawn.Spawn(frame, parent.Position, parent.Map, parent.Rotation);
        }

        public void CancelUpgrade()
        {
#if DEBUG && DEBUGBUILDINGS
            UpgradeQualityUtility.LogMessage("CancelUpgrade");
#endif
            desiredQuality = QualityCategory.Awful;
            keepQuality = false;

            if (!SkipRemoveDesignation)
            {
                DesignationManager?.TryRemoveDesignationOn(parent, UpgradeQualityDefOf.IncreaseQuality_Building);
            }
            tracker?.RemoveComponent(this);

            if (placedFrame != null)
            {
#if DEBUG && DEBUGBUILDINGS
                UpgradeQualityUtility.LogMessage("Killing Frame");
#endif
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
            if (this.placedFrame != null)
            {
                this.placedFrame.NeededResources = InitializeResources();
            }
            if (needDesignationAfterSpawn)
            {
                if (DesignationManager != null)
                {
                    needDesignationAfterSpawn = false;
                    DesignationManager.AddDesignation(new Designation(parent, UpgradeQualityDefOf.IncreaseQuality_Building));
                }
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref desiredQuality, "UpgQlty.desiredQuality", QualityCategory.Awful, false);
            if (Scribe.mode == LoadSaveMode.Saving && placedFrame != null)
            {
                Scribe_References.Look(ref placedFrame, "UpgQlty.placedFrame");
            }
            else if (Scribe.mode != LoadSaveMode.Saving)
            {
                Scribe_References.Look(ref placedFrame, "UpgQlty.placedFrame");
            }
            Scribe_Values.Look(ref keepQuality, "UpgQlty.keepQuality", false, false);
        }

        public override string CompInspectStringExtra()
        {
            if (HasUpgradeDesignation)
            {
                return "UpgQlty.Labels.UpgradingTo".Translate(QualityUtility.GetLabel(desiredQuality));
            }
            return base.CompInspectStringExtra();
        }

        private List<ThingDefCountQuality> InitializeResources()
        {
            return UpgradeQualityUtility.GetNeededResources(this.parent);
        }

        public bool IsStillActive()
        {
            if (HasUpgradeDesignation)
            {
                if (CompQuality != null && placedFrame != null && CompQuality.Quality != placedFrame.generatedForQuality)
                {
#if DEBUG && DEBUGBUILDINGS
                    UpgradeQualityUtility.LogMessage("Frame generated for different quality!");
#endif
                    PlaceFrame();
                }
                return true;
            }
            if (placedFrame != null)
            {
#if DEBUG && DEBUGBUILDINGS
                UpgradeQualityUtility.LogMessage("Found Frame without designation.");
#endif
                CancelUpgrade();
            }
            if (keepQuality)
            {
                return true;
            }
            if (CompQuality != null && CompQuality.Quality < desiredQuality)
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
