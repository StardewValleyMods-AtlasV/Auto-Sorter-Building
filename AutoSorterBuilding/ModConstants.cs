namespace AutoSorterBuilding
{
    // Shared IDs/keys used across the mod. Centralized here so every file references the same
    // values instead of redefining string literals.
    internal static class ModConstants
    {
        // var defs to make things easier
        public const string CP_UNIQUE_ID = "AtlasV.AutoSorterBuilding";
        public const string BUILDING_ID = $"{CP_UNIQUE_ID}_AutoSorterBuilding";
        public const string INPUT_CHEST_ID = $"{CP_UNIQUE_ID}_InputChest";

        // Upgrade tier building IDs. Each is registered as its own Data/Buildings entry with
        // BuildingToUpgrade pointing at the previous tier, Small -> Medium -> Large.
        public const string MEDIUM_BUILDING_ID = $"{BUILDING_ID}_Medium";
        public const string LARGE_BUILDING_ID = $"{BUILDING_ID}_Large";

        // empty category ID used for signs that are empty as catch all
        public const string EMPTY_CATEGORY_ID = $"{CP_UNIQUE_ID}_EmptyCategory";

        // Chests Anywhere's unique mod ID and the modData key it reads/writes for a chest's custom name.
        // Chests Anywhere stores its per-chest options in modData rather than the vanilla Name field
        public const string CHESTS_ANYWHERE_MOD_ID = "Pathoschild.ChestsAnywhere";
        public const string CHESTS_ANYWHERE_NAME_MODDATA_KEY = "Pathoschild.ChestsAnywhere/Name";

        // Automate's unique mod ID, used to gate the compat patch on whether it's installed.
        public const string AUTOMATE_MOD_ID = "Pathoschild.Automate";

        // Sign slot-2 "selector" item IDs.
        public const string SELECTOR_COLOUR_ID = $"{CP_UNIQUE_ID}_Selector_Colour";
        public const string SELECTOR_QUALITY_ID = $"{CP_UNIQUE_ID}_Selector_Quality";
        public const string SELECTOR_FLAVOUR_ID = $"{CP_UNIQUE_ID}_Selector_Flavour";
        public const string SELECTOR_MODID_ID = $"{CP_UNIQUE_ID}_Selector_ModID";
        public const string SELECTOR_GEODE_ID = $"{CP_UNIQUE_ID}_Selector_Geode";

        // Asset name prefix under which selector item textures are registered via AssetRequested,
        // e.g. "Mods/AtlasV.AutoSorterBuilding/SelectorItems/Colour".
        public const string SELECTOR_TEXTURE_ASSET_PREFIX = $"Mods/{CP_UNIQUE_ID}/SelectorItems";
        
        // modData key on a Sign instance holding the raw selector item ID assigned to slot 2 (e.g.
        // "AtlasV.AutoSorterBuilding_Selector_Colour"). Presence of this key is also used as the sole
        // signal in draw() that a Sign belongs to an AutoSorterBuilding, since draw() has no location
        // parameter to check against the interior cache directly.
        public const string SELECTOR_SLOT_MODDATA_KEY = $"{CP_UNIQUE_ID}_SelectorSlot";
        
        // All building tier IDs, used anywhere a check like "is this an AutoSorterBuilding, regardless
        // of which upgrade tier it currently is" is needed. When a 4th tier is ever added, this is the
        // only place that needs updating for that check to apply everywhere.
        public static readonly System.Collections.Generic.HashSet<string> ALL_BUILDING_IDS = new()
        {
            BUILDING_ID,
            MEDIUM_BUILDING_ID,
            LARGE_BUILDING_ID
        };

        public static bool IsAutoSorterBuildingType(string? buildingType) =>
            buildingType is not null && ALL_BUILDING_IDS.Contains(buildingType);
    }
}