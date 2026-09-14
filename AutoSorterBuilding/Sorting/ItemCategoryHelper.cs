using StardewValley;
using StardewValley.Tools;

namespace AutoSorterBuilding.Sorting
{
    internal static class ItemCategoryHelper
    {
        /* This function is just a little helper function so we don't have to repeat the same code
         in several different places. Both SpaceCore and Calcifer patch Object.getCategoryName() to
         return any overridden categories, so those will be supported by this helper for ordinary
         items. However, if a category is not overridden, but does not have a string representation
         of its category, then getCategoryName() would return an empty string. In that case, we want
         to fall back to using the item's category number converted to a string as its category
         identifier. */
        public static string GetItemCategory(Item item)
        {
            // Non scythe MeleeWeapons special case
            if (item is MeleeWeapon weapon && !weapon.isScythe())
            {
                // GetData() returns null only when Data/Weapons has no entry for this
                // item (the "Error Item" path in ReloadData()), in that case type.Value would just
                // be default of 0, an absence of data. Rather than guessing a weapon-family bucket for a broken item,
                // fall back to the same catch-all bucket
                if (weapon.GetData() is null)
                {
                    return ModConstants.EMPTY_CATEGORY_ID;
                }

                return GetWeaponTypeLabel(weapon.type.Value);
            }

            string categoryName = item.getCategoryName();
            if (string.IsNullOrWhiteSpace(categoryName))
            {
                return item.Category.ToString();
            }

            return categoryName;
        }

        // Resolves the underlying game category without calling the virtual getCategoryName(). This
        // is needed when Calcifer replaces that virtual result but the sorter wants to retain both
        // the custom label and the original category as separate members of one AND clause.
        public static string GetVanillaItemCategory(Item item)
        {
            if (item is MeleeWeapon weapon && !weapon.isScythe())
            {
                return weapon.GetData() is null
                    ? ModConstants.EMPTY_CATEGORY_ID
                    : GetWeaponTypeLabel(weapon.type.Value);
            }

            string categoryName = StardewValley.Object.GetCategoryDisplayName(item.Category);
            return string.IsNullOrWhiteSpace(categoryName)
                ? item.Category.ToString()
                : categoryName;
        }

        // Mirrors the exact switch MeleeWeapon.getCategoryName() uses internally, only the weapon family is wanted
        // for the bucket name, using localised names means no need to translate
        private static string GetWeaponTypeLabel(int type)
        {
            return Game1.content.LoadString(type switch
            {
                1 => "Strings\\StringsFromCSFiles:Tool.cs.14304", // Dagger
                2 => "Strings\\StringsFromCSFiles:Tool.cs.14305", // Club
                _ => "Strings\\StringsFromCSFiles:Tool.cs.14306", // Sword
            });
        }
    }
}
