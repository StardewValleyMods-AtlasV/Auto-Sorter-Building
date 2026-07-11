using System.Collections.Generic;
using System.Linq;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Objects;
using AutoSorterBuilding.Compat;

namespace AutoSorterBuilding.Sorting
{
    internal static class ItemSorter
    {
        // isManualSort is true only when this was triggered by the player closing the input chest's menu
        // (see Patches.BuildingChestActionPatch.Postfix), and false when triggered by the OnTimeChanged
        // timer.
        public static void SortItems(Building building, bool isManualSort = false)
        {
            /* Not entirely necessary to log this, but may help with debugging if anything ever goes wrong
             to know which specific interior it was. The monitor logs to the Trace LogLevel by default, so
             it won't show in the console, but it'll appear in the log file when uploaded somewhere. */
            ModEntry.ModMonitor.Log($"Sorting items into AutoSorter with name {building.GetIndoorsName()}");

            /* This will give is a dictionary where the keys are item categories and the values are lists of
             chests we found inside this specific building instance. We collect them all now so that we don't
             have to search the entire interior for every item in the input chest. */
            Dictionary<string, List<Chest>> chests = CollectChests(building, isManualSort);

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
            List<Chest>? EmptyCategoryChests = chests.GetValueOrDefault(ModConstants.EMPTY_CATEGORY_ID);

            /* This will be the input chest as defined in our Content Patcher pack. */
            Chest toSort = building.GetBuildingChest(ModConstants.INPUT_CHEST_ID);

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
                if (!chests.TryGetValue(ItemCategoryHelper.GetItemCategory(item), out var chestList))
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

        private static Dictionary<string, List<Chest>> CollectChests(Building building, bool updateChestNames)
        {
            Dictionary<string, List<Chest>> chests = new();

            // Only populated when going to rename chests on pass. Holds every signed
            // chest we find (including catch-all ones) paired with the category label its sign resolves
            // to. can't name chests as found anymore, since Utility.ForEachItemIn doesn't
            // guarantee it visits them in any particular order
            List<(Chest Chest, string Category)>? chestsToName = updateChestNames && ChestsAnywhereCompat.IsLoaded
                ? new List<(Chest, string)>()
                : null;

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
                            string category = ItemCategoryHelper.GetItemCategory(displayedItem);

                            if (!chests.TryGetValue(category, out var chestList))
                            {
                                /* If TryGetValue returns false here, it means this is the first time
                                 we've seen an item with this category, so we need to make a new List
                                 for our dictionary first, because we can't add a chest to a list that
                                 doesn't exist. */
                                chestList = new List<Chest>();
                                chests[category] = chestList;
                            }

                            /* Chest objects are reference types, so when we add it to the list here, it's
                             not making a copy of it. It's giving our list an address to know where to find
                             this exact Chest instance in our building. */
                            chestList.Add(chest);

                            chestsToName?.Add((chest, category));
                        }
                        else
                        {
                            /* If our displayedItem IS null, that means we have a sign, it's just empty. This should
                             act as a catch-all chest for ANY input, so we can add it to the catch-all category here. */
                            if (!chests.TryGetValue(ModConstants.EMPTY_CATEGORY_ID, out var chestList))
                            {
                                chestList = new List<Chest>();
                                chests[ModConstants.EMPTY_CATEGORY_ID] = chestList;
                            }

                            chestList.Add(chest);

                            // Unlike vanilla category names, "Uncategorized" isn't a string pre-localized,
                            // so this loads from the mods i18n files.
                            chestsToName?.Add((chest, ModEntry.Translation.Get("uncategorized-chest-name")));
                        }
                    }
                }

                /* Returning true in the context of this Utility function just means that we're telling it
                 to keep searching every item in the map. If we returned false, we'd stop searching entirely,
                 but we want to find EVERY chest in the map, so we need to keep returning true. */
                return true;
            });

            if (chestsToName is not null)
            {
                ChestsAnywhereCompat.NameChestsInReadingOrder(chestsToName);
            }

            return chests;
        }
    }
}
