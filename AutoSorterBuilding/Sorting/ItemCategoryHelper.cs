using System.Text.RegularExpressions;
using StardewValley;

namespace AutoSorterBuilding.Sorting
{
    internal static class ItemCategoryHelper
    {
        // Ordered list of locale specific RegEx for the "Level {0} {1}" style category strings
        // MeleeWeapon.getCategoryName() produces.
        // Each regex is anchored to where the digit placeholder actually falls for that language's word
        // order, since English's "Level first" order is not universal. English is checked last, as a
        // fallback, since it's the format we expect if none of the other locales match.
        private static readonly (string Locale, Regex Pattern)[] LeveledWeaponCategoryPatterns =
        {
            ("es-ES", new Regex(@"- Nivel \d+ ")),        // "{1} - Nivel {0} "
            ("de-DE", new Regex(@", Stufe \d+$")),        // "{1}, Stufe {0}"
            ("ru-RU", new Regex(@" \d+-го уровня$")),     // "{1} {0}-го уровня"
            ("hu-HU", new Regex(@"^\d+\. szintű ")),      // "{0}. szintű {1}"
            ("ja-JP", new Regex(@"レベル\d+の")),           // "レベル{0}の{1}"
            ("it-IT", new Regex(@" Livello \d+$")),       // "{1} Livello {0}"
            ("pt-BR", new Regex(@" Nível \d+$")),         // "{1} Nível {0}"
            ("ko-KR", new Regex(@"^레벨 \d+ ")),           // "레벨 {0} {1}"
            ("zh-CN", new Regex(@"^\d+ 级")),              // "{0} 级{1}"
            ("tr-TR", new Regex(@"^\d+\. Seviye ")),      // "{0}. Seviye {1}"
            ("fr-FR", new Regex(@"^Niveau \d+ ")),        // "Niveau {0} {1}"
            ("en-US", new Regex(@"^Level \d+ ")),         // "Level {0} {1}" — fallback, checked last
        };

        /* This function is just a little helper function so we don't have to repeat the same code
         in several different places. Both SpaceCore and Calcifer patch Object.getCategoryName() to
         return any overridden categories, so those will be supported by this helper. However, if
         a category is not overridden, but does not have a string representation of its category,
         then getCategoryName() would return an empty string. In that case, we want to fall back
         to using the item's category number \converted to a string as its category identifier. */
        public static string GetItemCategory(Item item)
        {
            string categoryName = item.getCategoryName();
            if (string.IsNullOrWhiteSpace(categoryName))
            {
                return item.Category.ToString();
            }

            // If this category name matches any of the known "Level N Weapon" style formats
            // (in any officially supported language), strip every digit character out of it for sorting.
            // So "Level 23 Sword" and "Level 47 Sword" both
            // sort into a "Level  Sword" bucket instead of being treated as separate categories.
            foreach (var (_, pattern) in LeveledWeaponCategoryPatterns)
            {
                if (pattern.IsMatch(categoryName))
                {
                    string stripped = pattern.Replace(categoryName, "");
                    return Regex.Replace(stripped, @"\s{2,}", " ").Trim();
                }
            }

            return categoryName;
        }
    }
}
