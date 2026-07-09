using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.GameData.Buildings;
using StardewValley.Inventories;
using StardewValley.Menus;
using StardewValley.Objects;

namespace AutoSorterBuilding
{
    internal sealed class ModEntry : Mod
    {
        // var defs to make things easier
        private const string CP_UNIQUE_ID = "AtlasV.AutoSorterBuilding";
        private const string BUILDING_ID = $"{CP_UNIQUE_ID}_AutoSorterBuilding";
        private const string INPUT_CHEST_ID = $"{CP_UNIQUE_ID}_InputChest";

        // empty category ID used for signs that are empty as catch all
        private const string EMPTY_CATEGORY_ID = $"{CP_UNIQUE_ID}_EmptyCategory";
        
        // bool so compat warning doesn't repeat every onTimeChanged and spam the log
        private static int AutomateCompatWarningLoggedTimeCounter = 0;
        
        private static IMonitor ModMonitor { get; set; } = null!;
        private static Harmony Harmony { get; set; } = null!;

        public override void Entry(IModHelper helper)
        {
            ModMonitor = Monitor;
            Harmony = new Harmony(ModManifest.UniqueID);

            /* This should be a Postfix so it will run AFTER the vanilla game's PerformBuildingChestAction function.
             This means our patch will run every time, even on other buildings and other chests, but we'll check the
             building inside our patch to make sure it only runs when relevant. */
            Harmony.Patch(
                original: AccessTools.Method(typeof(Building), nameof(Building.PerformBuildingChestAction)),
                postfix: new HarmonyMethod(typeof(ModEntry), nameof(Building_PerformBuildingChestAction_Postfix))
            );
            Harmony.Patch(
                original: AccessTools.Method(typeof(Building), nameof(Building.CheckItemConversionRule)),
                prefix: new HarmonyMethod(typeof(ModEntry), nameof(Building_CheckItemConversionRule_Prefix))
            );
            
            // Load check to make sure things only run when Automate is installed, also patch def and logging saying issues
            if (helper.ModRegistry.IsLoaded("Pathoschild.Automate"))
            {
                Monitor.Log("AutoSorterBuilding patches Automate's DataBasedBuildingMachine.SetInput. If you notice issues with Automate, please check whether they happen without this mod installed (backing up your save file beforehand) before reporting them. If in doubt, report it to https://www.nexusmods.com/stardewvalley/mods/36568?tab=bugs first", LogLevel.Info);

                Harmony.Patch(
                    original: AccessTools.Method(
                        AccessTools.TypeByName("Pathoschild.Stardew.Automate.Framework.Machines.DataBasedBuildingMachine"),
                        "SetInput"),
                    prefix: new HarmonyMethod(typeof(ModEntry), nameof(DataBasedBuildingMachine_SetInput_Prefix))
                );
            }

            helper.Events.GameLoop.TimeChanged += OnTimeChanged;
            
            
        }
        


        /* Runs every 10 in-game minutes. We only let the main player (host in multiplayer) trigger this
         so that all connected clients don't each independently try to sort the same buildings at the same
         time, which could cause item duplication or other multiplayer weirdness. */
        private void OnTimeChanged(object? sender, StardewModdingAPI.Events.TimeChangedEventArgs e)
        {
            if (!Context.IsMainPlayer) return;

            // Loops for narrowing down to Auto sort building
            foreach (GameLocation location in Game1.locations)
            {
                foreach (Building building in location.buildings)
                {
                    if (building.buildingType.Value is not BUILDING_ID) continue;

                    // checks mutex by watching ItemGrabMenu
                    Chest inputChest = building.GetBuildingChest(INPUT_CHEST_ID);
                    if (inputChest.GetMutex().IsLocked())
                    {
                        ModMonitor.Log($"Sorting items prevented in {building.GetIndoorsName()} due to mutex lock");
                        continue;
                    }
                    SortItems(building);
                }
            }
        }

