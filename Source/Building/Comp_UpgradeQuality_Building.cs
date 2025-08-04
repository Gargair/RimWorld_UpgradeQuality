using System.Collections.Generic;
using RimWorld;
using UpgradeQuality.Items;
using Verse;

namespace UpgradeQuality.Building
{
    public class Comp_UpgradeQuality_Building : ThingComp
    {
        private QualityCategory _desiredQuality;
        public QualityCategory DesiredQuality
        {
            get => _desiredQuality;
            set => _desiredQuality = value;
        }
        private bool _keepQuality;
        public bool KeepQuality { get => _keepQuality; set => _keepQuality = value; }

        public CompProperties_UpgradeQuality_Building Props => (CompProperties_UpgradeQuality_Building)props;
        private Frame_UpgradeQuality_Building _placedFrame;
        public Frame_UpgradeQuality_Building PlacedFrame { get => _placedFrame; set => _placedFrame = value; }

        private DesignationManager DesignationManager => parent?.Map?.designationManager;
        private bool HasUpgradeDesignation => UpgradeDesignation != null;
        private Designation UpgradeDesignation => DesignationManager?.DesignationOn(parent, UpgradeQualityDefOf.IncreaseQuality_Building);
        private bool needDesignationAfterSpawn = false;
        public bool SkipRemoveDesignation { get; set; } = false;
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
            if (!HasUpgradeDesignation && UpgradeQualityUtility.CanBeUpgraded(parent))
            {
                yield return CreateChangeBuildingGizmo();
            }
        }

        private static Command CreateChangeBuildingGizmo()
        {
            return new Command_UpgradeQuality_Building();
        }

        public void SetDesiredQualityTo(QualityCategory desiredQuality, bool keepQuality)
        {
            if (this.DesiredQuality == desiredQuality && this.KeepQuality == keepQuality)
            {
                return;
            }
            if (CompQuality?.Quality < desiredQuality)
            {
                this.DesiredQuality = desiredQuality;
                this.KeepQuality = keepQuality;
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
                this.DesiredQuality = desiredQuality;
                this.KeepQuality = keepQuality;
                tracker?.AddComponent(this);
            }
            else
            {
                CancelUpgrade();
            }
        }

        public void PlaceFrame()
        {
            if (PlacedFrame != null && PlacedFrame.Spawned)
            {
                PlacedFrame.Destroy(DestroyMode.Cancel);
                PlacedFrame = null;
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
            PlacedFrame = (Frame_UpgradeQuality_Building)GenSpawn.Spawn(frame, parent.Position, parent.Map, parent.Rotation);
        }

        public void CancelUpgrade()
        {
#if DEBUG && DEBUGBUILDINGS
            UpgradeQualityUtility.LogMessage("CancelUpgrade");
#endif
            DesiredQuality = QualityCategory.Awful;
            KeepQuality = false;

            if (!SkipRemoveDesignation)
            {
                DesignationManager?.TryRemoveDesignationOn(parent, UpgradeQualityDefOf.IncreaseQuality_Building);
            }
            tracker?.RemoveComponent(this);

            if (PlacedFrame != null)
            {
#if DEBUG && DEBUGBUILDINGS
                UpgradeQualityUtility.LogMessage("Killing Frame");
#endif
                if (PlacedFrame.Spawned)
                {
                    PlacedFrame.Destroy(DestroyMode.Cancel);
                }
                PlacedFrame = null;
            }
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (this.PlacedFrame != null)
            {
                this.PlacedFrame.NeededResources = InitializeResources();
            }
            if (needDesignationAfterSpawn && DesignationManager != null)
            {
                needDesignationAfterSpawn = false;
                DesignationManager.AddDesignation(new Designation(parent, UpgradeQualityDefOf.IncreaseQuality_Building));
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref _desiredQuality, "UpgQlty.desiredQuality", QualityCategory.Awful, false);
            if ((Scribe.mode == LoadSaveMode.Saving && PlacedFrame != null) || Scribe.mode != LoadSaveMode.Saving)
            {
                Scribe_References.Look(ref _placedFrame, "UpgQlty.placedFrame");
            }
            Scribe_Values.Look(ref _keepQuality, "UpgQlty.keepQuality", false, false);
        }

        public override string CompInspectStringExtra()
        {
            if (HasUpgradeDesignation)
            {
                return "UpgQlty.Labels.UpgradingTo".Translate(QualityUtility.GetLabel(DesiredQuality));
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
                if (CompQuality != null && PlacedFrame != null && CompQuality.Quality != PlacedFrame.generatedForQuality)
                {
#if DEBUG && DEBUGBUILDINGS
                    UpgradeQualityUtility.LogMessage("Frame generated for different quality!");
#endif
                    PlaceFrame();
                }
                return true;
            }
            if (PlacedFrame != null)
            {
#if DEBUG && DEBUGBUILDINGS
                UpgradeQualityUtility.LogMessage("Found Frame without designation.");
#endif
                CancelUpgrade();
            }
            if (KeepQuality)
            {
                return true;
            }
            if (CompQuality != null && CompQuality.Quality < DesiredQuality)
            {
                return true;
            }
            return false;
        }

        public void CheckAndDoUpgrade()
        {
            if (!HasUpgradeDesignation && this.parent.HitPoints >= this.parent.MaxHitPoints && KeepQuality && CompQuality != null && CompQuality.Quality < DesiredQuality)
            {
                SetDesiredQualityTo(DesiredQuality, KeepQuality);
            }
        }

    }
}
