MyDefinitionId steelPlateBlueprint = MyDefinitionId.Parse($"MyObjectBuilder_BlueprintDefinition/SteelPlate");
MyDefinitionId constructionComponentBlueprint = MyDefinitionId.Parse($"MyObjectBuilder_BlueprintDefinition/ConstructionComponent");
double lastRunTimeMs = 0;

public Program()
{
    Echo("Assembler Load Balancer v1.0");
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

    SplitLargeStacks();
    LoadBalanceQueues();
}

string GetShortName(MyDefinitionId longName)
{
    return longName.ToString().Split('/')[1]; // add checks
}

void SplitLargeStacks()
{
    List<IMyAssembler> assemblers = FindAssemblers();
    List<MyProductionItem> items = new List<MyProductionItem>();

    if (assemblers.Count() == 0) {
        return;
    }

    assemblers.ForEach(assembler => {
        assembler.GetQueue(items);

        for (int itemIdx = 0, queueIdx = 0; itemIdx < items.Count(); itemIdx++, queueIdx++) {
            MyProductionItem item = items[itemIdx];

            // item has Amount, BlueprintId, ItemId	
            if (item.Amount <= 10) {
                // Echo($"{queueIdx}: {item.Amount} is fine");
                continue;
            }

            int numItemsToRequeue = (int)item.Amount - 10;
            bool useLastItemHack = itemIdx == (items.Count() - 1);

            // leave 10 and split remainder into stacks of at most 10
            Echo($"{queueIdx}: reducing stack of {item.Amount}");
            assembler.RemoveQueueItem(queueIdx, (double)numItemsToRequeue);

            if (useLastItemHack) {
                // The game won't let you split the last item in the queue. Calling Add or Insert both
                // merge everything back into one stack. So hack it by enqueuing a Steel Plate, then do 
                // our thing, then remove it.
                // Note: If the last item is SteelPlate, then use ConstructionComponent.
                if (item.BlueprintId.Equals(steelPlateBlueprint)) {
                    // Echo($"Hack: Adding ConstructionComponent");
                    assembler.AddQueueItem(constructionComponentBlueprint, (double)1);
                } else {
                    // Echo($"Hack: Adding SteelPlate");
                    assembler.AddQueueItem(steelPlateBlueprint, (double)1);
                }
            }                    

            while (numItemsToRequeue > 0) {
                queueIdx++;
                int thisStackSize = Math.Min(10, numItemsToRequeue);

                // Echo($"Inserting {thisStackSize} items at {queueIdx}");
                assembler.InsertQueueItem(queueIdx, item.BlueprintId, (double)thisStackSize);

                numItemsToRequeue -= 10;
            }

            if (useLastItemHack) {
                // Echo($"Hack: Removing placeholder item at {queueIdx+1}");
                assembler.RemoveQueueItem(queueIdx+1, (double)1);
            }
        }
    });
}

void LoadBalanceQueues()
{
    // List all Assemblers' queues.
    // Find the average length (round up).
    // Sort Assemblers by queue length, longest first.
    // For each assembler
    // if queue length greater than the average, move excess stacks into a holding buffer.
    // else pull from buffer (up to the average length).
    List<IMyAssembler> assemblers = FindAssemblers();
    List<MyProductionItem> items = new List<MyProductionItem>();

    if (assemblers.Count() == 0) {
        return;
    }

    int totalQueuedStacks = assemblers.Select(assembler => {
        assembler.GetQueue(items);
        return items.Count();
    }).Sum();

    int average = (int)Math.Ceiling(1.0 * totalQueuedStacks / assemblers.Count());
    Echo($"Average queue length is {average}");

    List<IMyAssembler> sortedAssemblers = assemblers.OrderByDescending(assembler => {
        assembler.GetQueue(items);
        return items.Count();
    }).ToList();

    List<MyProductionItem> holdingBin = new List<MyProductionItem>();

    sortedAssemblers.ForEach(assembler => {
        assembler.GetQueue(items);
        Echo($"{assembler.CustomName} has {items.Count()} items");

        if (items.Count() > average) {
            int numItemsToDequeue = items.Count() - average;
            while (numItemsToDequeue-- > 0) {
                MyProductionItem item = items[average];
                items.RemoveAt(average);
                Echo($"Removing {item.Amount} {GetShortName(item.BlueprintId)}");
                assembler.RemoveQueueItem(average, item.Amount);
                holdingBin.Add(item);
            }

        } else if (items.Count() < average) {
            int numItemsToEnqueue = Math.Min(average - items.Count(), holdingBin.Count());
            while (numItemsToEnqueue-- > 0) {
                MyProductionItem item = holdingBin[0];
                holdingBin.RemoveAt(0);
                Echo($"Adding {item.Amount} {GetShortName(item.BlueprintId)}");
                assembler.AddQueueItem(item.BlueprintId, item.Amount);
            }
        }
    });

    if (holdingBin.Count() != 0) {
        // should not happen
        Echo($"ERROR: Len holding bin: {holdingBin.Count()}");
    }

    // Note that last items of the same type in the queues will get consolidated.
    // The next run of SplitLargeStacks() will fix this.
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
