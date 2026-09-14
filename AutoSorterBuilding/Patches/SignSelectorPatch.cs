using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Objects;
using AutoSorterBuilding.Content;
using AutoSorterBuilding.Sorting;

namespace AutoSorterBuilding.Patches
{
    internal static class SignSelectorPatch
    {
        // Cache of interior GameLocations belonging to AutoSorterBuildings. checkForAction is
        // interaction-rate so a search would be fine there, but draw() is frame-rate for every
        // visible sign, so both share this cache to keep the cost class consistent. Rebuilt every
        // in-game 10-minute tick (same cadence as TimeChangedHandler's sort pass), so a sign placed
        // inside a just-built AutoSorterBuilding may fall through to vanilla Sign behaviour for up
        // to 10 in-game minutes if interacted with immediately, not expected to be reachable in normal play. 
        // Falls back to vanilla and logs at Trace so this is diagnosable if it ever does come up.
        // Nulled on SaveLoaded/ReturnedToTitle since GameLocation instances aren't guaranteed
        // stable across a save load
        private static HashSet<GameLocation>? _autoSorterInteriors;
        private static readonly Dictionary<string, Item?> FilterSampleCache = new(StringComparer.Ordinal);
        private const uint FilterCycleIntervalTicks = 120;

        // Pixel offsets for the cycling OR-filter sample icon, relative to the sign's tile origin
        private const int FilterSampleOffsetX = 0;

        // (x*64, y*64).
        private const int FilterSampleOffsetY = -42;
        private const float FilterSampleScale = 0.75f;

        // Pixel offset for the "+N" additional-filter-count badge as a whole, relative to the
        // sign's tile origin (x*64, y*64).
        private const int FilterCountBadgeOffsetX = 8;
        private const int FilterCountBadgeOffsetY = -35;
        private const float FilterCountBadgePlusScale = 0.5f;
        private const float FilterCountBadgeNumberScale = 1f;
        private const float FilterCountBadgeNumberOffsetY = -10f;

        // Bundles the state a DrawPrefix needs to hand off to DrawPostfix for a given Sign.draw()
        // call.
         private sealed class DrawSwapState
        {
            public Item OriginalItem { get; }
            public Item SampleItem { get; }

            public DrawSwapState(Item originalItem, Item sampleItem)
            {
                OriginalItem = originalItem;
                SampleItem = sampleItem;
            }
        }

        public static void Register(Harmony harmony, IModHelper helper)
        {
            harmony.Patch(
                original: AccessTools.Method(typeof(Sign), nameof(Sign.checkForAction)),
                prefix: new HarmonyMethod(typeof(SignSelectorPatch), nameof(CheckForActionPrefix))
            );

            harmony.Patch(
                original: AccessTools.Method(typeof(Sign), nameof(Sign.draw),
                    new[] { typeof(SpriteBatch), typeof(int), typeof(int), typeof(float) }),
                prefix: new HarmonyMethod(typeof(SignSelectorPatch), nameof(DrawPrefix)),
                postfix: new HarmonyMethod(typeof(SignSelectorPatch), nameof(DrawPostfix))
            );

            helper.Events.GameLoop.TimeChanged += (_, _) => _autoSorterInteriors = BuildInteriorCache();
            helper.Events.GameLoop.SaveLoaded += (_, _) => _autoSorterInteriors = null;
            helper.Events.GameLoop.ReturnedToTitle += (_, _) => _autoSorterInteriors = null;
        }

        private static HashSet<GameLocation> BuildInteriorCache()
        {
            var interiors = new HashSet<GameLocation>();
            foreach (Building building in ItemSorter.FindAutoSorterBuildings())
            {
                GameLocation? interior = building.GetIndoors();
                if (interior is not null)
                {
                    interiors.Add(interior);
                }
            }
            return interiors;
        }

        private static bool IsAutoSorterInterior(GameLocation location)
        {
            // Lazy build so don't wait for the first TimeChanged tick after Entry().
            _autoSorterInteriors ??= BuildInteriorCache();
            return _autoSorterInteriors.Contains(location);
        }

