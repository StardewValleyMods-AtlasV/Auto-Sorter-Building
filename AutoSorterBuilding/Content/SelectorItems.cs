using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley.GameData.Objects;

namespace AutoSorterBuilding.Content
{
    // Registers the four sign-slot-2 "selector" items (Colour, Quality, Flavour, ModID).
    internal static class SelectorItems
    {
        private sealed class SelectorDefinition
        {
            // The Data/Objects + Data/CraftingRecipes key, and the qualified item ID players see.
            public string ItemId;

            // Used to build the texture asset name and the i18n key prefix. Matches the suffix
            // of ItemId exactly (Colour/Quality/Flavour/ModID) so the two can't drift apart.
            public string TypeLabel;

            public string TranslationKeyPrefix;
        }

        private static readonly SelectorDefinition[] Definitions = new[]
        {
            new SelectorDefinition { ItemId = ModConstants.SELECTOR_COLOUR_ID, TypeLabel = "Colour", TranslationKeyPrefix = "selector.colour" },
            new SelectorDefinition { ItemId = ModConstants.SELECTOR_QUALITY_ID, TypeLabel = "Quality", TranslationKeyPrefix = "selector.quality" },
            new SelectorDefinition { ItemId = ModConstants.SELECTOR_FLAVOUR_ID, TypeLabel = "Flavour", TranslationKeyPrefix = "selector.flavour" },
            new SelectorDefinition { ItemId = ModConstants.SELECTOR_MODID_ID, TypeLabel = "ModID", TranslationKeyPrefix = "selector.modid" },
        };

        internal static void Register(IModHelper helper)
        {
            helper.Events.Content.AssetRequested += OnAssetRequested;
        }

        private static void OnAssetRequested(object sender, AssetRequestedEventArgs e)
        {
            // Textures - one 16x16 PNG per selector type, loaded from Assets/Images/Items/.
            foreach (SelectorDefinition def in Definitions)
            {
                if (e.NameWithoutLocale.IsEquivalentTo(GetTextureAssetName(def.TypeLabel)))
                {
                    string typeLabel = def.TypeLabel;
                    e.LoadFromModFile<Texture2D>($"Assets/Images/Items/Selector_{typeLabel}.png", AssetLoadPriority.Medium);
                }
            }

            if (e.NameWithoutLocale.IsEquivalentTo("Data/Objects"))
            {
                e.Edit(EditObjects);
            }
            else if (e.NameWithoutLocale.IsEquivalentTo("Data/CraftingRecipes"))
            {
                e.Edit(EditCraftingRecipes);
            }
        }

        private static string GetTextureAssetName(string typeLabel)
        {
            return $"{ModConstants.SELECTOR_TEXTURE_ASSET_PREFIX}/{typeLabel}";
        }

        private static void EditObjects(IAssetData asset)
        {
            var data = asset.AsDictionary<string, ObjectData>().Data;

            foreach (SelectorDefinition def in Definitions)
            {
                data[def.ItemId] = new ObjectData
                {
                    Name = def.ItemId,
                    DisplayName = ModEntry.Translation.Get($"{def.TranslationKeyPrefix}.name"),
                    Description = ModEntry.Translation.Get($"{def.TranslationKeyPrefix}.description"),
                    Type = "Crafting",
                    Category = -8, // Crafting category
                    Price = 0,
                    Texture = GetTextureAssetName(def.TypeLabel),
                    SpriteIndex = 0,
                    Edibility = -300, // inedible
                    ContextTags = new List<string> { "not_placeable" }
                };
            }
        }

        private static void EditCraftingRecipes(IAssetData asset)
        {
            var data = asset.AsDictionary<string, string>().Data;

            foreach (SelectorDefinition def in Definitions)
            {
                // Format: ingredients/(unused)/output/bigCraftable/unlockConditions/displayName
                // - "388 1" -> 1 Wood
                // - "Field" -> the (unused) second field, must be present but doesn't affect anything
                // - "{id} 1" -> yields 1 of this item
                // - "false" -> not a big craftable
                // - "default" -> recipe learned automatically from game start
                // - trailing displayName left blank -> falls back to the item's own DisplayName I think
                data[def.ItemId] = $"388 1/Field/{def.ItemId} 1/false/default/";
            }
        }
        
        // Resolves a selector item's raw ID back to its TypeLabel (Colour/Quality/Flavour/ModID),
        // used by SignSelectorPatch both to recognize a held selector item and to resolve a stored
        // modData value back to a texture asset name for drawing.
        internal static bool TryGetTypeLabel(string itemId, out string? typeLabel)
        {
            foreach (SelectorDefinition def in Definitions)
            {
                if (def.ItemId == itemId)
                {
                    typeLabel = def.TypeLabel;
                    return true;
                }
            }
            typeLabel = null;
            return false;
        }
    }
}