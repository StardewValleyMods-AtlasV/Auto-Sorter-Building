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
