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
        // Asset names for each tier's interior map. Building.cs prefixes IndoorMap with "Maps\\"
        // itself, so the raw BuildingData.IndoorMap value is the bare name, but the asset actually
        // provided via AssetRequested must be the full "Maps/..." name for some fucking reason
        private static readonly string SmallMapAssetName = $"Maps/{ModConstants.BUILDING_ID}";
        private static readonly string MediumMapAssetName = $"Maps/{ModConstants.MEDIUM_BUILDING_ID}";
        private static readonly string LargeMapAssetName = $"Maps/{ModConstants.LARGE_BUILDING_ID}";

        // Upgrade tier, used to pick the right map/texture file and resolve config-driven texture paths.
        private enum BuildingTier { Small, Medium, Large }

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

        // Called from GMCMIntegration on field-change (live preview, might not want cause dark screen) and on save.
        // Invalidates all three tiers since a placed building may currently be at any of them.
        public static void InvalidateTexture(IModHelper helper)
        {
            helper.GameContent.InvalidateCache(ModConstants.BUILDING_ID);
            helper.GameContent.InvalidateCache(ModConstants.MEDIUM_BUILDING_ID);
            helper.GameContent.InvalidateCache(ModConstants.LARGE_BUILDING_ID);
        }

        private static void OnAssetRequested(object sender, AssetRequestedEventArgs e)
        {
            if (e.Name.IsEquivalentTo("Data/Buildings"))
            {
                e.Edit(asset =>
                {
                    var data = asset.AsDictionary<string, BuildingData>().Data;

                    data[ModConstants.BUILDING_ID] = BuildBuildingData(
                        buildingId: ModConstants.BUILDING_ID,
                        indoorMapId: ModConstants.BUILDING_ID,
                        nameKey: "Building.Name",
                        descriptionKey: "Building.Description",
                        buildCost: 5000,
                        buildMaterials: new List<BuildingMaterial>
                        {
                            new BuildingMaterial { ItemId = "388", Amount = 100 }, // Wood
                            new BuildingMaterial { ItemId = "335", Amount = 5 },   // Iron Bar
                            new BuildingMaterial { ItemId = "787", Amount = 2 }    // Battery
                        },
                        buildingToUpgrade: null);

                    data[ModConstants.MEDIUM_BUILDING_ID] = BuildBuildingData(
                        buildingId: ModConstants.MEDIUM_BUILDING_ID,
                        indoorMapId: ModConstants.MEDIUM_BUILDING_ID,
                        // TODO: add "Building.Name.Medium" / "Building.Description.Medium" entries to the translation files
                        nameKey: "Building.Name.Medium",
                        descriptionKey: "Building.Description.Medium",
                        buildCost: 10000,
                        buildMaterials: new List<BuildingMaterial>
                        {
                            new BuildingMaterial { ItemId = "388", Amount = 200 }, // Wood
                            new BuildingMaterial { ItemId = "390", Amount = 200 }, // Stone
                            new BuildingMaterial { ItemId = "336", Amount = 5 }    // Gold Bar
                        },
                        buildingToUpgrade: ModConstants.BUILDING_ID);

                    data[ModConstants.LARGE_BUILDING_ID] = BuildBuildingData(
                        buildingId: ModConstants.LARGE_BUILDING_ID,
                        indoorMapId: ModConstants.LARGE_BUILDING_ID,
                        // TODO: add "Building.Name.Large" / "Building.Description.Large" entries to the translation files
                        nameKey: "Building.Name.Large",
                        descriptionKey: "Building.Description.Large",
                        buildCost: 15000,
                        buildMaterials: new List<BuildingMaterial>
                        {
                            new BuildingMaterial { ItemId = "709", Amount = 50 }, // Hardwood
                            new BuildingMaterial { ItemId = "337", Amount = 5 },  // Iridium bar
                            new BuildingMaterial { ItemId = "74", Amount = 1 }    // Prismatic shard
                        },
                        buildingToUpgrade: ModConstants.MEDIUM_BUILDING_ID);
                });
                return;
            }

            if (e.Name.IsEquivalentTo(ModConstants.BUILDING_ID))
            {
                e.LoadFromModFile<Microsoft.Xna.Framework.Graphics.Texture2D>(
                    GetTextureAssetPath(BuildingTier.Small),
                    AssetLoadPriority.Medium);
                return;
            }

            if (e.Name.IsEquivalentTo(ModConstants.MEDIUM_BUILDING_ID))
            {
                e.LoadFromModFile<Microsoft.Xna.Framework.Graphics.Texture2D>(
                    GetTextureAssetPath(BuildingTier.Medium),
                    AssetLoadPriority.Medium);
                return;
            }

            if (e.Name.IsEquivalentTo(ModConstants.LARGE_BUILDING_ID))
            {
                e.LoadFromModFile<Microsoft.Xna.Framework.Graphics.Texture2D>(
                    GetTextureAssetPath(BuildingTier.Large),
                    AssetLoadPriority.Medium);
                return;
            }

            if (e.Name.IsEquivalentTo(SmallMapAssetName))
            {
                e.LoadFromModFile<xTile.Map>(
                    "Assets/Maps/SmallAutoSorterBuilding.tmx",
                    AssetLoadPriority.Medium);
                return;
            }

            if (e.Name.IsEquivalentTo(MediumMapAssetName))
            {
                e.LoadFromModFile<xTile.Map>(
                    "Assets/Maps/MediumAutoSorterBuilding.tmx",
                    AssetLoadPriority.Medium);
                return;
            }

            if (e.Name.IsEquivalentTo(LargeMapAssetName))
            {
                e.LoadFromModFile<xTile.Map>(
                    "Assets/Maps/LargeAutoSorterBuilding.tmx",
                    AssetLoadPriority.Medium);
            }
        }

        // Resolves the current config into a concrete file path under Assets/Images.
        // Small:  {Saturated|Desaturated}/{Appearance}/AutoSorterBuilding_{season}.png
        // Medium: {Saturated|Desaturated}/{Appearance}/AutoSorterBuilding_Medium_{season}.png
        // Large:  {Saturated|Desaturated}/{Appearance}/AutoSorterBuilding_Large_{season}.png
        private static string GetTextureAssetPath(BuildingTier tier)
        {
            var config = GMCMIntegration.Config;

            string saturation = config.EnableDesaturatedVersion ? "Desaturated" : "Saturated";
            string appearance = config.Appearance;
            // default to the summer variant.
            string season = config.EnableSeasonalVariants
                ? Game1.season.ToString().ToLowerInvariant()
                : "summer";

            string sizeInfix = tier switch
            {
                BuildingTier.Medium => "Medium_",
                BuildingTier.Large => "Large_",
                _ => string.Empty
            };

            return $"Assets/Images/{saturation}/{appearance}/AutoSorterBuilding_{sizeInfix}{season}.png";
        }

        // Shared builder for all three tiers. Exterior geometry (Size, HumanDoor, BuildMenuDrawOffset),
        // the input chest, its action tile, and ItemConversions are identical across tiers
        // only the identity/upgrade-chain fields, cost, and indoor map differ.
        private static BuildingData BuildBuildingData(
            string buildingId,
            string indoorMapId,
            string nameKey,
            string descriptionKey,
            int buildCost,
            List<BuildingMaterial> buildMaterials,
            string? buildingToUpgrade)
        {
            return new BuildingData
            {
                Name = ModEntry.Translation.Get(nameKey),
                Description = ModEntry.Translation.Get(descriptionKey),
                Texture = buildingId,
                Builder = "Robin",
                BuildCost = buildCost,
                BuildMaterials = buildMaterials,
                BuildingToUpgrade = buildingToUpgrade,
                BuildDays = 3,
                BuildMenuDrawOffset = new Point(0, -96),
                Size = new Point(7, 3),
                HumanDoor = new Point(3, 2),
                AllowsFlooringUnderneath = true,
                DrawLayers = new List<BuildingDrawLayer>(),
                IndoorMap = indoorMapId,
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