using System.Collections.Generic;
using System.Linq;

namespace AutoSorterBuilding.Sorting
{
    // Central registry for the four ISortKeyExtractor implementations. All extractors are
    // stateless, so this is populated once via static initialization rather than an Entry()-time
    // registration step
    //
    // PriorityOrder is the default, precedence used by ItemSorter when an incoming item could match
    // more than one registered selector bucket in the same building
    // Category itself isn't in this list, it's handled as the unconditional
    // final fallback directly in ItemSorter.
    internal static class SortKeyExtractorRegistry
    {
        private static readonly ISortKeyExtractor[] _priorityOrder =
        {
            new FlavourSortKeyExtractor(),
            new ColourSortKeyExtractor(),
            new QualitySortKeyExtractor(),
            new ModIdSortKeyExtractor(),
        };

        private static readonly Dictionary<string, ISortKeyExtractor> _bySelectorId =
            _priorityOrder.ToDictionary(e => e.SelectorId);

        // Used by ItemSorter.CollectChests to resolve a sign's slot-2 modData value to an
        // extractor for building that sign's bucket key.
        public static IReadOnlyList<ISortKeyExtractor> PriorityOrder => _priorityOrder;

        // Used by ItemSorter.SortItems to walk candidate bucket keys for an incoming item, in
        // default priority order.
        public static bool TryGetExtractor(string selectorId, out ISortKeyExtractor? extractor) =>
            _bySelectorId.TryGetValue(selectorId, out extractor);
    }
}