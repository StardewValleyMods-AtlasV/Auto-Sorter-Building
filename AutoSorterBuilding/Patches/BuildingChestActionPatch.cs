using HarmonyLib;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Menus;
using AutoSorterBuilding.Sorting;

namespace AutoSorterBuilding.Patches
{
    internal static class BuildingChestActionPatch
    {
        public static void Register(Harmony harmony)
        {
            /* This should be a Postfix so it will run AFTER the vanilla game's PerformBuildingChestAction function.
             This means our patch will run every time, even on other buildings and other chests, but we'll check the
             building inside our patch to make sure it only runs when relevant. */
            harmony.Patch(
                original: AccessTools.Method(typeof(Building), nameof(Building.PerformBuildingChestAction)),
                postfix: new HarmonyMethod(typeof(BuildingChestActionPatch), nameof(Postfix))
            );
        }

        /* This function was chosen for the patch because it's what runs when the player clicks on the input chest on
         the exterior of the building. Depending on what type of chest it is, different things will happen, but if it's
         the normal chest type like we have it set, this vanilla function will end up opening a Chest menu for us that
         we want to watch out for. So, this is the simplest function to patch for this behaviour. */
        public static void Postfix(Building __instance)
        {
            /* If the building is not an AutoSorterBuilding, we don't need to do anything, so return early.
             Similarly, if the active menu after PerformBuildingChestAction is *not* an ItemGrabMenu, it
             means that our input chest wasn't actually opened. This should only happen if the chest type
             in the Content Patcher pack is changed from "Chest" to "Input" or "Output" but who knows what
             other mods might do, so we'll check for it just to be safe. */
            if (!ModConstants.IsAutoSorterBuildingType(__instance.buildingType.Value) ||
                Game1.activeClickableMenu is not ItemGrabMenu menu) return;

            /* The menu's exitFunction doesn't always get called, so we use actionsWhenPlayerFree here to
             queue up a delegate to run instead. The player isn't considered free until they've closed the menu. */
            Game1.actionsWhenPlayerFree.Add(() =>
                {
                    /* Can't hurt to double-check that it's our building type that is the reason this menu is opened,
                     and not some other mod interaction that might've happened in the middle of PerformBuildingChestAction. */
                    if (menu.context is Building building &&
                        ModConstants.IsAutoSorterBuildingType(building.buildingType.Value))
                    {
                        ItemSorter.SortItems(building, isManualSort: true);
                    }
                }
            );
        }
    }
}
