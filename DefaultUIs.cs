using Newtonsoft.Json;
using PeterHan.PLib.Options;

namespace QualityOfLifeONI
{
    [JsonObject(MemberSerialization.OptIn)]
    //[ModInfo("Default Tool Filters", "https://github.com/yourusername/DefaultToolFilters")]
    [RestartRequired]
    public class ToolFilterOptions
    {
        // --- DIG TOOL FILTERS ---
        [Option("Dig: Include Natural Backwall", "Should the Dig tool include Natural Backwalls by default?")]
        [JsonProperty]
        public bool DigNaturalBackwall { get; set; } = false;

        [Option("Dig: Include Plants", "Should the Dig tool include Plants by default?")]
        [JsonProperty]
        public bool DigPlants { get; set; } = true;

        [Option("Dig: Include Tiles", "Should the Dig tool include Solid Tiles by default?")]
        [JsonProperty]
        public bool DigTiles { get; set; } = true;

        // --- DECONSTRUCT TOOL FILTER ---
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

        [Option("Default Deconstruct Filter", "Select the default selection filter for the Deconstruct tool.")]
        [JsonProperty]
        public DeconstructFilterOptions DefaultDeconstructFilter { get; set; } = DeconstructFilterOptions.All;

        // --- PRIORITY TOOL FILTER ---
        public enum PriorityFilterOptions
        {
            [Option("All")] All,
            [Option("Construction")] Construction,
            [Option("Digging")] Digging,
            [Option("Cleaning")] Cleaning,
            [Option("Duties")] Duties
        }

        [Option("Default Priority Filter", "Select the default selection filter for the Priority tool.")]
        [JsonProperty]
        public PriorityFilterOptions DefaultPriorityFilter { get; set; } = PriorityFilterOptions.All;
    }
}