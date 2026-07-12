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
    }
}