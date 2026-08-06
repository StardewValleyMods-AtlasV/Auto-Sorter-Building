using GenericModConfigMenu;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using AutoSorterBuilding.Content;

namespace AutoSorterBuilding.Config
{
    // Registers the config with Generic Mod Config Menu, if installed.
    // the mod works fine with default config values if GMCM isn't present, and can be edited manually I think.
    internal static class GMCMIntegration
    {
        // Live config instance. Read by BuildingContentLoader when answering AssetRequested.
        internal static ModConfig Config { get; private set; } = new();

        private static readonly string[] AppearanceValues =
        {
            "Vanilla", "Blue", "DarkBlue", "DarkBrown", "DarkRoof",
            "Green", "LightBrown", "Pink", "Red", "WhiteRoof"
        };

        public static void Register(IModHelper helper, IManifest manifest)
        {
            Config = helper.ReadConfig<ModConfig>();
            helper.Events.GameLoop.GameLaunched += (_, _) => OnGameLaunched(helper, manifest);
        }

        private static void OnGameLaunched(IModHelper helper, IManifest manifest)
        {
            var configMenu = helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (configMenu is null)
            {
                ModEntry.ModMonitor.Log("Generic Mod Config Menu not installed; skipping config UI registration.", LogLevel.Trace);
                return;
            }

            configMenu.Register(
                mod: manifest,
                reset: () => Config = new ModConfig(),
                save: () =>
                {
                    helper.WriteConfig(Config);
                    // Explicit invalidate on save
                    BuildingContentLoader.InvalidateTexture(helper);
                }
            );

            configMenu.AddSectionTitle(
                mod: manifest,
                text: () => ModEntry.Translation.Get("Config.Function.SectionTitle")
            );

            configMenu.AddBoolOption(
                mod: manifest,
                getValue: () => Config.EnableChestNumbering,
                setValue: value => Config.EnableChestNumbering = value,
                name: () => ModEntry.Translation.Get("Config.EnableChestNumbering.Name"),
                tooltip: () => ModEntry.Translation.Get("Config.EnableChestNumbering.Tooltip"),
                fieldId: "EnableChestNumbering"
            );

            configMenu.AddSectionTitle(
                mod: manifest,
                text: () => ModEntry.Translation.Get("Config.Appearance.SectionTitle")
            );

            configMenu.AddBoolOption(
                mod: manifest,
                getValue: () => Config.EnableSeasonalVariants,
                setValue: value => Config.EnableSeasonalVariants = value,
                name: () => ModEntry.Translation.Get("Config.EnableSeasonalVariants.Name"),
                tooltip: () => ModEntry.Translation.Get("Config.EnableSeasonalVariants.Tooltip"),
                fieldId: "EnableSeasonalVariants"
            );

            configMenu.AddSectionTitle(
                mod: manifest,
                text: () => ModEntry.Translation.Get("Config.Appearance.SectionTitle")
            );

            configMenu.AddBoolOption(
                mod: manifest,
                getValue: () => Config.EnableSeasonalVariants,
                setValue: value => Config.EnableSeasonalVariants = value,
                name: () => ModEntry.Translation.Get("Config.EnableSeasonalVariants.Name"),
                tooltip: () => ModEntry.Translation.Get("Config.EnableSeasonalVariants.Tooltip"),
                fieldId: "EnableSeasonalVariants"
            );

            configMenu.AddTextOption(
                mod: manifest,
                getValue: () => Config.Appearance,
                setValue: value => Config.Appearance = value,
                name: () => ModEntry.Translation.Get("Config.Appearance.Name"),
                tooltip: () => ModEntry.Translation.Get("Config.Appearance.Tooltip"),
                allowedValues: AppearanceValues,
                fieldId: "Appearance"
            );

            configMenu.AddBoolOption(
                mod: manifest,
                getValue: () => Config.EnableDesaturatedVersion,
                setValue: value => Config.EnableDesaturatedVersion = value,
                name: () => ModEntry.Translation.Get("Config.EnableDesaturatedVersion.Name"),
                tooltip: () => ModEntry.Translation.Get("Config.EnableDesaturatedVersion.Tooltip"),
                fieldId: "EnableDesaturatedVersion"
            );
            
            configMenu.AddSectionTitle(
                mod: manifest,
                text: () => ModEntry.Translation.Get("Config.SortPriority.SectionTitle")
            );

            configMenu.AddParagraph(
                mod: manifest,
                text: () => ModEntry.Translation.Get("Config.SortPriority.Explanation")
            );

            configMenu.AddNumberOption(
                mod: manifest,
                getValue: () => Config.GeodePriority,
                setValue: value => Config.GeodePriority = value,
                name: () => ModEntry.Translation.Get("Config.GeodePriority.Name"),
                tooltip: () => ModEntry.Translation.Get("Config.GeodePriority.Tooltip"),
                min: 1, max: 5, interval: 1,
                fieldId: "GeodePriority"
            );

            configMenu.AddNumberOption(
                mod: manifest,
                getValue: () => Config.FlavourPriority,
                setValue: value => Config.FlavourPriority = value,
                name: () => ModEntry.Translation.Get("Config.FlavourPriority.Name"),
                tooltip: () => ModEntry.Translation.Get("Config.FlavourPriority.Tooltip"),
                min: 1, max: 5, interval: 1,
                fieldId: "FlavourPriority"
            );

            configMenu.AddNumberOption(
                mod: manifest,
                getValue: () => Config.ColourPriority,
                setValue: value => Config.ColourPriority = value,
                name: () => ModEntry.Translation.Get("Config.ColourPriority.Name"),
                tooltip: () => ModEntry.Translation.Get("Config.ColourPriority.Tooltip"),
                min: 1, max: 5, interval: 1,
                fieldId: "ColourPriority"
            );

            configMenu.AddNumberOption(
                mod: manifest,
                getValue: () => Config.QualityPriority,
                setValue: value => Config.QualityPriority = value,
                name: () => ModEntry.Translation.Get("Config.QualityPriority.Name"),
                tooltip: () => ModEntry.Translation.Get("Config.QualityPriority.Tooltip"),
                min: 1, max: 5, interval: 1,
                fieldId: "QualityPriority"
            );

            configMenu.AddNumberOption(
                mod: manifest,
                getValue: () => Config.ModIdPriority,
                setValue: value => Config.ModIdPriority = value,
                name: () => ModEntry.Translation.Get("Config.ModIdPriority.Name"),
                tooltip: () => ModEntry.Translation.Get("Config.ModIdPriority.Tooltip"),
                min: 1, max: 5, interval: 1,
                fieldId: "ModIdPriority"
            );
        }
    }
}