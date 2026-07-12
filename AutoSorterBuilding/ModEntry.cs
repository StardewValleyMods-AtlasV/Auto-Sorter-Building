using HarmonyLib;
using StardewModdingAPI;
using AutoSorterBuilding.Compat;
using AutoSorterBuilding.Events;
using AutoSorterBuilding.Patches;
using AutoSorterBuilding.Content;
using AutoSorterBuilding.Config;

namespace AutoSorterBuilding
{
    internal sealed class ModEntry : Mod
    {
        internal static IMonitor ModMonitor { get; private set; } = null!;
        internal static Harmony Harmony { get; private set; } = null!;
        internal static ITranslationHelper Translation { get; private set; } = null!;

        public override void Entry(IModHelper helper)
        {
            ModMonitor = Monitor;
            Translation = helper.Translation;
            Harmony = new Harmony(ModManifest.UniqueID);

            // Just a modData read/write, so no Harmony needed
            // it's just checked once here and used to gate naming later.
            ChestsAnywhereCompat.IsLoaded = helper.ModRegistry.IsLoaded(ModConstants.CHESTS_ANYWHERE_MOD_ID);

            BuildingChestActionPatch.Register(Harmony);
            ItemConversionPatch.Register(Harmony);
            AutomateCompat.Register(Harmony, helper, Monitor);

            TimeChangedHandler.Register(helper);
            BuildingContentLoader.Register(helper);
            GMCMIntegration.Register(helper, ModManifest);
        }
    }
}