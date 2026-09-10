using KSerialization;
using UnityEngine;
using static QualityOfLifeONI.QoLConfig;

namespace QualityOfLifeONI
{
    [SerializationConfig(MemberSerialization.OptIn)]
    public class CryoCondenser : KMonoBehaviour, ISim200ms, ISidescreenButtonControl
    {
        [MyCmpReq]
        private readonly Storage storage;
        [MyCmpReq]
        private readonly Operational operational;
        [MyCmpReq]
        private readonly PrimaryElement primaryElement;
        [MyCmpReq]
        private readonly EnergyConsumer energyConsumer;

        private const float BASE_BATCH_MASS_KG = 10f;
        private const float HIGH_WATER_MARK_KG = 100f;

        [Serialize]
        private bool isBufferingFull = false;

        [Serialize]
        private bool isTurboMode = false;

        protected override void OnSpawn()
        {
            base.OnSpawn();

            if (!(ModInit.Config?.CryoCondenser_EnableTurboMode ?? false))
            {
                isTurboMode = false;
            }

            UpdatePowerConsumption();
        }

        private void UpdatePowerConsumption()
        {
            float basePower = ModInit.Config?.CryoCondenser_PowerConsumption ?? 2400f;
            bool turboEnabled = ModInit.Config?.CryoCondenser_EnableTurboMode ?? false;
            float currentRequirement = (isTurboMode && turboEnabled) ? basePower * 4f : basePower;

            if (energyConsumer != null)
            {
                energyConsumer.BaseWattageRating = currentRequirement;
            }
        }

        public void Sim200ms(float dt)
        {
            if (!operational.IsOperational)
            {
                operational.SetActive(false);
                return;
            }

            // 1. Calculate stored gas mass per element
            System.Collections.Generic.Dictionary<Element, float> elementMassMap = new System.Collections.Generic.Dictionary<Element, float>();
            float totalAllGasMass = 0f;

            for (int i = 0; i < storage.items.Count; i++)
            {
                GameObject item = storage.items[i];
                if (item == null) continue;

                PrimaryElement pe = item.GetComponent<PrimaryElement>();
                if (pe != null && pe.Element.IsGas && pe.Element.lowTempTransition != null && pe.Mass > 0f)
                {
                    if (!elementMassMap.ContainsKey(pe.Element))
                    {
                        elementMassMap[pe.Element] = 0f;
                    }
                    elementMassMap[pe.Element] += pe.Mass;
                    totalAllGasMass += pe.Mass;
                }
            }

            // 2. Buffer State Logic
            if (!isBufferingFull)
            {
                if (totalAllGasMass >= HIGH_WATER_MARK_KG)
                {
                    isBufferingFull = true;
                }
                else
                {
                    operational.SetActive(false);
                    return;
                }
            }

            bool turboEnabled = ModInit.Config?.CryoCondenser_EnableTurboMode ?? false;
            float batchMass = (isTurboMode && turboEnabled) ? BASE_BATCH_MASS_KG * 4f : BASE_BATCH_MASS_KG;

            // 3. Find target element
            Element targetGasElement = null;
            float maxMassFound = 0f;

            foreach (var kvp in elementMassMap)
            {
                if (kvp.Value >= batchMass && kvp.Value > maxMassFound)
                {
                    maxMassFound = kvp.Value;
                    targetGasElement = kvp.Key;
                }
            }

            if (targetGasElement == null)
            {
                isBufferingFull = false;
                operational.SetActive(false);
                return;
            }

            // 4. Process Batch
            Element liquidElement = targetGasElement.lowTempTransition;
            float massToConvert = batchMass;

            float targetTempKelvin;
            if (ModInit.Config?.CryoCondenser_OutputCoolingMode == CoolingMode.Safe)
            {
                targetTempKelvin = liquidElement.lowTemp + 4f;
            }
            else
            {
                targetTempKelvin = Mathf.Max(targetGasElement.lowTemp - 14f, 1f);
            }

            float remainingToConsume = massToConvert;
            float totalGasTempSum = 0f;
            byte diseaseIdx = 0;
            int diseaseCount = 0;

            for (int i = storage.items.Count - 1; i >= 0; i--)
            {
                GameObject item = storage.items[i];
                if (item == null) continue;

                PrimaryElement pe = item.GetComponent<PrimaryElement>();
                if (pe != null && pe.Element == targetGasElement && pe.Mass > 0f)
                {
                    float amountFromThisItem = Mathf.Min(pe.Mass, remainingToConsume);
                    totalGasTempSum += pe.Temperature * amountFromThisItem;
                    diseaseIdx = pe.DiseaseIdx;
                    diseaseCount += pe.DiseaseCount;

                    remainingToConsume -= amountFromThisItem;
                    pe.Mass -= amountFromThisItem;

                    if (pe.Mass <= 0f)
                    {
                        storage.ConsumeIgnoringDisease(item);
                    }

                    if (remainingToConsume <= 0f) break;
                }
            }

            float averageGasTemp = totalGasTempSum / massToConvert;
            float tempDiff = averageGasTemp - targetTempKelvin;

            if (tempDiff > 0f)
            {
                float heatExtractedDTU = massToConvert * targetGasElement.specificHeatCapacity * tempDiff;
                float buildingMass = primaryElement.Mass;
                float buildingSHC = primaryElement.Element.specificHeatCapacity;

                if (buildingMass > 0f && buildingSHC > 0f)
                {
                    primaryElement.Temperature += heatExtractedDTU / (buildingMass * buildingSHC);
                }
            }

            operational.SetActive(true);

            SimHashes liquidHash = liquidElement.id;
            storage.AddLiquid(
                liquidHash,
                massToConvert,
                targetTempKelvin,
                diseaseIdx,
                diseaseCount,
                false,
                true
            );
        }

        #region ISidescreenButtonControl Interface
        public string SidescreenTitle => "Cryo Condenser";

        public string SidescreenButtonText => isTurboMode ? "Mode: TURBO (4x)" : "Mode: Classic (1x)";

        public string SidescreenButtonTooltip => isTurboMode
            ? "Currently operating at 4x speed and consuming 4x power."
            : "Currently operating at normal speed and power consumption.";

        public bool SidescreenEnabled() => ModInit.Config?.CryoCondenser_EnableTurboMode ?? false;

        public bool SidescreenButtonInteractable() => ModInit.Config?.CryoCondenser_EnableTurboMode ?? false;

        public bool SidescreenButtonShowable() => ModInit.Config?.CryoCondenser_EnableTurboMode ?? false;

        public void SetButtonTextOverride(ButtonMenuTextOverride text) { }

        public void OnSidescreenButtonPressed()
        {
            if (ModInit.Config?.CryoCondenser_EnableTurboMode ?? false)
            {
                isTurboMode = !isTurboMode;
                UpdatePowerConsumption();
            }
        }

        public int ButtonSideScreenSortOrder() => 20;

        public int HorizontalGroupID() => -1;
        #endregion
    }
}