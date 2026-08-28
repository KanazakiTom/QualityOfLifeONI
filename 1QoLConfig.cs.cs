using Newtonsoft.Json;
using PeterHan.PLib.Options;

namespace QualityOfLifeONI
{
    // ==========================================
    // CATEGORY: CRYO CONSENDER
    // ==========================================
    [JsonObject(MemberSerialization.OptIn)]
    [RestartRequired]
    public class QoLConfig
    {
        public enum TechDifficulty
        {
            [Option("Easy (Liquid Temperature)", "Unlocked alongside Thermo Aquatuner / Liquid Temperature tech.")]
            Easy,
            [Option("Hard (Hydrogen Engines / High-Tier)", "Unlocked at late-game Hydrogen Engine research.")]
            Hard
        }

        public enum CoolingMode
        {
            [Option("Easy (Safe Mode)", "Cools liquid to 4°C above its freezing point to prevent pipe bursts.")]
            Safe,
            [Option("Hard (Legacy Mode)", "Cools liquid 14°C below its boiling point (can freeze/burst pipes for narrow-range gases like Hydrogen).")]
            Legacy
        }

        [JsonObject(MemberSerialization.OptIn)]
        [RestartRequired]
        public class PlayerConfig : SingletonOptions<PlayerConfig>
        {
            [JsonProperty]
            [Option("Power Consumption (Watts)", "Set the power usage for the Cryo Condenser.")]
            [Limit(1200f, 10000f)]
            public float PowerConsumption { get; set; } = 2400f;

            [JsonProperty]
            [Option("Tech Tree Difficulty", "Controls where the Cryo Condenser appears in the research tree.")]
            public TechDifficulty Difficulty { get; set; } = TechDifficulty.Easy;

            [JsonProperty]
            [Option("Cooling Mode", "Controls output temperature logic.")]
            public CoolingMode OutputCoolingMode { get; set; } = CoolingMode.Safe;

            // NEW: Experimental / Alpha toggle for Turbo Mode
            [JsonProperty]
            [Option("Enable Turbo Mode (Alpha)", "Enable the sidescreen button to toggle 4x speed and power mode.")]
            public bool EnableTurboMode { get; set; } = false;
        }

        // ==========================================
        // CATEGORY: BEETA SETTINGS
        // ==========================================
        [Option("Sleep Blocks", "How many blocks at the end of the cycle the Beetas should sleep (1 block = 25s).", "Beeta Settings")]
        [Limit(3, 8)]
        [JsonProperty]
        public int SleepBlocks { get; set; } = 3;

        // ==========================================
        // CATEGORY: TOOL FILTERS
        // ==========================================
        [Option("Dig: Include Natural Backwall", "Should the Dig tool include Natural Backwalls by default?", "Tool Filters")]
        [JsonProperty]
        public bool DigNaturalBackwall { get; set; } = false;

        [Option("Dig: Include Plants", "Should the Dig tool include Plants by default?", "Tool Filters")]
        [JsonProperty]
        public bool DigPlants { get; set; } = true;

        [Option("Dig: Include Tiles", "Should the Dig tool include Solid Tiles by default?", "Tool Filters")]
        [JsonProperty]
        public bool DigTiles { get; set; } = true;

        public enum DeconstructFilterOptions
        {
            [Option("All")] All,
            [Option("Power Wires")] PowerWires,
            [Option("Liquid Pipes")] LiquidPipes,
            [Option("Gas Pipes")] GasPipes,
            [Option("Conveyor Rails")] ConveyorRails,
            [Option("Buildings")] Buildings,
            [Option("Automation")] Automation,
            [Option("Background Buildings")] BackgroundBuildings
        }

        [Option("Default Deconstruct Filter", "Select the default selection filter for the Deconstruct tool.", "Tool Filters")]
        [JsonProperty]
        public DeconstructFilterOptions DefaultDeconstructFilter { get; set; } = DeconstructFilterOptions.All;

        public enum PriorityFilterOptions
        {
            [Option("All")] All,
            [Option("Construction")] Construction,
            [Option("Digging")] Digging,
            [Option("Cleaning")] Cleaning,
            [Option("Duties")] Duties
        }

        [Option("Default Priority Filter", "Select the default selection filter for the Priority tool.", "Tool Filters")]
        [JsonProperty]
        public PriorityFilterOptions DefaultPriorityFilter { get; set; } = PriorityFilterOptions.All;
    }
}