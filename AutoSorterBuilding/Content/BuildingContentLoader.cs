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

        // Stored so ComposeTexture (called later by SMAPI, inside the deferred e.LoadFrom
        // delegate, not from this call stack) can load the layer source files.
        private static IModHelper? _helper;

        public static void Register(IModHelper helper)
        {
            _helper = helper;
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
                e.LoadFrom(() => ComposeTexture(BuildingTier.Small), AssetLoadPriority.Medium);
                return;
            }

            if (e.Name.IsEquivalentTo(ModConstants.MEDIUM_BUILDING_ID))
            {
                e.LoadFrom(() => ComposeTexture(BuildingTier.Medium), AssetLoadPriority.Medium);
                return;
            }

            if (e.Name.IsEquivalentTo(ModConstants.LARGE_BUILDING_ID))
            {
                e.LoadFrom(() => ComposeTexture(BuildingTier.Large), AssetLoadPriority.Medium);
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

        // Composites the three source layers into a single flattened texture:
        //   Base_{Colour}_{Saturated|Desaturated}.png (bottom) -> Overlay_{Tier}.png -> Overlay_{Season}.png (top)
        // SMAPI itself caches the returned Texture2D under the tier's asset name until
        // InvalidateTexture() is called (day-change or config save), so only one composited texture
        // per tier asset name (three total: Small/Medium/Large)
        //
        // all three source layers, across colour/tier/season combination, share identical pixel dimensions,
        // single width/height read from the base layer is safe to reuse for all three GetData calls.
        // A mismatched file will throw a GetData size-mismatch exception rather than silently
        // misaligning layers(fail loud, so I actually notice).
        private static Microsoft.Xna.Framework.Graphics.Texture2D ComposeTexture(BuildingTier tier)
        {
            var config = GMCMIntegration.Config;

            string colour = config.Appearance;
            string saturation = config.EnableDesaturatedVersion ? "Desaturated" : "Saturated";
            string season = config.EnableSeasonalVariants
                ? Game1.season.ToString()
                : "Summer"; // Summer overlay is the intentionally-blank/transparent variant.
            string tierName = tier.ToString(); // "Small" | "Medium" | "Large"

            var baseTex = _helper!.ModContent.Load<Microsoft.Xna.Framework.Graphics.Texture2D>(
                $"Assets/Images/Base_{colour}_{saturation}.png");
            var tierOverlay = _helper.ModContent.Load<Microsoft.Xna.Framework.Graphics.Texture2D>(
                $"Assets/Images/Overlay_{tierName}.png");
            var seasonOverlay = _helper.ModContent.Load<Microsoft.Xna.Framework.Graphics.Texture2D>(
                $"Assets/Images/Overlay_{season}.png");

            int width = baseTex.Width;
            int height = baseTex.Height;
            int pixelCount = width * height;

            var basePixels = new Color[pixelCount];
            var tierPixels = new Color[pixelCount];
            var seasonPixels = new Color[pixelCount];
            baseTex.GetData(basePixels);
            tierOverlay.GetData(tierPixels);
            seasonOverlay.GetData(seasonPixels);

            var result = new Color[pixelCount];
            for (int i = 0; i < pixelCount; i++)
            {
                Color composited = AlphaOver(basePixels[i], tierPixels[i]);
                result[i] = AlphaOver(composited, seasonPixels[i]);
            }

            var composedTexture = new Microsoft.Xna.Framework.Graphics.Texture2D(
                Game1.graphics.GraphicsDevice, width, height);
            composedTexture.SetData(result);
            return composedTexture;
        }

        // Standard "src over dst" alpha compositing
        private static Color AlphaOver(Color dst, Color src)
        {
            if (src.A == 0)
            {
                return dst;
            }
            if (src.A == 255)
            {
                return src;
            }

            float srcA = src.A / 255f;
            float dstA = dst.A / 255f;
            float outA = srcA + dstA * (1f - srcA);
            if (outA <= 0f)
            {
                return Color.Transparent;
            }

            float r = (src.R * srcA + dst.R * dstA * (1f - srcA)) / outA;
            float g = (src.G * srcA + dst.G * dstA * (1f - srcA)) / outA;
            float b = (src.B * srcA + dst.B * dstA * (1f - srcA)) / outA;

            return new Color((byte)r, (byte)g, (byte)b, (byte)(outA * 255f));
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