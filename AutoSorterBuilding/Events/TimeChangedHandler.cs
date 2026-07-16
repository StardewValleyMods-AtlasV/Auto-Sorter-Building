using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Objects;
using AutoSorterBuilding.Sorting;

namespace AutoSorterBuilding.Events
{
    internal static class TimeChangedHandler
    {
        public static void Register(IModHelper helper)
        {
            helper.Events.GameLoop.TimeChanged += OnTimeChanged;
        }

        /* Runs every 10 in-game minutes. We only let the main player (host in multiplayer) trigger this
         so that all connected clients don't each independently try to sort the same buildings at the same
         time, which could cause item duplication or other multiplayer weirdness. */
        private static void OnTimeChanged(object? sender, TimeChangedEventArgs e)
        {
            if (!Context.IsMainPlayer) return;

            foreach (Building building in ItemSorter.FindAutoSorterBuildings())
            {
                Chest inputChest = building.GetBuildingChest(ModConstants.INPUT_CHEST_ID);
                if (inputChest.GetMutex().IsLocked())
                {
                    ModEntry.ModMonitor.Log($"Sorting items prevented in {building.GetIndoorsName()} due to mutex lock");
                    continue;
                }
                ItemSorter.SortItems(building);
            }
        }
    }
}
