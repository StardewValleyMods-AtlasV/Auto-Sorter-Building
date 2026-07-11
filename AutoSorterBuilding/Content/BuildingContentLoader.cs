using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley.GameData.Buildings;

namespace AutoSorterBuilding.Content
{
    // Registers the building's Data/Buildings entry, its exterior texture, and its interior map
    // through content pipeline, replaces content pack
    //
    // TODO: Config-driven variants (season, appearance, desaturation, interior size)
    // TODO: Helper.GameContent.InvalidateCache(...) calls when the config changes.
    internal static class BuildingContentLoader
    {
        // Asset name for the interior map. Building.cs prefixes IndoorMap with "Maps\\" itself,
        // so the raw BuildingData.IndoorMap value is the bare name
        // but the asset actually provided via AssetRequested must be the full "Maps/..." name for some fucking reason
        private static readonly string MapAssetName = $"Maps/{ModConstants.BUILDING_ID}";

        public static void Register(IModHelper helper)
        {
            helper.Events.Content.AssetRequested += OnAssetRequested;
        }

        private static void OnAssetRequested(object sender, AssetRequestedEventArgs e)
        {
            if (e.Name.IsEquivalentTo("Data/Buildings"))
            {
                e.Edit(asset =>
                {
                    var data = asset.AsDictionary<string, BuildingData>().Data;
                    data[ModConstants.BUILDING_ID] = BuildBuildingData();
                });
                return;
            }

            if (e.Name.IsEquivalentTo(ModConstants.BUILDING_ID))
            {
                e.LoadFromModFile<Microsoft.Xna.Framework.Graphics.Texture2D>(
                    "Assets/Images/Saturated/Vanilla/AutoSorterBuilding_spring.png",
                    AssetLoadPriority.Medium);
                return;
            }

            if (e.Name.IsEquivalentTo(MapAssetName))
            {
                e.LoadFromModFile<xTile.Map>(
                    "Assets/Maps/SmallAutoSorterBuilding.tmx",
                    AssetLoadPriority.Medium);
            }
        }

        private static BuildingData BuildBuildingData()
        {
            return new BuildingData
            {
                Name = ModEntry.Translation.Get("Building.Name"),
                Description = ModEntry.Translation.Get("Building.Description"),
                Texture = ModConstants.BUILDING_ID,
                Builder = "Robin",
                BuildCost = 5000,
                BuildMaterials = new List<BuildingMaterial>
                {
                    new BuildingMaterial { ItemId = "388", Amount = 100 }, // Wood
                    new BuildingMaterial { ItemId = "335", Amount = 5 },   // Iron Bar
                    new BuildingMaterial { ItemId = "787", Amount = 2 }    // Battery
                },
                BuildDays = 3,
                BuildMenuDrawOffset = new Point(0, -96),
                Size = new Point(7, 3),
                HumanDoor = new Point(3, 2),
                AllowsFlooringUnderneath = true,
                DrawLayers = new List<BuildingDrawLayer>(),
                IndoorMap = ModConstants.BUILDING_ID,
                IndoorMapType = "StardewValley.Shed",
                Chests = new List<BuildingChest>
                {
                    new BuildingChest
                    {
                        Id = ModConstants.INPUT_CHEST_ID,
                        Type = BuildingChestType.Chest,
                        Sound = "Ship",
                        ChestFullMessage = ModEntry.Translation.Get("Building.InputFull"),
                        DisplayTile = new Vector2(5, 2),
                        DisplayHeight = 1.5f
                    }
                },
                ActionTiles = new List<BuildingActionTile>
                {
                    new BuildingActionTile
                    {
                        Id = $"{ModConstants.BUILDING_ID}_OpenInputChest",
                        Tile = new Point(5, 2),
                        Action = $"BuildingChest {ModConstants.INPUT_CHEST_ID}"
                    }
                },
                ItemConversions = new List<BuildingItemConversion>
                {
                    new BuildingItemConversion
                    {
                        Id = "AcceptAll",
                        RequiredTags = new List<string>(),
                        SourceChest = ModConstants.INPUT_CHEST_ID,
                        MaxDailyConversions = -1
                    }
                }
            };
        }
    }
}