        /* This function is just a little helper function so we don't have to repeat the same code
         in several different places. Both SpaceCore and Calcifer patch Object.getCategoryName() to
         return any overridden categories, so those will be supported by this helper. However, if
         a category is not overridden, but does not have a string representation of its category,
         then getCategoryName() would return an empty string. In that case, we want to fall back
         to using the item's category number \converted to a string as its category identifier. */
        private static string GetItemCategory(Item item)
        {
            string categoryName = item.getCategoryName();
            if (string.IsNullOrWhiteSpace(categoryName))
            {
                categoryName = item.Category.ToString();
            }

            return categoryName;
        }

        /* If this is our building, we don't have any actual item conversions, our ItemConversions thing
         is only there so we actually have a chest to place things in. We can't just remove ItemConversions
         because then nothing can be placed in the chest, but we don't want things to go to any destination
         chest, so we can't make one. What do we do? Let's just make the game skip checking our item conversions
         entirely! */
        [HarmonyPriority(Priority.First)]
        public static bool Building_CheckItemConversionRule_Prefix(Building __instance)
        {
            /* Since this is a bool prefix, Harmony will not run the original function if we return false. */
            if (__instance.buildingType.Value is BUILDING_ID) return false;
            return true;
        }
        
        public static bool DataBasedBuildingMachine_SetInput_Prefix(object __instance, object input, ref bool __result)
        {
            try
            {
                Building building = Traverse.Create(__instance).Property("Machine").GetValue<Building>();
                if (building?.buildingType.Value is not BUILDING_ID)
                    return true;

                Chest inputChest = building.GetBuildingChest(INPUT_CHEST_ID);
                if (inputChest is null)
                {
                    __result = false;
                    return false;
                }

                object itemsResult = Traverse.Create(input).Method("GetItems").GetValue();
                if (itemsResult is not System.Collections.IEnumerable items)
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
                AutomateCompatWarningLoggedTimeCounter++;
                if (AutomateCompatWarningLoggedTimeCounter == 20)
                {
                    ModMonitor.Log(
                        $"AutoSorterBuilding's Automate compatibility patch failed, falling back to default Automate behavior, please report to https://www.nexusmods.com/stardewvalley/mods/36568?tab=bugs with a log (https://smapi.io/log). Details:\n{ex}",
                        LogLevel.Warn);
                    AutomateCompatWarningLoggedTimeCounter = 0;
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
                    Item taken = tracker.Method("Take", count).GetValue<Item>();
                    slots.Add(taken);
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

        /* This function was chosen for the patch because it's what runs when the player clicks on the input chest on
         the exterior of the building. Depending on what type of chest it is, different things will happen, but if it's
         the normal chest type like we have it set, this vanilla function will end up opening a Chest menu for us that
         we want to watch out for. So, this is the simplest function to patch for this behaviour. */
        public static void Building_PerformBuildingChestAction_Postfix(Building __instance)
        {
            /* If the building is not an AutoSorterBuilding, we don't need to do anything, so return early.
             Similarly, if the active menu after PerformBuildingChestAction is *not* an ItemGrabMenu, it
             means that our input chest wasn't actually opened. This should only happen if the chest type
             in the Content Patcher pack is changed from "Chest" to "Input" or "Output" but who knows what
             other mods might do, so we'll check for it just to be safe. */
            if (__instance.buildingType.Value is not BUILDING_ID ||
                Game1.activeClickableMenu is not ItemGrabMenu menu) return;

            /* The menu's exitFunction doesn't always get called, so we use actionsWhenPlayerFree here to
             queue up a delegate to run instead. The player isn't considered free until they've closed the menu. */
            Game1.actionsWhenPlayerFree.Add(() =>
                {
                    /* Can't hurt to double-check that it's our building type that is the reason this menu is opened,
                     and not some other mod interaction that might've happened in the middle of PerformBuildingChestAction. */
                    if (menu.context is Building building && building.buildingType.Value is BUILDING_ID)
                    {
                        SortItems(building);
                    }
                }
            );
        }

        private static void SortItems(Building building)
        {
            /* Not entirely necessary to log this, but may help with debugging if anything ever goes wrong
             to know which specific interior it was. The monitor logs to the Trace LogLevel by default, so
             it won't show in the console, but it'll appear in the log file when uploaded somewhere. */
            ModMonitor.Log($"Sorting items into AutoSorter with name {building.GetIndoorsName()}");
            
            /* This will give is a dictionary where the keys are item categories and the values are lists of
             chests we found inside this specific building instance. We collect them all now so that we don't
             have to search the entire interior for every item in the input chest. */
            Dictionary<string, List<Chest>> chests = CollectChests(building);
            
            /* If we didn't find any chests, or somehow all of our lists of chests are empty, then we can't sort anything,
             so we return early. */
            if (chests.Count <= 0 || chests.All(pair => pair.Value.Count <= 0))
            {
                return;
            }
            
            /* This will grab all the chests that had empty signs above them, which we may need for both
             items that couldn't be matched to a specific chest and items that could be matched but their
             chest is full. If we have no catch-all chests, this list won't exist, so we need to remember
             to check for null where appropriate. */
            List<Chest>? EmptyCategoryChests = chests.GetValueOrDefault(EMPTY_CATEGORY_ID);

            /* This will be the input chest as defined in our Content Patcher pack. */
            Chest toSort = building.GetBuildingChest(INPUT_CHEST_ID);
            
            /* We'll need to clear our input chest, so we'll need to make a copy of the items inside it.
             When we call ToList() here on the IInventory instance returned by GetItemsForPlayer, it
             creates a new List for us, but both itemsToSort and the original IInventory will point to
             the same objects in memory. Basically, the List itself and the items inside the list are
             different things, where the List tells the computer where to look for the items, but the items
             never move. */
            List<Item> itemsToSort = toSort.GetItemsForPlayer().ToList();
            
            /* Therefore, when we clear the IInventory now, we're only clearing its knowledge of where
             the items are in memory. The GetItemsForPlayer IInventory will no longer have any clue
             where the items are, so it will be an empty list, but our other list we just made, itemsToSort,
             still knows where the original items are in memory, so we can still use them. We haven't
             copied the items, we only made two lists that both know where the same items are and then
             made one of those lists forget about them. */
            toSort.GetItemsForPlayer().Clear();
            
            /* This leftoverItems list will just help us in our loop coming up and is entirely separate. */
            List<Item> leftoverItems = new List<Item>();
            
            foreach (var item in itemsToSort)
            {
                /* If we don't have any chests with a matching category... */
                if (!chests.TryGetValue(GetItemCategory(item), out var chestList))
                {
                    /* ...but we DO have catch-all chests, then we'll just use those. */
                    if (EmptyCategoryChests is not null)
                    {
                        chestList = EmptyCategoryChests;
                    }
                    /* Otherwise, we have nowhere to put this item, so we mark it as leftover and continue
                     to the next item in our input chest. */
                    else
                    {
                        leftoverItems.Add(item);
                        continue;
                    }
                }
                else if (EmptyCategoryChests is not null)
                {
                    /* If we entered this block, it means we DID find at least one chest that matches the
                     category, however we still want to use the catch-all chests for oveflow purposes. But
                     instead of assigning the empty category list to our chestList variable, we append
                     its contents to the end of our exact-match list. */
                    chestList.AddRange(EmptyCategoryChests);
                }

                /* Create a new Item instance to hold our leftover item, if any. The "item" variable in this loop
                 can't be changed mid-loop, so we have to create this new Item reference. We want to create it
                 outside the loop up ahead because we want to know if we have any leftover after we've checked
                 every chest, and not reset it every time we look at a new chest. */
                Item? leftover = null;
                
                foreach (var chest in chestList)
                {
                    /* If the mutex is locked, that means another player is currently using the chest we're
                     looking at. We don't want to touch that chest while that's happpening, that can lead
                     to strange multiplayer issues like desync or item duplication. We have to skip this
                     chest in that case. */
                    if (chest.GetMutex().IsLocked()) continue;

                    /* addItem will return null if the chest could successfully hold the entire stack of
                     the item we're trying to add to it. If not, though (for example, if the chest could
                     only hold 500 wood but we tried to add a stack of 999), then it will return what
                     was leftover. Continuing that example, that means our leftover variable here would
                     become a Wood Item with a stack size of 499. */
                    leftover = chest.addItem(item);
                    
                    /* If the leftover IS null though, it means we're done with this input item, so no
                     need to check the other chests. */
                    if (leftover is null) break;
                }

                /* If we've looked at every chest with a matching category and we still have a leftover item,
                 then that means this AutoSorter building cannot hold this input. Either because there wasn't
                 enough space or we just didn't have a chest with a matching category. So, it gets added
                 to our list of leftovers. Continuing our earlier example, we'd have our 499 stack of wood
                 added to our list here. */
                if (leftover is not null) leftoverItems.Add(leftover);
            }

            /* After we've gone through every item in the input chest and ended up here, our leftoverItems list
             will be full of the stuff we couldn't fit in the AutoSorter for one reason or another. So we add
             them all back to the AutoSorter's input chest again so the player can retrieve them. */
            toSort.GetItemsForPlayer().AddRange(leftoverItems);
        }

        private static Dictionary<string, List<Chest>> CollectChests(Building building)
        {
            Dictionary<string, List<Chest>> chests = new();

            /* This will get the indoor location unique to this specific building instance, no
             matter how many of this building type the player has built in their world. */
            GameLocation interior = building.GetIndoors();
            
            /* This Utility function takes in an action to perform on every item in the interior we
             give it, the one we just got above. Chests are Objects are Items, so this will find
             any Chest that is placed inside our building. */
            Utility.ForEachItemIn(interior, item =>
            {
                /* Like above, we want to make sure the chest mutex is not locked. It's technically
                 not impossible for it to be unlocked here but then locked by the time the rest of
                 our sorting happens, so we should check in both places. */
                if (item is Chest chest && !chest.GetMutex().IsLocked())
                {
                    /* Luckily, the GameLocation class (which is what our interior is) has a function
                     we can use to get whatever Object is at a specific tile. We found our chest, so now
                     we check if there is another Object placed directly above it and that the Object is
                     specifically a Sign class object. */
                    var itemAbove = interior.getObjectAtTile((int)chest.TileLocation.X, (int)chest.TileLocation.Y - 1); /* -1 because positive Y is down. */
                    
                    if (itemAbove is Sign sign)
                    {
                        /* The "is { }" part just makes sure that the displayItem on our Sign is not null.
                         We just don't know exactly what type it is, but we don't really care as long as
                         it's a non-null item anyway for us to assign to the displayedItem variable. */
                        if (sign.displayItem.Value is { } displayedItem)
                        {
                            
                            if (!chests.TryGetValue(GetItemCategory(displayedItem), out var chestList))
                            {
                                /* If TryGetValue returns false here, it means this is the first time
                                 we've seen an item with this category, so we need to make a new List
                                 for our dictionary first, because we can't add a chest to a list that
                                 doesn't exist. */
                                chestList = new List<Chest>();
                                chests[GetItemCategory(displayedItem)] = chestList;
                            }

                            /* Chest objects are reference types, so when we add it to the list here, it's
                             not making a copy of it. It's giving our list an address to know where to find
                             this exact Chest instance in our building. */
                            chestList.Add(chest);
                        }
                        else
                        {
                            /* If our displayedItem IS null, that means we have a sign, it's just empty. This should
                             act as a catch-all chest for ANY input, so we can add it to the catch-all category here. */
                            if (!chests.TryGetValue(EMPTY_CATEGORY_ID, out var chestList))
                            {
                                chestList = new List<Chest>();
                                chests[EMPTY_CATEGORY_ID] = chestList;
                            }
                            
                            chestList.Add(chest);
                        }
                    }
                }

                /* Returning true in the context of this Utility function just means that we're telling it
                 to keep searching every item in the map. If we returned false, we'd stop searching entirely,
                 but we want to find EVERY chest in the map, so we need to keep returning true. */
                return true;
            });
            
            return chests;
        }
    }
}