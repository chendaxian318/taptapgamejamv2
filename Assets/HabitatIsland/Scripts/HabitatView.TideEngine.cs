using UnityEngine;

namespace HabitatIsland
{
    public partial class HabitatView
    {
        GameObject tideEnginePrefab;
        bool AddTideEngine(Transform parent)
        {
            if (!tideEnginePrefab) tideEnginePrefab = Resources.Load<GameObject>("Models/TideEngine/TideEngine");
            if (!tideEnginePrefab) return false;
            var instance = Instantiate(tideEnginePrefab, parent, false);
            instance.name = "Tide Engine · rune pressure plate";
            return true;
        }
        void UpdateTideEngineVisuals(HabitatState shown, float dt, bool reset)
        {
            if (tiles == null) return;
            // Grid movement displays a candidate before commit. Only the authoritative
            // state may turn a pressure plate on, including during visible recovery.
            var game = HabitatGame.Instance;
            HabitatState committed = game && game.State != null ? game.State : shown;
            for (int i = 0; i < tiles.Length; i++)
            {
                var cell = tiles[i];
                if (!cell.tide) { cell.tideKnown = false; continue; }
                Tile tile = i < committed.tiles.Length && committed.width == shown.width && committed.depth == shown.depth ? committed.tiles[i] : null;
                bool active = tile != null && tile.coveredProp == PropKind.Tide && (tile.prop == PropKind.Crate || tile.prop == PropKind.Float);
                if (reset) cell.tide.ResetVisual(active);
                else
                {
                    if (!cell.tide.IsInitialized) cell.tide.ResetVisual(cell.tideKnown ? cell.tidePressed : active, cell.tideKnown ? cell.tideCompression : -1f);
                    cell.tide.Render(active, dt);
                }
                cell.tideKnown = true; cell.tidePressed = active; cell.tideCompression = cell.tide.Compression;
            }
            // A carried engine is unpressed; it must not inherit a previous floor state.
            if (carriedVisual && shown.carryingProp == PropKind.Tide)
            {
                var carried = carriedVisual.GetComponentInChildren<HabitatTideEngineVisual>();
                if (carried) carried.Render(false, dt);
            }
        }
    }
}
