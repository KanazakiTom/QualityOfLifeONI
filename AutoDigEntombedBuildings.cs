using HarmonyLib;
using UnityEngine;

namespace QualityOfLifeONI
{
    // 1. Create a custom component that implements ISim1000ms natively
    public class AutoDigEntombedComponent : KMonoBehaviour, ISim1000ms
    {
        [MyCmpReq]
        private Building building;

        public void Sim1000ms(float dt)
        {
            if (building == null) return;

            // Check if building has the red "Entombed" status
            KSelectable selectable = building.GetComponent<KSelectable>();
            if (selectable != null && selectable.HasStatusItem(Db.Get().BuildingStatusItems.Entombed))
            {
                ProcessEntombedBuilding(building);
            }
        }

        private void ProcessEntombedBuilding(Building building)
        {
            if (building.PlacementCells == null || building.PlacementCells.Length == 0) return;

            // find the minimum Y coordinate among placement cells to locate the bottom base row (y=0)
            int minY = int.MaxValue;

            foreach (int cell in building.PlacementCells)
            {
                if (Grid.IsValidCell(cell)){
                    Grid.CellToXY(cell, out int _, out int y);
                    if (y < minY) minY = y;
                }
            
            }
            
            foreach (int cell in building.PlacementCells)
            {
                if (!Grid.IsValidCell(cell)) continue;
                if (building.PrefabID() == "SOLARPANEL" || building.PrefabID() == "CUSTOMSOLARPANEL")
                {
                    Grid.CellToXY(cell, out int cellX, out int cellY);
                    int localY = cellY - minY;

                    if (localY == 0) continue;
                }

                // Solid, diggable, and not Neutronium (hardness < 255)
                if (Grid.Solid[cell] && Grid.Element[cell].hardness < 255)
                {
                    // Ignore foundation tiles
                    if (Grid.Objects[cell, (int)ObjectLayer.FoundationTile] != null) continue;

                    // Only place if no dig errand already exists
                    if (Grid.Objects[cell, (int)ObjectLayer.DigPlacer] == null)
                    {
                        bool wasFoundation = Grid.Foundation[cell];
                        Grid.Foundation[cell] = false;

                        GameObject digGO = DigTool.PlaceDig(cell, 0);

                        Grid.Foundation[cell] = wasFoundation;

                        // Priority High (9)
                        if (digGO != null)
                        {
                            Prioritizable prioritizable = digGO.GetComponent<Prioritizable>();
                            if (prioritizable != null)
                            {
                                prioritizable.SetMasterPriority(new PrioritySetting(PriorityScreen.PriorityClass.high, 9));
                            }
                        }
                    }
                }
            }
        }
    }

    // 2. Attach your component to every building when it spawns!
    [HarmonyPatch(typeof(BuildingComplete), "OnSpawn")]
    public static class BuildingComplete_OnSpawn_Patch
    {
        public static void Postfix(BuildingComplete __instance)
        {
            __instance.gameObject.AddOrGet<AutoDigEntombedComponent>();
        }
    }
}