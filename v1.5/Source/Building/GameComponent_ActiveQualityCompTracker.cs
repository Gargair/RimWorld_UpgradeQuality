using System.Collections.Generic;
using Verse;

namespace UpgradeQuality.Building
{
    public class GameComponent_ActiveQualityCompTracker : GameComponent
    {
        private List<Comp_UpgradeQuality_Building> activeQualityComps = new List<Comp_UpgradeQuality_Building>();
        private Game activeGame;
        public GameComponent_ActiveQualityCompTracker(Game game) { this.activeGame = game; }

        public override void LoadedGame()
        {
            activeQualityComps.Clear();
            foreach (Map map in activeGame.Maps)
            {
                foreach (var thing in map.listerBuildings.allBuildingsColonist)
                {
                    if (thing.TryGetComp<Comp_UpgradeQuality_Building>(out var comp))
                    {
                        this.AddComponent(comp);
                    }
                }
            }
        }

        public override void StartedNewGame()
        {
            activeQualityComps.Clear();
        }

        public override void GameComponentTick()
        {
            for (int i = activeQualityComps.Count - 1; i >= 0; i--)
            {
                var comp = activeQualityComps[i];
                if (comp == null || !comp.IsStillActive())
                {
                    activeQualityComps.RemoveAt(i);
                }
                if (comp.parent.IsHashIntervalTick(600))
                {
                    comp.CheckAndDoUpgrade();
                }
            }
        }

        public void AddComponent(Comp_UpgradeQuality_Building comp)
        {
            if (!this.activeQualityComps.Contains(comp) && comp.IsStillActive())
            {
                this.activeQualityComps.Add(comp);
            }
        }
    }
}
