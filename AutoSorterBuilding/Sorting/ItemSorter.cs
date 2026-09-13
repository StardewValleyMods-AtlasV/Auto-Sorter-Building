using System;
using System.Collections.Generic;
using System.Linq;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Objects;
using AutoSorterBuilding.Compat;
using AutoSorterBuilding.Config;

namespace AutoSorterBuilding.Sorting
{
    internal static class ItemSorter
    {
        // Every category in one clause must match (AND). A chest may contain several clauses,
        // created from the sign's primary item and its saved alternatives, and any clause may
        // match (OR).
        private sealed class CategoryClause
        {
            public IReadOnlyList<ResolvedItemCategory> Categories { get; }

            public CategoryClause(IReadOnlyList<ResolvedItemCategory> categories)
            {
                Categories = categories;
            }
        }

        private sealed class SelectorClause
        {
            public ISortKeyExtractor Extractor { get; }
            public string Value { get; }

            public SelectorClause(ISortKeyExtractor extractor, string value)
            {
                Extractor = extractor;
                Value = value;
            }
        }

        private sealed class ChestFilter
        {
            // Keep all routing information together per chest. This prevents the same chest from
            // being represented in several category buckets and accidentally tried more than once.
            public Chest Chest { get; }
            public List<CategoryClause> CategoryClauses { get; } = new();
            public List<SelectorClause> SelectorClauses { get; } = new();
            public List<string> DisplayLabels { get; } = new();
            public bool IsCatchAll { get; set; }

            public ChestFilter(Chest chest)
            {
                Chest = chest;
            }
        }

        public static void SortItems(Building building, bool isManualSort = false)
        {
            ModEntry.ModMonitor.Log($"Sorting items into AutoSorter with name {building.GetIndoorsName()}");

            // Resolve signs once per sorting pass rather than searching the building again for
            // every item placed in the input chest.
            List<ChestFilter> filters = CollectChestFilters(building, isManualSort);
            if (filters.Count == 0)
            {
                return;
            }

            Chest inputChest = building.GetBuildingChest(ModConstants.INPUT_CHEST_ID);

            // Work from a snapshot and temporarily empty the input chest. Any stack which can't be
            // completely placed is added back below, so a failed or partial match never loses items.
            List<Item> itemsToSort = inputChest.GetItemsForPlayer().ToList();
            inputChest.GetItemsForPlayer().Clear();

            var leftoverItems = new List<Item>();
            foreach (Item item in itemsToSort)
            {
                List<Chest> destinations = ResolveDestinations(filters, item);
                Item? remaining = item;

                foreach (Chest chest in destinations)
                {
                    // A chest may have become locked after filters were collected, particularly in
                    // multiplayer, so check its mutex again immediately before changing its items.
                    if (chest.GetMutex().IsLocked())
                    {
                        continue;
                    }

                    remaining = chest.addItem(remaining);
                    if (remaining is null)
                    {
                        break;
                    }
                }

                if (remaining is not null)
                {
                    leftoverItems.Add(remaining);
                }
            }

            inputChest.GetItemsForPlayer().AddRange(leftoverItems);
        }

        private static List<Chest> ResolveDestinations(IReadOnlyList<ChestFilter> filters, Item item)
        {
            // Catch-all destinations are appended only after all specific matches. CombineDistinct
            // below also ensures a catch-all chest with saved clauses is not attempted twice.
            List<ChestFilter> catchAllFilters = filters.Where(filter => filter.IsCatchAll).ToList();

            // Once a selector type has a matching chest, lower-priority selectors and categories aren't used.
            foreach (ISortKeyExtractor extractor in SortKeyExtractorRegistry.GetPriorityOrder(GMCMIntegration.Config))
            {
                string? value = extractor.ExtractKey(item);
                if (value is null)
                {
                    continue;
                }

                List<ChestFilter> selectorMatches = filters
                    .Where(filter => filter.SelectorClauses.Any(clause =>
                        clause.Extractor.SelectorId == extractor.SelectorId &&
                        string.Equals(clause.Value, value, StringComparison.Ordinal)))
                    .ToList();

                if (selectorMatches.Count > 0)
                {
                    return CombineDistinct(selectorMatches, catchAllFilters);
                }
            }

            IReadOnlyList<ResolvedItemCategory> itemCategories = ItemCategoryRegistry.GetCategories(item);
            var itemCategoryKeys = new HashSet<string>(
                itemCategories.Select(category => category.Key),
                StringComparer.Ordinal);

            // A clause matches through set containment: the incoming item must contain every
            // category required by that clause. If several clauses on one chest match, only its
            // largest matching clause determines that chest's specificity.
            // OrderByDescending is stable, so ties retain the reading order established during collection.
            List<ChestFilter> categoryMatches = filters
                .Select(filter => new
                {
                    Filter = filter,
                    Specificity = filter.CategoryClauses
                        .Where(clause => clause.Categories.All(category => itemCategoryKeys.Contains(category.Key)))
                        .Select(clause => clause.Categories.Count)
                        .DefaultIfEmpty(0)
                        .Max()
                })
                .Where(match => match.Specificity > 0)
                .OrderByDescending(match => match.Specificity)
                .Select(match => match.Filter)
                .ToList();

            return CombineDistinct(categoryMatches, catchAllFilters);
        }

