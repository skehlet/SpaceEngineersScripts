double lastRunTimeMs = 0;

public Program()
{
    Echo("Assembler Load Balancer v2.0");
    Runtime.UpdateFrequency = UpdateFrequency.Update100;
}

List<IMyAssembler> FindAssemblers()
{
    return FilterBlocks<IMyAssembler>(assembler => {
        if (!assembler.IsWorking) return false;
        if (!assembler.DetailedInfo.Contains("Type: Assembler")) return false; // filter out Survival kits
        if (assembler.Mode != MyAssemblerMode.Assembly) return false;
        return true;
    });
}

public void Main(string argument, UpdateType updateSource)
{
    double now = GetTimeInMs();
    if (now - lastRunTimeMs < 5000) {
        return;
    }
    Echo($"Running at {now}");
    lastRunTimeMs = now;

    LoadBalanceAssemblers();
}

string GetShortName(MyDefinitionId longName)
{
    return longName.ToString().Split('/')[1]; // TODO error checks
}

void LoadBalanceAssemblers()
{
    // Sum up the number of item in all Assemblers.
    // Find the average (round up).
    // Sort Assemblers by item count, largest first.
    // For each assembler:
    //      Get a sum of the number of items
    //      If it's more than the average:
    //          Start walking through the items and counting until you hit the average. Move all excess items to the holding bin.
    //      Else (If it's less than the average):
    //          Calculate how many more items it should have, then pull those from the holding bin.
    List<IMyAssembler> assemblers = FindAssemblers();
    List<MyProductionItem> myItems = new List<MyProductionItem>();

    if (assemblers.Count == 0) {
        return;
    }

    int allItemCount = assemblers.Select(assembler => {
        assembler.GetQueue(myItems);
        return myItems.Select(item => (int)item.Amount).Sum();
    }).Sum();

    int average = (int)Math.Ceiling(1.0 * allItemCount / assemblers.Count);
    Echo($"Tot: {allItemCount}/{assemblers.Count} Avg: {average}");

    List<IMyAssembler> sortedAssemblers = assemblers.OrderByDescending(assembler => {
        assembler.GetQueue(myItems);
        return myItems.Select(item => (int)item.Amount).Sum();
    }).ToList();

    List<MyProductionItem> holdingBin = new List<MyProductionItem>();

    sortedAssemblers.ForEach(assembler => {
        assembler.GetQueue(myItems);
        int myTotalItemCount = myItems.Select(item => (int)item.Amount).Sum();
        Echo($"{myTotalItemCount}/{average}: {assembler.CustomName}");

        if (myTotalItemCount > average) {
            int myItemCount = 0;
            for (int myItemIdx = 0; myItemIdx < myItems.Count; myItemIdx++) {
                MyProductionItem myItem = myItems[myItemIdx];

                if (myItemCount < average) {
                    int spaceAvailable = average - myItemCount;
                    if (myItem.Amount <= spaceAvailable) {
                        Echo($"{myItemIdx}: OK {myItem.Amount} {GetShortName(myItem.BlueprintId)}");
                        myItemCount += (int)myItem.Amount;
                    } else {
                        // must reduce this item and put excess into holding bin
                        Echo($"{myItemIdx}: ONLY {spaceAvailable}/{myItem.Amount} {GetShortName(myItem.BlueprintId)}");
                        int excess = (int)myItem.Amount - spaceAvailable;
                        assembler.RemoveQueueItem(myItemIdx, (double)excess);
                        myItemCount += spaceAvailable;
                        MyProductionItem myNewItem = new MyProductionItem(myItem.ItemId, myItem.BlueprintId, excess);
                        holdingBin.Add(myNewItem);
                    }
                } else {
                    // all to holding bin
                    Echo($"{myItemIdx}: XS {myItem.Amount} {GetShortName(myItem.BlueprintId)}");
                    assembler.RemoveQueueItem(myItemIdx, (double)myItem.Amount);
                    holdingBin.Add(myItem);
                }
            }
        } else {
            int takeCount = average - myTotalItemCount;
            while (takeCount > 0 && holdingBin.Count > 0) {
                MyProductionItem myItem = holdingBin[0];
                holdingBin.RemoveAt(0);

                if (myItem.Amount <= takeCount) {
                    // take it all
                    Echo($"ADD {myItem.Amount} {GetShortName(myItem.BlueprintId)}");
                    assembler.AddQueueItem(myItem.BlueprintId, myItem.Amount);
                    takeCount -= (int)myItem.Amount;

                } else {
                    // take only part and put the rest back
                    Echo($"ADD {takeCount}/{myItem.Amount} {GetShortName(myItem.BlueprintId)}");
                    assembler.AddQueueItem(myItem.BlueprintId, (double)takeCount);
                    MyProductionItem myNewItem = new MyProductionItem(myItem.ItemId, myItem.BlueprintId, myItem.Amount - takeCount);
                    holdingBin.Insert(0, myNewItem);
                    break;
                }
            }
        }
    });

    if (holdingBin.Count != 0) {
        // should not happen
        Echo($"ERROR: Len holding bin: {holdingBin.Count}");
    }
}

public static long GetTimeInMs()
{
    return DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
}

public List<T> FilterBlocks<T>(Func<T, Boolean> filter = null) where T : class, IMyTerminalBlock
{
    var blocks = new List<T>();
    GridTerminalSystem.GetBlocksOfType(blocks, x => {
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
