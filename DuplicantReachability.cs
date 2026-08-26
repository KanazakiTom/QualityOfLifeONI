using HarmonyLib;

namespace QualityOfLifeONI
{
    // 1. Fix Deconstruction Reachability
    [HarmonyPatch(typeof(Deconstructable), "OnSpawn")]
    public class Deconstructable_OnSpawn_Patch
    {
        public static void Postfix(Deconstructable __instance)
        {
            if (__instance == null) return;

            Building building = __instance.GetComponent<Building>();
            if (building == null || building.Def == null) return;

            if (building.Def.WidthInCells <= 1 && building.Def.HeightInCells <= 1) return;

            CellOffset[] placementOffsets = AccessTools.PropertyGetter(typeof(Deconstructable), "placementOffsets")
                ?.Invoke(__instance, null) as CellOffset[];

            if (placementOffsets == null) return;

            CellOffset[][] offsetTable = OffsetGroups.BuildReachabilityTable(
                placementOffsets,
                OffsetGroups.InvertedStandardTableWithCorners,
                building.Def.ConstructionOffsetFilter
            );

            __instance.SetOffsetTable(offsetTable);
        }
    }

    // 2. Fix Construction Work AND Material Delivery (Fetch Errands) Reachability
    [HarmonyPatch(typeof(Constructable), "OnSpawn")]
    public class Constructable_OnSpawn_Patch
    {
        public static void Postfix(Constructable __instance)
        {
            if (__instance == null) return;

            BuildingUnderConstruction buildingUC = __instance.GetComponent<BuildingUnderConstruction>();
            if (buildingUC == null || buildingUC.Def == null) return;

            // Skip 1x1 structures
            if (buildingUC.Def.WidthInCells <= 1 && buildingUC.Def.HeightInCells <= 1) return;

            // Build corner-inclusive reachability table for both building work and material supplying
            CellOffset[][] offsetTable = OffsetGroups.BuildReachabilityTable(
                buildingUC.Def.PlacementOffsets,
                OffsetGroups.InvertedStandardTableWithCorners,
                buildingUC.Def.ConstructionOffsetFilter
            );

            // 1. Set offset table on Constructable (used by supply errands and construction chores)
            __instance.SetOffsetTable(offsetTable);

            // 2. Set offset table on Workable (used by duplicant construction work)
            Workable workable = __instance.GetComponent<Workable>();
            if (workable != null)
            {
                workable.SetOffsetTable(offsetTable);
            }
        }
    }
}