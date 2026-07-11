using HarmonyLib;
using StardewValley.Buildings;

namespace AutoSorterBuilding.Patches
{
    internal static class ItemConversionPatch
    {
        public static void Register(Harmony harmony)
        {
            harmony.Patch(
                original: AccessTools.Method(typeof(Building), nameof(Building.CheckItemConversionRule)),
                prefix: new HarmonyMethod(typeof(ItemConversionPatch), nameof(Prefix))
            );
        }

        /* If this is our building, we don't have any actual item conversions, our ItemConversions thing
         is only there so we actually have a chest to place things in. We can't just remove ItemConversions
         because then nothing can be placed in the chest, but we don't want things to go to any destination
         chest, so we can't make one. What do we do? Let's just make the game skip checking our item conversions
         entirely! */
        [HarmonyPriority(Priority.First)]
        public static bool Prefix(Building __instance)
        {
            /* Since this is a bool prefix, Harmony will not run the original function if we return false. */
            if (__instance.buildingType.Value is ModConstants.BUILDING_ID) return false;
            return true;
        }
    }
}
