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
        private const string CalciferProviderId = "sophie.Calcifer";
        private const char KeySeparator = '\u001f';

        public static IReadOnlyList<ResolvedItemCategory> GetCategories(Item item)
        {
            string resolvedCategory = ItemCategoryHelper.GetItemCategory(item);
            string? calciferCategory = GetCalciferCategory(item);

            // Calcifer changes the displayed category text but intentionally leaves Item.Category
            // untouched. Treat the documented custom field and the underlying vanilla category as
            // two simultaneous memberships, which forms an AND clause for this sample item.
            if (calciferCategory is not null)
            {
                string vanillaCategory = ItemCategoryHelper.GetVanillaItemCategory(item);
                if (!string.Equals(calciferCategory, vanillaCategory, System.StringComparison.Ordinal))
                {
                    return new[]
                    {
                        new ResolvedItemCategory(
                            BuildKey(CalciferProviderId, calciferCategory),
                            calciferCategory),
                        new ResolvedItemCategory(
                            BuildKey(VanillaProviderId, vanillaCategory),
                            vanillaCategory,
                            vanillaCategory == ModConstants.EMPTY_CATEGORY_ID)
                    };
                }
            }

            // Other category-name overrides, including SpaceCore's, continue through the existing
            // virtual getCategoryName() path as one category.
            return new[]
            {
                new ResolvedItemCategory(
                    BuildKey(VanillaProviderId, resolvedCategory),
                    resolvedCategory,
                    resolvedCategory == ModConstants.EMPTY_CATEGORY_ID)
            };
        }

        private static string? GetCalciferCategory(Item item)
        {
            if (item is not StardewValley.Object obj ||
                !Game1.objectData.TryGetValue(obj.ItemId, out var objectData) ||
                objectData.CustomFields is null ||
                !objectData.CustomFields.TryGetValue(ModConstants.CALCIFER_CATEGORY_CUSTOM_FIELD, out string? category) ||
                string.IsNullOrWhiteSpace(category))
            {
                return null;
            }

            return category.Trim();
        }

        private static string BuildKey(string providerId, string categoryId)
        {
            return $"{providerId}{KeySeparator}{categoryId}";
        }
    }
}
