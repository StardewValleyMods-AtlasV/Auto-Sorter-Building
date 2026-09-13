using System.Collections.Generic;
using StardewValley;

namespace AutoSorterBuilding.Sorting
{
    // Keep category identity separate from its label and catch-all status. ItemSorter compares Key,
    // while generated chest names use DisplayName, making each value's intended use explicit.
    internal sealed class ResolvedItemCategory
    {
        public string Key { get; }
        public string DisplayName { get; }
        public bool IsCatchAll { get; }

        public ResolvedItemCategory(string key, string displayName, bool isCatchAll = false)
        {
            Key = key;
            DisplayName = displayName;
            IsCatchAll = isCatchAll;
        }
    }

    internal static class ItemCategoryRegistry
    {
        // Prefix internal keys with their source. Stardew currently supplies the only source, but
        // including it in the serialized identity keeps key construction centralized and unambiguous.
        private const string VanillaProviderId = "game";
        private const char KeySeparator = '\u001f';

        public static IReadOnlyList<ResolvedItemCategory> GetCategories(Item item)
        {
            // Stardew's category surface currently resolves to one category. Returning a list here
            // is intentional: the rest of the sorter deals in category clauses and remains agnostic
            // about how many category identities a sample contains.
            string category = ItemCategoryHelper.GetItemCategory(item);
            return new[]
            {
                new ResolvedItemCategory(
                    BuildKey(VanillaProviderId, category),
                    category,
                    category == ModConstants.EMPTY_CATEGORY_ID)
            };
        }

        private static string BuildKey(string providerId, string categoryId)
        {
            return $"{providerId}{KeySeparator}{categoryId}";
        }
    }
}
