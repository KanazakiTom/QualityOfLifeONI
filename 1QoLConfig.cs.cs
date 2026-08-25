using Newtonsoft.Json;
using PeterHan.PLib.Options;

namespace QualityOfLifeONI
{
    [JsonObject(MemberSerialization.OptIn)]
    [RestartRequired]
    public class QoLConfig
    {
        // ==========================================
        // CATEGORY: AUTO ARTIFACT MODULE DROP
        // ==========================================
        [Option("Enable Auto Artifact Drop", "Automatically drop artifacts from the Artifact Module after landing.", "Auto Artifact Drop")]
        [JsonProperty]
        public bool ArtifactAutoDropEnabled { get; set; } = true;

        [Option("Drop Delay (Seconds)", "Time to wait in seconds after the rocket lands before automatically dropping the artifact.", "Auto Artifact Drop")]
        [Limit(1, 600)]
        [JsonProperty]
        public float ArtifactDropDelaySeconds { get; set; } = 5f;

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