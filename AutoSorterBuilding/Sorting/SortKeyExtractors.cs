using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using StardewValley;
using StardewModdingAPI;

namespace AutoSorterBuilding.Sorting
{
    // Flavour is keyed off Object.GetPreservedItemId(), which wraps preservedParentSheetIndex and
    // correctly special-cases Wild Honey's "-1" by returning null (so Wild Honey falls
    // through to Colour/Quality/ModID/Category instead of forming a "Flavour:-1" bucket).
    internal sealed class FlavourSortKeyExtractor : ISortKeyExtractor
    {
        public string SelectorId => ModConstants.SELECTOR_FLAVOUR_ID;
        public string TypeLabel => "Flavour";

        public string? ExtractKey(Item item)
        {
            if (item is not StardewValley.Object obj) return null;

            string? preservedId = obj.GetPreservedItemId();
            return string.IsNullOrEmpty(preservedId) ? null : preservedId;
        }
    }

    // Colour is keyed off "color_*" context tags. Multiple matches sort alphabetically by the full
    // tag (so "color_blue" < "color_red"), first one wins, Trace-logged when there's more than one
    // so this is diagnosable if an item's colour bucket ever looks surprising.
    internal sealed class ColourSortKeyExtractor : ISortKeyExtractor
    {
        private const string TagPrefix = "color_";

        public string SelectorId => ModConstants.SELECTOR_COLOUR_ID;
        public string TypeLabel => "Colour";

        public string? ExtractKey(Item item)
        {
            HashSet<string> tags = item.GetContextTags();

            List<string>? colourTags = null;
            foreach (string tag in tags)
            {
                if (tag.StartsWith(TagPrefix, StringComparison.Ordinal))
                {
                    (colourTags ??= new List<string>()).Add(tag);
                }
            }

            if (colourTags is null) return null;

            if (colourTags.Count > 1)
            {
                colourTags.Sort(StringComparer.Ordinal);
                ModEntry.ModMonitor.Log(
                    $"Item {item.QualifiedItemId} has {colourTags.Count} color_* tags; using \"{colourTags[0]}\" alphabetically.",
                    LogLevel.Trace);
            }

            return colourTags[0].Substring(TagPrefix.Length);
        }
    }

    // Quality is a direct switch on Item.Quality. Quality 0 ("no quality") is deliberately treated
    // as an extraction miss (null), not a valid "None" bucket - most items default to Quality 0
    // (including virtually all weapons/tools), so if it counted as a real value, a single Quality
    // selector sign showing a base-quality slot-1 item would register a "Quality:None" bucket that
    // hijacks nearly every plain item in the building ahead of the Category fallback. Treating it as
    // a miss instead means Quality-0 items just fall through to ModID/Category like any other
    // extraction failure, and only Silver/Gold/Iridium items can actually land in a Quality bucket.
    // The switch's final null case (3, unused in vanilla) falls through the same way.
    internal sealed class QualitySortKeyExtractor : ISortKeyExtractor
    {
        public string SelectorId => ModConstants.SELECTOR_QUALITY_ID;
        public string TypeLabel => "Quality";

        public string? ExtractKey(Item item)
        {
            return item.Quality switch
            {
                1 => "Silver",
                2 => "Gold",
                4 => "Iridium",
                _ => null // 0 (no quality) and 3 (unused in vanilla) both fall through
            };
        }
    }

    // ModID is inferred from an author-prefixed item ID convention (e.g.
    // "AtlasV.AutoSorterBuilding_Selector_ModID" -> "AtlasV.AutoSorterBuilding"): everything before
    // the first period, the period itself, and the run of letters/digits directly after it, stopping
    // at the first non-alphanumeric character (including underscore, which most mod authors use as
    // their own separator after the ModID segment). This is a convention, not a requirement, so any
    // item ID that doesn't match (including all vanilla numeric IDs) is a normal extraction miss,
    // falling through to Category.
    //
    // Possiblity of looking into tracing what mod adds stuff through content pipeline, questionable 
    // but not impossible, proven by asset trace mod by chu
    internal sealed class ModIdSortKeyExtractor : ISortKeyExtractor
    {
        // Anchored at the start, deliberately only letters/digits count as "word" characters here,
        // so a hyphen or underscore anywhere in the first two segments stops the match rather than
        // being swallowed into the ModID - not confirmed against real-world mod ID variety, easy to
        // relax later if a legitimate ModID convention using those characters turns up.
        private static readonly Regex ModIdPattern = new(@"^([A-Za-z0-9]+)\.([A-Za-z0-9]+)");

        public string SelectorId => ModConstants.SELECTOR_MODID_ID;
        public string TypeLabel => "ModID";

        public string? ExtractKey(Item item)
        {
            Match match = ModIdPattern.Match(item.ItemId);
            return match.Success ? $"{match.Groups[1].Value}.{match.Groups[2].Value}" : null;
        }
    }
}