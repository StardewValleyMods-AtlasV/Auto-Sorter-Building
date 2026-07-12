using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.GameData.Buildings;
using AutoSorterBuilding.Config;

namespace AutoSorterBuilding.Content
{
    // Registers the building's Data/Buildings entry, its exterior texture, and its interior map
    // through content pipeline, replaces content pack
    internal static class BuildingContentLoader
    {
        // Asset name for the interior map. Building.cs prefixes IndoorMap with "Maps\\" itself,
        // so the raw BuildingData.IndoorMap value is the bare name
        // but the asset actually provided via AssetRequested must be the full "Maps/..." name for some fucking reason
        private static readonly string MapAssetName = $"Maps/{ModConstants.BUILDING_ID}";

        // Tracks the season the currently loaded texture was built for, so DayStarted only
        // invalidates when the season has actually changed rather than every single day.
        private static string? _lastAppliedSeason;

        public static void Register(IModHelper helper)
        {
            helper.Events.Content.AssetRequested += OnAssetRequested;
            helper.Events.GameLoop.DayStarted += (_, _) => OnDayStarted(helper);
        }

        private static void OnDayStarted(IModHelper helper)
        {
            if (!GMCMIntegration.Config.EnableSeasonalVariants)
                return;

            string currentSeason = Game1.season.ToString();
            if (currentSeason == _lastAppliedSeason)
                return;

            _lastAppliedSeason = currentSeason;
            InvalidateTexture(helper);
        }

        // Called from GMCMIntegration on field-change (live preview, might not want cause dark screen) and on save
        public static void InvalidateTexture(IModHelper helper)
        {
            helper.GameContent.InvalidateCache(ModConstants.BUILDING_ID);
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
                    GetTextureAssetPath(),
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

        // Resolves the current config into a concrete file path under Assets/Images.
        // {Saturated|Desaturated}/{Appearance}/AutoSorterBuilding_{season}.png
        private static string GetTextureAssetPath()
        {
            var config = GMCMIntegration.Config;

            string saturation = config.EnableDesaturatedVersion ? "Desaturated" : "Saturated";
            string appearance = config.Appearance;
            // default to the summer variant.
            string season = config.EnableSeasonalVariants
                ? Game1.season.ToString().ToLowerInvariant()
                : "summer";

            return $"Assets/Images/{saturation}/{appearance}/AutoSorterBuilding_{season}.png";
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