        private static bool CheckForActionPrefix(Sign __instance, Farmer who, bool justCheckingForActivity, ref bool __result)
        {
            if (justCheckingForActivity) return true;

            Item? currentItem = who.CurrentItem;
            if (currentItem is null) return true;

            // Custom interactions require an existing primary item. An empty sign retains vanilla
            // behavior so the held item becomes that primary item first.
            if (__instance.displayItem.Value is null) return true;

            GameLocation? location = who.currentLocation;
            if (location is null || !IsAutoSorterInterior(location)) return true;

            if (SelectorItems.TryGetTypeLabel(currentItem.ItemId, out _))
            {
                // Re-interacting with a selector replaces slot 2, matching vanilla slot-1 behavior.
                __instance.modData[ModConstants.SELECTOR_SLOT_MODDATA_KEY] = currentItem.ItemId;
                Game1.playSound("coin");
                __result = true;
                return false;
            }

            if (IsAdditionalFilterModifierDown())
            {
                IReadOnlyList<ResolvedItemCategory> categories = ItemCategoryRegistry.GetCategories(currentItem);
                IReadOnlyList<ResolvedItemCategory> primaryCategories =
                    ItemCategoryRegistry.GetCategories(__instance.displayItem.Value);

                if (HaveSameCategoryKeys(categories, primaryCategories))
                {
                    Game1.playSound("cancel");
                    Game1.addHUDMessage(new HUDMessage(ModEntry.Translation.Get("filter.primary-exists")));
                }
                else
                {
                    bool added = SignFilterStorage.ToggleAdditionalClause(
                        __instance,
                        categories,
                        currentItem.QualifiedItemId);
                    string label = string.Join(" + ", categories.Select(category => category.DisplayName));
                    Game1.playSound(added ? "coin" : "trashcan");
                    Game1.addHUDMessage(new HUDMessage(ModEntry.Translation.Get(
                        added ? "filter.added" : "filter.removed",
                        new { filter = label })));
                }

                __result = true;
                return false;
            }

            return true;
        }

        private static bool IsAdditionalFilterModifierDown()
        {
            return ModEntry.Input.IsDown(SButton.LeftShift) || ModEntry.Input.IsDown(SButton.RightShift);
        }

        private static bool HaveSameCategoryKeys(
            IReadOnlyList<ResolvedItemCategory> left,
            IReadOnlyList<ResolvedItemCategory> right)
        {
            var leftKeys = left.Select(category => category.Key).ToHashSet(StringComparer.Ordinal);
            return leftKeys.SetEquals(right.Select(category => category.Key));
        }

        // Decides, once per Sign.draw() call, whether this frame should show a cycled OR-filter
        // sample instead of the real primary item. If so, the primary item is temporarily hidden
        // (displayItem.Value = null) so vanilla's own drawing draws nothing for it.
         private static void DrawPrefix(Sign __instance, out DrawSwapState? __state)
        {
            __state = null;

            int additionalCount = SignFilterStorage.GetAdditionalClauseCount(__instance);
            if (additionalCount <= 0)
            {
                return;
            }

            Item? primaryItem = __instance.displayItem.Value;
            if (primaryItem is null)
            {
                return;
            }

            // Index zero is the primary item itself, let vanilla draw it normally.
            int cycleIndex = (int)((Game1.ticks / FilterCycleIntervalTicks) % (uint)(additionalCount + 1));
            if (cycleIndex == 0)
            {
                return;
            }

            string? sampleItemId = SignFilterStorage.GetAdditionalSampleItemId(__instance, cycleIndex - 1);
            if (sampleItemId is null)
            {
                return;
            }

            Item? sampleItem = GetCachedSampleItem(sampleItemId);
            if (sampleItem is null)
            {
                return;
            }

            __instance.displayItem.Value = null;
            __state = new DrawSwapState(primaryItem, sampleItem);
        }

