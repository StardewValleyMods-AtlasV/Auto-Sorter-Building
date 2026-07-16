using System;
using System.Collections.Generic;
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

            // Not a selector item -> vanilla slot-1 behaviour, unchanged.
            if (!SelectorItems.TryGetTypeLabel(currentItem.ItemId, out string? typeLabel)) return true;

            // Selectors only ever target slot 2. Slot 1 empty -> fall through so the documented
            // "empty slot 1 -> set slot 1" behaviour still applies even when holding a selector.
            if (__instance.displayItem.Value is null) return true;

            // who.currentLocation - used throughout decompiled game code but its
            // declaration wasn't found in source.
            GameLocation? location = who.currentLocation;
            if (location is null || !IsAutoSorterInterior(location))
            {
                ModEntry.ModMonitor.Log(
                    $"Selector item {currentItem.ItemId} used on a Sign outside an AutoSorterBuilding interior (or location lookup failed) - falling back to vanilla Sign behaviour.",
                    LogLevel.Trace);
                return true;
            }

            // Overwrite is intentional, re-interacting with a selector in hand always replaces
            // whatever was in slot 2, same as vanilla slot 1 behaviour.
            __instance.modData[ModConstants.SELECTOR_SLOT_MODDATA_KEY] = currentItem.ItemId;
            Game1.playSound("coin");
            __result = true;
            return false;
        }

        private static void DrawPostfix(Sign __instance, SpriteBatch spriteBatch, int x, int y, float alpha)
        {
            // Presence of this modData key is the sole "belongs to building" signal,
            // since draw() has no location parameter to check against the interior cache, only
            // this prefix above ever writes this key (or at least should).
            if (!__instance.modData.TryGetValue(ModConstants.SELECTOR_SLOT_MODDATA_KEY, out string? selectorId)) return;
            if (!SelectorItems.TryGetTypeLabel(selectorId, out string? typeLabel)) return;

            // Bottom-right corner of the sign tile, drawn at 1.75x scale (16x16 source -> 32x32 on screen).
            // The sign's own art is drawn one tile above y (see base draw()'s y*64-64 offsets),
            // so "bottom right" here means the bottom-right of that same tile:
            // x*64 + 59 - badge width, y*64 - badge height - vertical offset.
            const float scale = 1.75f;
            const int sourceSize = 16; // selector textures are 16x16 per SelectorItems.cs
            const int verticalOffset = 22;
            float badgePixelSize = sourceSize * scale;

            Texture2D badgeTexture = Game1.content.Load<Texture2D>($"Mods/{ModConstants.CP_UNIQUE_ID}/SelectorItems/{typeLabel}");
            Vector2 position = Game1.GlobalToLocal(Game1.viewport, new Vector2(
                x * 64 + 59 - badgePixelSize,
                y * 64 - badgePixelSize + verticalOffset
            ));
            spriteBatch.Draw(
                badgeTexture,
                position,
                null,
                Color.White * alpha,
                0f,
                Vector2.Zero,
                scale,
                SpriteEffects.None,
                Math.Max(0f, (float)((y + 1) * 64 - 24) / 10000f) + (float)x * 1E-05f + 3E-05f
            );
        }
    }
}