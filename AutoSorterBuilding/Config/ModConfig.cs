namespace AutoSorterBuilding.Config
{
    // read/written via helper.ReadConfig<T>/WriteConfig<T>.
    // Field names/casing intentionally mirror the old CP ConfigSchema keys.
    internal sealed class ModConfig
    {
        public bool EnableSeasonalVariants { get; set; } = true;

        // One of: Vanilla, Blue, DarkBlue, DarkBrown, DarkRoof, Green, LightBrown, Pink, Red, WhiteRoof
        // Matches the folder name under Assets/Images/{Saturated|Desaturated}/.
        public string Appearance { get; set; } = "Vanilla";

        public bool EnableDesaturatedVersion { get; set; } = false;
    }
}