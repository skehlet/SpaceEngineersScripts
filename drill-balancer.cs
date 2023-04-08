IMyTextSurface myScreen = null;
int logCounter = 1;
const float FONT_SIZE = 1.0F;
double lastRunTimeMs = 0;

public Program()
{
    myScreen = Me.GetSurface(0);
    myScreen.ContentType = ContentType.TEXT_AND_IMAGE;
    myScreen.FontSize = FONT_SIZE;
    myScreen.Alignment = VRage.Game.GUI.TextPanel.TextAlignment.LEFT;

    Log("Drill Balancer v1.0");

    Runtime.UpdateFrequency = UpdateFrequency.Update100;
}

public void Main(string argument, UpdateType updateSource)
{
    double now = GetTimeInMs();
    if (now - lastRunTimeMs < 1000)
    {
        return;
    }
    Log($"Running at {now}");
    lastRunTimeMs = now;

    LoadBalanceDrills();
}

List<IMyShipDrill> FindDrills(Func<IMyShipDrill, Boolean> filter = null)
{
    return FilterBlocks<IMyShipDrill>(filter);
}

void LoadBalanceDrills()
{
    // Sum up the volume of all items in all Drills.
    // Find the average (round up).
    // Sort Drills by colume, largest first.
    // For each drill:
    //      Get a sum of the volume
    //      If it's more than the average:
    //          Start walking through the items and counting until you hit the average. Move all excess items to the holding bin.
    //      Else (If it's less than the average):
    //          Calculate how many more items it should have, then pull those from the holding bin.
    //
    // Go through all drills, all inventories, all slots
    //      Create map storing the total quantity of each MyItemType found
    // Create a second map storing the average quantity of each MyItemType
    // Every drill should store an average number of each MyItemType
    // Sort the drills, largest total volume first
    // Go through each drill
    // For each MyItemType
    // Find quantity of each MyItemType (use MyInventoryItem? FindItem(MyItemType itemType))
    // Find the difference between the quantity found and the average
    // If the difference is positive (meaning higher than average)
    // then go through each other drill and find that drill's difference
    // if that drill's difference is negative (meaning it's lower than average), deposit up to that drill's abs(difference)
    List<IMyShipDrill> drills = FindDrills();
    // List<MyProductionItem> myItems = new List<MyProductionItem>();

    if (drills.Count == 0)
    {
        Log("No drills found");
        return;
    }

    var typeTotals = new Dictionary<MyItemType, int>();
    var typeAvgs = new Dictionary<MyItemType, int>();

    // Go through all drills, all inventories, all items
    //      Create map storing the total quantity of each MyItemType found
    drills.ForEach(drill => {
        // Log($"{drill.CustomName}: {drill.InventoryCount} invs");
        GetInventoriesFromEntity(drill).ForEach(inventory => {
            GetItemsFromInventory(inventory).ForEach(myInventoryItem => {
                if (typeTotals.ContainsKey(myInventoryItem.Type)) {
                    typeTotals[myInventoryItem.Type] += (int)myInventoryItem.Amount;
                } else {
                    typeTotals.Add(myInventoryItem.Type, (int)myInventoryItem.Amount);
                }
            });
        });
    });

    // Create a second map storing the average quantity of each MyItemType
    // TODO: this assumes all drills and their inventory(ies, they only have 1) are identical
    foreach (KeyValuePair<MyItemType, int> kvp in typeTotals) {
        // Log($"{kvp.Key}: {kvp.Value}");
        int average = (int)Math.Ceiling(1.0 * kvp.Value / drills.Count);
        typeAvgs.Add(kvp.Key, average);
        // Log($"{kvp.Key}: {average}");
    }

    // Every drill should store an average number of each MyItemType
    // Sort the drills, largest total volume first
    List<IMyShipDrill> sortedDrills = drills.OrderByDescending(GetCurrentVolumeByDrill).ToList();

    // // debug
    // sortedDrills.ForEach(drill => {
    //     Log($"{drill.CustomName}: vol: {GetCurrentVolumeByDrill(drill)}");
    // });

    // For each MyItemType
    // For each drill (sorted)
    // If it has more than the average quantity of this type,
    // figure out how to distribute that excess to all drills below average for this type, 
    // without pushing that one over the average
    foreach (KeyValuePair<MyItemType, int> keyValue in typeAvgs) {
        var type = keyValue.Key;
        var average = keyValue.Value;

        sortedDrills.ForEach(drill => {
            GetInventoriesFromEntity(drill).ForEach(inventory => {
                var myQuantity = GetQuantityOfTypeByInventory(type, inventory);
                if (myQuantity > average) {
                    int excessCount = myQuantity - average;
                    Log($"{drill.CustomName} excess {GetShortName(type)}: {excessCount}");

                    List<MyInventoryItem> myItems = GetItemsFromInventory(inventory, item => item.Type == type);

                    myItems.ForEach(myItem => {

                        Dictionary<IMyInventory, int> otherInventories = FindOtherInventoriesWithRoomForType(type, average);
                        foreach (KeyValuePair<IMyInventory, int> keyValue2 in otherInventories) {
                            var otherInventory = keyValue2.Key;
                            int otherRoom = keyValue2.Value;

                            var amountToMove = Math.Min(Math.Min(excessCount, otherRoom), (int)myItem.Amount);
                            if (amountToMove > 0) {
                                Log($"Moving {amountToMove} to other inventory");
                                inventory.TransferItemTo(otherInventory, myItem, amountToMove);
                                excessCount -= amountToMove;
                            }
                        }
                    });
                }
            });
        });
    }
}

