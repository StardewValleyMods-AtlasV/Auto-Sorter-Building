using System;
using System.Collections;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Inventories;
using StardewValley.Objects;

namespace AutoSorterBuilding.Compat
{
    internal static class AutomateCompat
    {
        // bool so compat warning doesn't repeat every onTimeChanged and spam the log
        private static int _automateCompatWarningLoggedTimeCounter = 0;

        public static void Register(Harmony harmony, IModHelper helper, IMonitor monitor)
        {
            // Load check to make sure things only run when Automate is installed, also patch def and logging saying issues
            if (!helper.ModRegistry.IsLoaded(ModConstants.AUTOMATE_MOD_ID)) return;

            monitor.Log("AutoSorterBuilding patches Automate's DataBasedBuildingMachine.SetInput. If you notice issues with Automate, please check whether they happen without this mod installed (backing up your save file beforehand) before reporting them. If in doubt, report it to https://www.nexusmods.com/stardewvalley/mods/36568?tab=bugs first", LogLevel.Info);

            harmony.Patch(
                original: AccessTools.Method(
                    AccessTools.TypeByName("Pathoschild.Stardew.Automate.Framework.Machines.DataBasedBuildingMachine"),
                    "SetInput"),
                prefix: new HarmonyMethod(typeof(AutomateCompat), nameof(SetInput_Prefix))
            );
        }

        public static bool SetInput_Prefix(object __instance, object input, ref bool __result)
        {
            try
            {
                Building building = Traverse.Create(__instance).Property("Machine").GetValue<Building>();
                if (!ModConstants.IsAutoSorterBuildingType(building?.buildingType.Value))
                    return true;

                Chest inputChest = building.GetBuildingChest(ModConstants.INPUT_CHEST_ID);
                if (inputChest is null)
                {
                    __result = false;
                    return false;
                }

                object itemsResult = Traverse.Create(input).Method("GetItems").GetValue();
                if (itemsResult is not IEnumerable items)
                {
                    __result = false;
                    return false;
                }

                foreach (object stack in items)
                {
                    Item sample = Traverse.Create(stack).Property("Sample").GetValue<Item>();
                    if (!building.IsValidObjectForChest(sample, inputChest))
                        continue;

                    if (TryStoreFullStack(stack, inputChest))
                    {
                        __result = true;
                        return false;
                    }
                }

                __result = false;
                return false;
            }
            catch (Exception ex)
            {
                _automateCompatWarningLoggedTimeCounter++;
                if (_automateCompatWarningLoggedTimeCounter == 20)
                {
                    ModEntry.ModMonitor.Log(
                        $"AutoSorterBuilding's Automate compatibility patch failed, falling back to default Automate behavior, please report to https://www.nexusmods.com/stardewvalley/mods/36568?tab=bugs with a log (https://smapi.io/log). Details:\n{ex}",
                        LogLevel.Warn);
                    _automateCompatWarningLoggedTimeCounter = 0;
                }
                return true;
            }
        }

        private static bool TryStoreFullStack(object trackedStack, Chest chest)
        {
            Traverse tracker = Traverse.Create(trackedStack);
            int count = tracker.Property("Count").GetValue<int>();
            if (count <= 0) return false;

            Item sample = tracker.Property("Sample").GetValue<Item>();

            chest.clearNulls();
            int originalCount = count;
            IInventory slots = chest.GetItemsForPlayer();
            int maxStackSize = sample.maximumStackSize();

            for (int i = 0; i < chest.GetActualCapacity() && count > 0; i++)
            {
                if (slots.Count <= i)
                {
                    // Take() internally calls Reduce() (which removes the item from its source
                    // inventory) but returns a *clone* made via Item.getOne(), which does not
                    // preserve inner state like a fishing rod's bait/tackle, an Object's
                    // heldObject, or a mod's custom inventory fields. Grab the real, original
                    // Item reference before Take() runs, then restore its stack size and store
                    // that instead of the lossy clone so nothing gets wiped.
                    Item original = tracker.Field("Item").GetValue<Item>();
                    tracker.Method("Take", count).GetValue<Item>();
                    original.Stack = count;
                    slots.Add(original);
                    count = 0;
                }
                else
                {
                    Item slot = slots[i];
                    if (sample.canStackWith(slot) && slot.Stack < maxStackSize)
                    {
                        Item toAdd = sample.getOne();
                        toAdd.Stack = Math.Min(count, maxStackSize - slot.Stack);
                        int actualAdded = toAdd.Stack - slot.addToStack(toAdd);

                        tracker.Method("Reduce", actualAdded).GetValue();
                        count -= actualAdded;
                    }
                }
            }

            return count < originalCount;
        }
    }
}