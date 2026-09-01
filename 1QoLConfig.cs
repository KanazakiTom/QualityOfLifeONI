using Newtonsoft.Json;
using PeterHan.PLib.Options;

namespace QualityOfLifeONI
{
    // ==========================================
    // MASTER CONFIG
    // ==========================================
    [JsonObject(MemberSerialization.OptIn)]
    [RestartRequired]
    public class QoLConfig
    {
        // ------------------------------------------
        // CATEGORY: BEETA SETTINGS
        // ------------------------------------------
        [Option("Sleep Blocks", "How many blocks at the end of the cycle the Beetas should sleep (1 block = 25s).", "Beeta Settings")]
        [Limit(3, 8)]
        [JsonProperty]
        public int SleepBlocks { get; set; } = 3;

        // ------------------------------------------
        // CATEGORY: TOOL FILTERS
        // ------------------------------------------
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

        // ------------------------------------------
        // CATEGORY: CUSTOMIZABLE SPEED
        // ------------------------------------------
        [Option("Slow speed", "Speed multiplier for 1x game speed.", "Customizable Speed")]
        [Limit(0.0, 10.0)]
        [JsonProperty]
        public float SlowSpeed { get; set; } = 1f;

        [Option("Normal speed", "Speed multiplier for 2x game speed.", "Customizable Speed")]
        [Limit(0.0, 20.0)]
        [JsonProperty]
        public float NormalSpeed { get; set; } = 2f;

        [Option("Super speed", "Speed multiplier for 3x game speed.", "Customizable Speed")]
        [Limit(0.0, 30.0)]
        [JsonProperty]
        public float SuperSpeed { get; set; } = 3f;

        // ------------------------------------------
        // CATEGORY: BETTER RAD PILLS
        // ------------------------------------------
        [Option("Rad threshold", "How many rads before a dupe is allowed to consume a radpill (100/300/600 rads = minor/major/extreme sickness, 900 = incapacitated).", "Better Rad Pills")]
        [Limit(0.0, 900.0)]
        [JsonProperty]
        public float Rads { get; set; } = 33f;

        [Option("Faster animation", "Dupes ingest rad pills faster (10s => 1s).", "Better Rad Pills")]
        [JsonProperty]
        public bool FasterAnim { get; set; } = true;

        // ------------------------------------------
        // CATEGORY: CRYO CONDENSER
        // ------------------------------------------
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
            [Option("Hard (Legacy Mode)", "Cools liquid 14°C below its boiling point.")]
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

            [JsonProperty]
            [Option("Enable Turbo Mode (Alpha)", "Enable the sidescreen button to toggle 4x speed and power mode.")]
            public bool EnableTurboMode { get; set; } = false;
        }

        public class CaiLibConfig
        {
            [JsonProperty]
            public int Height { get; set; } = 8;
        }
    }
}