List<IMyInventory> GetInventoriesFromEntity(IMyEntity entity) {
    List<IMyInventory> myInventories = new List<IMyInventory>();
    var inventoryCount = entity.InventoryCount;
    for (int i = 0; i < inventoryCount; i++) {
        myInventories.Add(entity.GetInventory(i));
    }
    return myInventories;
}

List<MyInventoryItem> GetItemsFromInventory(IMyInventory inventory, Func<MyInventoryItem, Boolean> filter = null) {
    List<MyInventoryItem> myInventoryItems = new List<MyInventoryItem>();
    inventory.GetItems(myInventoryItems);
    return filter == null ? myInventoryItems : myInventoryItems.FindAll(item => filter(item));
}

int GetCurrentVolumeByDrill(IMyShipDrill drill) {
    return GetInventoriesFromEntity(drill).Select(inventory => (int)(1000 * inventory.CurrentVolume)).Sum();
}

int GetQuantityOfTypeByDrill(MyItemType type, IMyShipDrill drill) {
    return GetInventoriesFromEntity(drill).Select(inventory => {
        return GetItemsFromInventory(inventory, item => item.Type == type)
            .Select(item => (int)item.Amount)
            .Sum();
    }).Sum();
}

int GetQuantityOfTypeByInventory(MyItemType type, IMyInventory inventory) {
    return GetItemsFromInventory(inventory, item => item.Type == type)
        .Select(item => (int)item.Amount)
        .Sum();
}

Dictionary<IMyInventory, int> FindOtherInventoriesWithRoomForType(MyItemType type, int average) {
    var otherInventories = new Dictionary<IMyInventory, int>();
    FindDrills().ForEach(otherDrill => {
        GetInventoriesFromEntity(otherDrill).ForEach(otherInventory => {
            var otherQuantity = GetQuantityOfTypeByInventory(type, otherInventory);
            var otherRoom = average - otherQuantity;
            if (otherRoom > 0) {
                Log($"Found inventory with {otherRoom} room");
                otherInventories.Add(otherInventory, otherRoom);
            }
        });
    });
    return otherInventories;
}


string GetShortName(MyDefinitionId longName)
{
    return longName.ToString().Split('/')[1]; // TODO error checks
}

public static long GetTimeInMs()
{
    return DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
}

public List<T> FilterBlocks<T>(Func<T, Boolean> filter = null) where T : class, IMyTerminalBlock
{
    var blocks = new List<T>();
    GridTerminalSystem.GetBlocksOfType(blocks, x =>
    {
        if (!x.IsSameConstructAs(Me)) return false;
        return (filter == null) || filter(x);
    });
    return blocks.ConvertAll(x => (T)x);
}

public T GetFirstBlockOfType<T>(Func<T, Boolean> filter = null) where T : class, IMyTerminalBlock
{
    var blocks = FilterBlocks<T>(filter);
    return (blocks.Count == 0) ? null : blocks.First();
}

public void Log(string message)
{
    message = $"{logCounter++}: {message.Trim()}";

    Echo(message);

    string oldText = myScreen.GetText();
    // only keep most recent 50 lines
    IEnumerable<string> oldLines = oldText.Split(
        new string[] { "\r\n", "\n" },
        StringSplitOptions.RemoveEmptyEntries
    ).Take(50);
    string newText = message + Environment.NewLine.ToString()
        + string.Join(Environment.NewLine.ToString(), oldLines);
    myScreen.WriteText(newText);
}