        private static List<Chest> CombineDistinct(
            IEnumerable<ChestFilter> specificFilters,
            IEnumerable<ChestFilter> catchAllFilters)
        {
            var destinations = new List<Chest>();

            // Reference equality is intentional: two different placed chests may contain identical
            // data, while repeated matches for the same placed Chest object must collapse to one try.
            var seen = new HashSet<Chest>(ReferenceEqualityComparer.Instance);

            foreach (ChestFilter filter in specificFilters.Concat(catchAllFilters))
            {
                if (seen.Add(filter.Chest))
                {
                    destinations.Add(filter.Chest);
                }
            }

            return destinations;
        }

        private static List<ChestFilter> CollectChestFilters(Building building, bool updateChestNames)
        {
            GameLocation interior = building.GetIndoors();
            var chests = new List<Chest>();

            // Utility.ForEachItemIn doesn't promise tile order, so first gather every currently
            // available chest and sort the result explicitly below.
            Utility.ForEachItemIn(interior, item =>
            {
                if (item is Chest chest && !chest.GetMutex().IsLocked())
                {
                    chests.Add(chest);
                }
                return true;
            });

            chests = chests
                // Stable top-to-bottom, then left-to-right order controls overflow between equally
                // specific destinations and optional Chests Anywhere numbering.
                .OrderBy(chest => chest.TileLocation.Y)
                .ThenBy(chest => chest.TileLocation.X)
                .ToList();

            var filters = new List<ChestFilter>();
            List<(Chest Chest, string Category)>? chestsToName = updateChestNames && ChestsAnywhereCompat.IsLoaded
                ? new List<(Chest, string)>()
                : null;

            foreach (Chest chest in chests)
            {
                int x = (int)chest.TileLocation.X;
                int y = (int)chest.TileLocation.Y - 1;

                // Only the sign directly above a chest configures it. Additional OR clauses live
                // in that sign's modData instead of consuming more tiles with stacked signs.
                if (interior.getObjectAtTile(x, y) is not Sign sign)
                {
                    continue;
                }

                var filter = new ChestFilter(chest);
                AddSignClause(filter, sign);
                filters.Add(filter);
                chestsToName?.Add((chest, string.Join(" OR ", filter.DisplayLabels)));
            }

            if (chestsToName is not null)
            {
                ChestsAnywhereCompat.NameChestsInReadingOrder(chestsToName);
            }

            return filters;
        }

        private static void AddSignClause(ChestFilter filter, Sign sign)
        {
            // An empty primary sign marks the chest as catch-all. Stored clauses are still read so
            // old or externally edited sign data can't hide valid specific destinations.
            if (sign.displayItem.Value is not { } displayedItem)
            {
                filter.IsCatchAll = true;
                filter.DisplayLabels.Add(ModEntry.Translation.Get("uncategorized-chest-name"));
                AddStoredClauses(filter, sign);
                return;
            }

            if (sign.modData.TryGetValue(ModConstants.SELECTOR_SLOT_MODDATA_KEY, out string? selectorId) &&
                SortKeyExtractorRegistry.TryGetExtractor(selectorId, out ISortKeyExtractor? extractor))
            {
                // A valid selector changes the primary item from a category sample into a selector
                // sample. If extraction fails, ordinary category behavior remains the safe fallback.
                string? value = extractor!.ExtractKey(displayedItem);
                if (value is not null)
                {
                    filter.SelectorClauses.Add(new SelectorClause(extractor, value));
                    filter.DisplayLabels.Add(extractor.GetDisplayLabel(value));
                    AddStoredClauses(filter, sign);
                    return;
                }
            }

            IReadOnlyList<ResolvedItemCategory> categories = ItemCategoryRegistry.GetCategories(displayedItem);
            if (categories.Count == 1 && categories[0].IsCatchAll)
            {
                filter.IsCatchAll = true;
                filter.DisplayLabels.Add(ModEntry.Translation.Get("uncategorized-chest-name"));
                AddStoredClauses(filter, sign);
                return;
            }

            if (categories.Count > 0)
            {
                filter.CategoryClauses.Add(new CategoryClause(categories));
                filter.DisplayLabels.Add(string.Join(" + ", categories.Select(category => category.DisplayName)));
            }

            AddStoredClauses(filter, sign);
        }

        private static void AddStoredClauses(ChestFilter filter, Sign sign)
        {
            // Each Shift-added sample is a separate OR alternative. Its categories are stored as a
            // single clause so the AND relationship within that sample remains intact.
            foreach (IReadOnlyList<ResolvedItemCategory> categories in SignFilterStorage.GetAdditionalClauses(sign))
            {
                if (categories.Count == 1 && categories[0].IsCatchAll)
                {
                    filter.IsCatchAll = true;
                    filter.DisplayLabels.Add(ModEntry.Translation.Get("uncategorized-chest-name"));
                }
                else
                {
                    filter.CategoryClauses.Add(new CategoryClause(categories));
                    filter.DisplayLabels.Add(string.Join(" + ", categories.Select(category => category.DisplayName)));
                }
            }
        }

        public static IEnumerable<Building> FindAutoSorterBuildings()
        {
            // Shared by timed sorting and sign interaction checks so both features agree on which
            // locations are Auto-Sorter interiors.
            foreach (GameLocation location in Game1.locations)
            {
                foreach (Building building in location.buildings)
                {
                    if (ModConstants.IsAutoSorterBuildingType(building.buildingType.Value))
                    {
                        yield return building;
                    }
                }
            }
        }
    }
}
