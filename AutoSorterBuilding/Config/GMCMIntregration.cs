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
        }
    }
}