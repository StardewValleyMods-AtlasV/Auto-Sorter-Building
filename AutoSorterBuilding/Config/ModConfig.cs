namespace AutoSorterBuilding.Config
{
    // read/written via helper.ReadConfig<T>/WriteConfig<T>.
    // Field names/casing intentionally mirror the old CP ConfigSchema keys.
    internal sealed class ModConfig
    {
        // When enabled, Chests Anywhere names get a "1. ", "2. " etc. prefix based on reading order.
        // When disabled, the name is just the plain category/label with no numbering.
        public bool EnableChestNumbering { get; set; } = true;

        public bool EnableSeasonalVariants { get; set; } = true;

        // One of: Vanilla, Blue, DarkBlue, DarkBrown, DarkRoof, Green, LightBrown, Pink, Red, WhiteRoof
        // Matches the folder name under Assets/Images/{Saturated|Desaturated}/.
        public string Appearance { get; set; } = "Vanilla";

        public bool EnableDesaturatedVersion { get; set; } = false;
        
        // Selector-priority ranks (1 = tried first). Defaults mirror the previous hardcoded
        // order in SortKeyExtractorRegistry (Geode, Flavour, Colour, Quality, ModID).
        // Ties are broken by that same default order, see SortKeyExtractorRegistry.GetPriorityOrder.
        public int GeodePriority { get; set; } = 1;
        public int FlavourPriority { get; set; } = 2;
        public int ColourPriority { get; set; } = 3;
        public int QualityPriority { get; set; } = 4;
        public int ModIdPriority { get; set; } = 5;
    }
}