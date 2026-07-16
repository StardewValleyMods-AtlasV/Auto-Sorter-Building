using StardewValley;

namespace AutoSorterBuilding.Sorting
{
    // A drill down attribute extractor for the sign slot 2 selector system
    internal interface ISortKeyExtractor
    {
        // The raw selector item ID this extractor is invoked for (matches a ModConstants.SELECTOR_*_ID
        // and thus the raw string stored in Sign.modData[ModConstants.SELECTOR_SLOT_MODDATA_KEY]).
        string SelectorId { get; }

        // Used only to namespace the dictionary bucket key, e.g. "Colour:red". Matches
        // SelectorItems' TypeLabel for the same selector so the two can't drift apart.
        string TypeLabel { get; }

        // Returns the extracted attribute value for this item, or null if the item doesn't have
        // this attribute (or it can't be determined). Null is the single "extraction failed" signal
        // that callers (ItemSorter) use to fall through to the next priority level.
        string? ExtractKey(Item item);
    }
}