        private static void DrawPostfix(
            Sign __instance,
            SpriteBatch spriteBatch,
            int x,
            int y,
            float alpha,
            DrawSwapState? __state)
        {
            float layerDepth = Math.Max(0f, (float)((y + 1) * 64 - 24) / 10000f) +
                (float)x * 1E-05f + 3E-05f;

            if (__state is not null)
            {
                // Vanilla has now run (and drew nothing, since we hid the primary item above).
                // Restore the real item so save data / other mods see it as normal, then draw the
                // sample in its place,a swap, not an overlay.
                __instance.displayItem.Value = __state.OriginalItem;
                DrawFilterSample(__state.SampleItem, spriteBatch, x, y, alpha, layerDepth);
            }

            int additionalCount = SignFilterStorage.GetAdditionalClauseCount(__instance);

            if (__instance.modData.TryGetValue(ModConstants.SELECTOR_SLOT_MODDATA_KEY, out string? selectorId) &&
                SelectorItems.TryGetTypeLabel(selectorId, out string? typeLabel))
            {
                const float selectorScale = 1.75f;
                const int sourceSize = 16;
                const int verticalOffset = 22;
                float badgePixelSize = sourceSize * selectorScale;
                Texture2D badgeTexture = Game1.content.Load<Texture2D>(
                    $"Mods/{ModConstants.CP_UNIQUE_ID}/SelectorItems/{typeLabel}");
                Vector2 selectorPosition = Game1.GlobalToLocal(Game1.viewport, new Vector2(
                    x * 64 + 59 - badgePixelSize,
                    y * 64 - badgePixelSize + verticalOffset));

                spriteBatch.Draw(
                    badgeTexture,
                    selectorPosition,
                    null,
                    Color.White * alpha,
                    0f,
                    Vector2.Zero,
                    selectorScale,
                    SpriteEffects.None,
                    layerDepth);
            }

            if (additionalCount > 0)
            {
                Vector2 badgePosition = Game1.GlobalToLocal(Game1.viewport, new Vector2(
                    x * 64 + FilterCountBadgeOffsetX,
                    y * 64 + FilterCountBadgeOffsetY));
                DrawFilterCountBadge(spriteBatch, additionalCount, badgePosition, alpha, layerDepth);
            }
        }

        // Draws "+" and the count as two independently-scaled strings that still move together as
        // one badge: the number starts right after however wide the "+" ends up at its own scale.
        private static void DrawFilterCountBadge(
            SpriteBatch spriteBatch,
            int additionalCount,
            Vector2 badgePosition,
            float alpha,
            float layerDepth)
        {
            const string plusText = "+";
            string numberText = additionalCount.ToString();

            float plusWidth = Game1.tinyFont.MeasureString(plusText).X * FilterCountBadgePlusScale;
            Vector2 numberPosition = badgePosition + new Vector2(plusWidth, FilterCountBadgeNumberOffsetY);

            DrawOutlinedString(spriteBatch, plusText, badgePosition, FilterCountBadgePlusScale, alpha, layerDepth);
            DrawOutlinedString(spriteBatch, numberText, numberPosition, FilterCountBadgeNumberScale, alpha, layerDepth + 0.00001f);
        }

        private static void DrawOutlinedString(
            SpriteBatch spriteBatch,
            string text,
            Vector2 position,
            float scale,
            float alpha,
            float layerDepth)
        {
            spriteBatch.DrawString(
                Game1.tinyFont,
                text,
                position + new Vector2(2f, 2f),
                Color.Black * alpha,
                0f,
                Vector2.Zero,
                scale,
                SpriteEffects.None,
                layerDepth);
            spriteBatch.DrawString(
                Game1.tinyFont,
                text,
                position,
                Color.White * alpha,
                0f,
                Vector2.Zero,
                scale,
                SpriteEffects.None,
                layerDepth + 0.00001f);
        }

        private static Item? GetCachedSampleItem(string sampleItemId)
        {
            if (!FilterSampleCache.TryGetValue(sampleItemId, out Item? sampleItem))
            {
                sampleItem = ItemRegistry.Create(sampleItemId, allowNull: true);
                FilterSampleCache[sampleItemId] = sampleItem;
            }
            return sampleItem;
        }

        private static void DrawFilterSample(
            Item sampleItem,
            SpriteBatch spriteBatch,
            int x,
            int y,
            float alpha,
            float layerDepth)
        {
            Vector2 itemPosition = Game1.GlobalToLocal(Game1.viewport, new Vector2(
                x * 64 + FilterSampleOffsetX,
                y * 64 + FilterSampleOffsetY));

            sampleItem.drawInMenu(
                spriteBatch,
                itemPosition,
                FilterSampleScale,
                alpha,
                layerDepth + 0.000002f,
                StackDrawType.Hide,
                Color.White,
                drawShadow: false);
        }
    }
}