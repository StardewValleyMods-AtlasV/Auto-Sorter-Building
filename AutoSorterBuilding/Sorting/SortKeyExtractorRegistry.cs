using System.Collections.Generic;
using System.Linq;
using AutoSorterBuilding.Config;

namespace AutoSorterBuilding.Sorting
{
    // Central registry for the five ISortKeyExtractor implementations. All extractors are
    // stateless, so this is populated once via static initialization rather than an Entry()-time
    // registration step.
    //
    // _defaultOrder is the historical hardcoded order (Geode, Flavour, Colour, Quality, ModID).
    // It now serves two purposes: (1) the tie-break sequence when GetPriorityOrder(config) sees
    // two extractors configured with the same numeric rank, and (2) the fallback used by
    // PriorityOrder for any caller not yet updated to pass a ModConfig.
    // Category itself isn't in this list, it's handled as the unconditional final fallback
    // directly in ItemSorter, and is not configurable.
    internal static class SortKeyExtractorRegistry
    {
        private static readonly ISortKeyExtractor[] _defaultOrder =
        {
            new GeodeSortKeyExtractor(),
            new FlavourSortKeyExtractor(),
            new ColourSortKeyExtractor(),
            new QualitySortKeyExtractor(),
            new ModIdSortKeyExtractor(),
        };

        private static readonly Dictionary<string, ISortKeyExtractor> _bySelectorId =
            _defaultOrder.ToDictionary(e => e.SelectorId);

        // Used by ItemSorter.CollectChests to resolve a sign's slot-2 modData value to an
        // extractor for building that sign's bucket key. Selector-to-extractor lookup is
        // unaffected by priority order, so this stays as-is.
        public static bool TryGetExtractor(string selectorId, out ISortKeyExtractor? extractor) =>
            _bySelectorId.TryGetValue(selectorId, out extractor);

        // Used by ItemSorter.SortItems to walk candidate bucket keys for an incoming item, in
        // the user-configured priority order. Ties (two extractors sharing the same rank, e.g.
        // from a hand-edited config file) are broken by _defaultOrder position, so a config with
        // all-default ranks reproduces previous hardcoded order exactly.
        public static IReadOnlyList<ISortKeyExtractor> GetPriorityOrder(ModConfig config)
        {
            return _defaultOrder
                .Select((extractor, defaultIndex) => (
                    extractor,
                    rank: GetConfiguredRank(extractor.SelectorId, config),
                    defaultIndex))
                .OrderBy(x => x.rank)
                .ThenBy(x => x.defaultIndex)
                .Select(x => x.extractor)
                .ToArray();
        }

        private static int GetConfiguredRank(string selectorId, ModConfig config) => selectorId switch
        {
            ModConstants.SELECTOR_GEODE_ID => config.GeodePriority,
            ModConstants.SELECTOR_FLAVOUR_ID => config.FlavourPriority,
            ModConstants.SELECTOR_COLOUR_ID => config.ColourPriority,
            ModConstants.SELECTOR_QUALITY_ID => config.QualityPriority,
            ModConstants.SELECTOR_MODID_ID => config.ModIdPriority,
            _ => int.MaxValue, // unrecognized selector ID, sort last rather than throw, for once, not failing loudly
        };
    }
}