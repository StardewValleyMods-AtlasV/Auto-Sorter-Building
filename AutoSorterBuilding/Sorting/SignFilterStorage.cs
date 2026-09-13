using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using StardewModdingAPI;
using StardewValley.Objects;

namespace AutoSorterBuilding.Sorting
{
    internal static class SignFilterStorage
    {
        // These private DTOs are the save format written into Sign.modData. They deliberately contain
        // only plain values so System.Text.Json can deserialize them without any game object state.
        private sealed class StoredCategory
        {
            public string Key { get; set; } = "";
            public string DisplayName { get; set; } = "";
            public bool IsCatchAll { get; set; }

            public StoredCategory() { }
        }

        private sealed class StoredClause
        {
            // Categories are saved as a snapshot of the sample's filter. The item ID is retained
            // separately only so SignSelectorPatch can draw that sample while cycling the sign.
            public List<StoredCategory> Categories { get; set; } = new();
            public string SampleItemId { get; set; } = "";

            public StoredClause() { }
        }

        private sealed class CacheEntry
        {
            public string Json { get; set; } = "";
            public List<StoredClause> Clauses { get; set; } = new();
        }

        // Signs are recreated when a save is loaded. A ConditionalWeakTable lets old cache entries
        // disappear with their Sign instances instead of retaining game objects for the whole session.
        private static readonly ConditionalWeakTable<Sign, CacheEntry> Cache = new();

        public static IReadOnlyList<IReadOnlyList<ResolvedItemCategory>> GetAdditionalClauses(Sign sign)
        {
            List<StoredClause> storedClauses = Read(sign);
            var clauses = new List<IReadOnlyList<ResolvedItemCategory>>();

            foreach (StoredClause storedClause in storedClauses)
            {
                // Be defensive about manually edited or older modData: discard blank keys and
                // duplicate categories rather than allowing malformed clauses into the sorter.
                IReadOnlyList<ResolvedItemCategory> categories = storedClause.Categories
                    .Where(category => !string.IsNullOrWhiteSpace(category.Key))
                    .GroupBy(category => category.Key, StringComparer.Ordinal)
                    .Select(group => group.First())
                    .Select(category => new ResolvedItemCategory(
                        category.Key,
                        string.IsNullOrWhiteSpace(category.DisplayName) ? category.Key : category.DisplayName,
                        category.IsCatchAll))
                    .ToList();

                if (categories.Count > 0)
                {
                    clauses.Add(categories);
                }
            }

            return clauses;
        }

        // Returns true when a clause was added and false when an existing clause was removed.
        public static bool ToggleAdditionalClause(
            Sign sign,
            IReadOnlyList<ResolvedItemCategory> categories,
            string sampleItemId)
        {
            List<StoredClause> storedClauses = Read(sign);

            // Compare category sets instead of list order. The same AND clause should toggle off
            // even if category discovery returned its members in a different order.
            HashSet<string> categoryKeys = categories
                .Select(category => category.Key)
                .ToHashSet(StringComparer.Ordinal);

            int existingIndex = storedClauses.FindIndex(clause =>
            {
                HashSet<string> storedKeys = clause.Categories
                    .Select(category => category.Key)
                    .ToHashSet(StringComparer.Ordinal);
                return storedKeys.SetEquals(categoryKeys);
            });

            bool added;
            if (existingIndex >= 0)
            {
                storedClauses.RemoveAt(existingIndex);
                added = false;
            }
            else
            {
                storedClauses.Add(new StoredClause
                {
                    SampleItemId = sampleItemId,
                    Categories = categories.Select(category => new StoredCategory
                    {
                        Key = category.Key,
                        DisplayName = category.DisplayName,
                        IsCatchAll = category.IsCatchAll
                    }).ToList()
                });
                added = true;
            }

            if (storedClauses.Count == 0)
            {
                // Removing the keys entirely keeps ordinary signs indistinguishable from signs
                // which have never used the additional-filter feature.
                sign.modData.Remove(ModConstants.ADDITIONAL_FILTERS_MODDATA_KEY);
                sign.modData.Remove(ModConstants.ADDITIONAL_FILTER_COUNT_MODDATA_KEY);
            }
            else
            {
                sign.modData[ModConstants.ADDITIONAL_FILTERS_MODDATA_KEY] = JsonSerializer.Serialize(storedClauses);

                // Drawing only needs the number for its +N badge. Mirror the count separately so
                // every rendered frame doesn't need to deserialize the full clause list.
                sign.modData[ModConstants.ADDITIONAL_FILTER_COUNT_MODDATA_KEY] = storedClauses.Count.ToString();
            }

            return added;
        }

        public static int GetAdditionalClauseCount(Sign sign)
        {
            return sign.modData.TryGetValue(ModConstants.ADDITIONAL_FILTER_COUNT_MODDATA_KEY, out string? countText) &&
                int.TryParse(countText, out int count)
                    ? Math.Max(0, count)
                    : 0;
        }

        public static string? GetAdditionalSampleItemId(Sign sign, int index)
        {
            List<StoredClause> clauses = Read(sign);
            if (index < 0 || index >= clauses.Count)
            {
                return null;
            }

            string itemId = clauses[index].SampleItemId;
            return string.IsNullOrWhiteSpace(itemId) ? null : itemId;
        }

        private static List<StoredClause> Read(Sign sign)
        {
            if (!sign.modData.TryGetValue(ModConstants.ADDITIONAL_FILTERS_MODDATA_KEY, out string? json) ||
                string.IsNullOrWhiteSpace(json))
            {
                return new List<StoredClause>();
            }

            try
            {
                CacheEntry cache = Cache.GetOrCreateValue(sign);

                // Comparing the source JSON keeps the cache correct if another screen or mod changes
                // this sign's modData without going through ToggleAdditionalClause.
                if (string.Equals(cache.Json, json, StringComparison.Ordinal))
                {
                    return cache.Clauses;
                }

                cache.Json = json;
                cache.Clauses = JsonSerializer.Deserialize<List<StoredClause>>(json) ?? new List<StoredClause>();
                return cache.Clauses;
            }
            catch (Exception ex)
            {
                // Invalid save data must never stop an entire building from sorting. Cache an empty
                // result for this exact JSON as well, which avoids repeating the warning every pass.
                ModEntry.ModMonitor.Log(
                    $"Couldn't read additional filters from a sign; the invalid data will be ignored.\n{ex}",
                    LogLevel.Warn);
                CacheEntry cache = Cache.GetOrCreateValue(sign);
                cache.Json = json;
                cache.Clauses = new List<StoredClause>();
                return new List<StoredClause>();
            }
        }
    }
}
