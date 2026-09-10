using System.Collections.Generic;
using QualityOfLifeONI;
using TUNING;
using UnityEngine;

namespace QualityOfLifeONI
{
    public class CryoCondenserConfig : IBuildingConfig
    {
        public const string ID = "CryoCondenser";

        public override BuildingDef CreateBuildingDef()
        {
            string id = ID;
            int width = 2;
            int height = 2;
            string anim = "liquidconditioner_kanim";
            int hitpoints = 100;
            float construction_time = 120f;
            float[] tierMass = BUILDINGS.CONSTRUCTION_MASS_KG.TIER6;
            string[] rawMetals = MATERIALS.ALL_METALS;
            float melting_point = 1600f;
            BuildLocationRule build_location_rule = BuildLocationRule.OnFloor;
            EffectorValues noiseTier = NOISE_POLLUTION.NOISY.TIER3;

            BuildingDef buildingDef = BuildingTemplates.CreateBuildingDef(
                id, width, height, anim, hitpoints, construction_time,
                tierMass, rawMetals, melting_point, build_location_rule,
                BUILDINGS.DECOR.NONE, noiseTier, 0.2f
            );

            BuildingTemplates.CreateElectricalBuildingDef(buildingDef);

            float powerRequirement = ModInit.Config?.CryoCondenser_PowerConsumption ?? 2400f;
            buildingDef.EnergyConsumptionWhenActive = powerRequirement;
            buildingDef.ExhaustKilowattsWhenActive = 0f;
            buildingDef.SelfHeatKilowattsWhenActive = 0f;

            buildingDef.InputConduitType = ConduitType.Gas;
            buildingDef.UtilityInputOffset = new CellOffset(0, 1);

            buildingDef.OutputConduitType = ConduitType.Liquid;
            buildingDef.UtilityOutputOffset = new CellOffset(1, 0);

            buildingDef.Floodable = false;
            buildingDef.PowerInputOffset = new CellOffset(1, 0);

            buildingDef.PermittedRotations = PermittedRotations.FlipH;
            buildingDef.ViewMode = OverlayModes.LiquidConduits.ID;
            buildingDef.Overheatable = true;
            buildingDef.OverheatTemperature = 398.15f;
            buildingDef.LogicInputPorts = LogicOperationalController.CreateSingleInputPortList(new CellOffset(1, 1));

            GeneratedBuildings.RegisterWithOverlay(OverlayScreen.LiquidVentIDs, ID);

            return buildingDef;
        }

        public override void ConfigureBuildingTemplate(GameObject go, Tag prefab_tag)
        {
            Storage storage = go.AddOrGet<Storage>();
            storage.capacityKg = 200f;
            storage.showInUI = true;
            storage.SetDefaultStoredItemModifiers(StoredItemModifiers);

            ConduitConsumer conduitConsumer = go.AddOrGet<ConduitConsumer>();
            conduitConsumer.conduitType = ConduitType.Gas;
            conduitConsumer.consumptionRate = 10f;
            conduitConsumer.capacityTag = GameTags.Gas;
            conduitConsumer.wrongElementResult = ConduitConsumer.WrongElementResult.Dump;
            conduitConsumer.storage = storage;

            ConduitDispenser conduitDispenser = go.AddOrGet<ConduitDispenser>();
            conduitDispenser.conduitType = ConduitType.Liquid;
            conduitDispenser.alwaysDispense = true;
            conduitDispenser.elementFilter = null;
            conduitDispenser.storage = storage;

            go.AddOrGet<LoopingSounds>();
            go.AddOrGet<CryoCondenser>();
        }

        public override void DoPostConfigureComplete(GameObject go)
        {
            go.AddOrGet<LogicOperationalController>();
            go.AddOrGetDef<PoweredActiveController.Def>();
            go.GetComponent<KPrefabID>().AddTag(GameTags.OverlayBehindConduits, false);
        }

        private static readonly List<Storage.StoredItemModifier> StoredItemModifiers = new List<Storage.StoredItemModifier>
        {
            Storage.StoredItemModifier.Hide,
            Storage.StoredItemModifier.Insulate,
            Storage.StoredItemModifier.Seal
        };
    }
}