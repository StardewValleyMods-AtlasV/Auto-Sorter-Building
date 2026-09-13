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

        public static void Register(Harmony harmony, IModHelper helper)
        {
            harmony.Patch(
                original: AccessTools.Method(typeof(Sign), nameof(Sign.checkForAction)),
                prefix: new HarmonyMethod(typeof(SignSelectorPatch), nameof(CheckForActionPrefix))
            );

            harmony.Patch(
                original: AccessTools.Method(typeof(Sign), nameof(Sign.draw),
                    new[] { typeof(SpriteBatch), typeof(int), typeof(int), typeof(float) }),
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

        private static void DrawPostfix(Sign __instance, SpriteBatch spriteBatch, int x, int y, float alpha)
        {
            float layerDepth = Math.Max(0f, (float)((y + 1) * 64 - 24) / 10000f) +
                (float)x * 1E-05f + 3E-05f;

            int additionalCount = SignFilterStorage.GetAdditionalClauseCount(__instance);
            if (additionalCount > 0)
            {
                DrawCyclingFilterSample(__instance, spriteBatch, x, y, alpha, layerDepth, additionalCount);
            }

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
                const float textScale = 0.65f;
                string text = $"+{additionalCount}";
                Vector2 textPosition = Game1.GlobalToLocal(Game1.viewport, new Vector2(x * 64 + 4, y * 64 - 53));
                spriteBatch.DrawString(
                    Game1.smallFont,
                    text,
                    textPosition + new Vector2(2f, 2f),
                    Color.Black * alpha,
                    0f,
                    Vector2.Zero,
                    textScale,
                    SpriteEffects.None,
                    layerDepth);
                spriteBatch.DrawString(
                    Game1.smallFont,
                    text,
                    textPosition,
                    Color.White * alpha,
                    0f,
                    Vector2.Zero,
                    textScale,
                    SpriteEffects.None,
                    layerDepth + 0.00001f);
            }
        }

        private static void DrawCyclingFilterSample(
            Sign sign,
            SpriteBatch spriteBatch,
            int x,
            int y,
            float alpha,
            float layerDepth,
            int additionalCount)
        {
            // Index zero leaves the primary item drawn by the game visible. Later indices overlay
            // one Shift-added sample for two seconds each before returning to the primary item.
            int cycleIndex = (int)((Game1.ticks / FilterCycleIntervalTicks) % (uint)(additionalCount + 1));
            if (cycleIndex == 0)
            {
                return;
            }

            string? sampleItemId = SignFilterStorage.GetAdditionalSampleItemId(sign, cycleIndex - 1);
            if (sampleItemId is null)
            {
                return;
            }

            if (!FilterSampleCache.TryGetValue(sampleItemId, out Item? sampleItem))
            {
                sampleItem = ItemRegistry.Create(sampleItemId, allowNull: true);
                FilterSampleCache[sampleItemId] = sampleItem;
            }

            if (sampleItem is null)
            {
                return;
            }

            Vector2 itemPosition = Game1.GlobalToLocal(
                Game1.viewport,
                new Vector2(x * 64 + 8, y * 64 - 56));

            // The backing plate hides the primary icon beneath transparent parts of the cycling icon.
            spriteBatch.Draw(
                Game1.staminaRect,
                new Rectangle((int)itemPosition.X, (int)itemPosition.Y, 48, 48),
                null,
                Color.Black * 0.72f * alpha,
                0f,
                Vector2.Zero,
                SpriteEffects.None,
                layerDepth);
            sampleItem.drawInMenu(
                spriteBatch,
                itemPosition,
                0.75f,
                alpha,
                layerDepth + 0.000002f,
                StackDrawType.Hide,
                Color.White,
                drawShadow: false);
        }
    }
}
