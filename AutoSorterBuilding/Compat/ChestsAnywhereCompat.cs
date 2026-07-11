using System.Collections.Generic;
using System.Linq;
using StardewValley.Objects;

namespace AutoSorterBuilding.Compat
{
    internal static class ChestsAnywhereCompat
    {
        // Set once in ModEntry.Entry() based on whether Chests Anywhere is installed.
        public static bool IsLoaded = false;

        // Chests Anywhere reads/writes its custom chest name from/to it's modData key directly, it's a
        // plain data field, only ever set if it's currently empty, if it already holds any non-empty value
        // assume player named (or alike).
        // To get the mod to rename, just empty out the name field in the chest
        public static void UpdateChestName(Chest chest, string name)
        {
            if (chest.modData.TryGetValue(ModConstants.CHESTS_ANYWHERE_NAME_MODDATA_KEY, out string? existingName) &&
                !string.IsNullOrWhiteSpace(existingName))
            {
                return;
            }

            chest.modData[ModConstants.CHESTS_ANYWHERE_NAME_MODDATA_KEY] = name;
        }

        // Numbers and names every signed chest in the building in english reading order: left to right, top to bottom
        public static void NameChestsInReadingOrder(List<(Chest Chest, string Category)> chestsToName)
        {
            var orderedChests = chestsToName
                .OrderBy(entry => entry.Chest.TileLocation.Y)
                .ThenBy(entry => entry.Chest.TileLocation.X);

            int index = 1;
            foreach (var (chest, category) in orderedChests)
            {
                UpdateChestName(chest, $"{index}. {category}");
                index++;
            }
        }
    }
}
