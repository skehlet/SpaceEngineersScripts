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

    Log("Assembler Load Balancer v2.0");
    Runtime.UpdateFrequency = UpdateFrequency.Update100;
}

List<IMyAssembler> FindAssemblers()
{
    return FilterBlocks<IMyAssembler>(assembler => {
        //Log($"assembler: {assembler.DetailedInfo}");
        if (!assembler.IsWorking) {
            Log($"Skipping {assembler.Name}, not working");
            return false;
        }
        if (assembler.DetailedInfo.Contains("Type: Survival Kit")) {
            Log($"Skipping {assembler.Name}, is Survival kit");
            return false; // filter out Survival kits
        }
        // if (!assembler.DetailedInfo.Contains("Type: Assembler")) {
        //     Log($"Skipping {assembler.Name}, is Survival kit?");
        //     return false; // filter out Survival kits
        // }
        if (assembler.Mode != MyAssemblerMode.Assembly) {
            Log($"Skipping {assembler.Name}, in disassembly mode");
            return false;
        }
        return true;
    });
}

public void Main(string argument, UpdateType updateSource)
{
    double now = GetTimeInMs();
    if (now - lastRunTimeMs < 5000) {
        return;
    }
    Log($"Running at {now}");
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
        Log("No assemblers found");
        return;
    }

    int allItemCount = assemblers.Select(assembler => {
        assembler.GetQueue(myItems);
        return myItems.Select(item => (int)item.Amount).Sum();
    }).Sum();

    int average = (int)Math.Ceiling(1.0 * allItemCount / assemblers.Count);
    Log($"Tot: {allItemCount}/{assemblers.Count} Avg: {average}");

    List<IMyAssembler> sortedAssemblers = assemblers.OrderByDescending(assembler => {
        assembler.GetQueue(myItems);
        return myItems.Select(item => (int)item.Amount).Sum();
    }).ToList();

    List<MyProductionItem> holdingBin = new List<MyProductionItem>();

    sortedAssemblers.ForEach(assembler => {
        assembler.GetQueue(myItems);
        int myTotalItemCount = myItems.Select(item => (int)item.Amount).Sum();
        Log($"{myTotalItemCount}/{average}: {assembler.CustomName}");

        if (myTotalItemCount > average) {
            int myItemCount = 0;
            for (int myItemIdx = 0; myItemIdx < myItems.Count; myItemIdx++) {
                MyProductionItem myItem = myItems[myItemIdx];

                if (myItemCount < average) {
                    int spaceAvailable = average - myItemCount;
                    if (myItem.Amount <= spaceAvailable) {
                        Log($"{myItemIdx}: OK {myItem.Amount} {GetShortName(myItem.BlueprintId)}");
                        myItemCount += (int)myItem.Amount;
                    } else {
                        // must reduce this item and put excess into holding bin
                        Log($"{myItemIdx}: ONLY {spaceAvailable}/{myItem.Amount} {GetShortName(myItem.BlueprintId)}");
                        int excess = (int)myItem.Amount - spaceAvailable;
                        assembler.RemoveQueueItem(myItemIdx, (double)excess);
                        myItemCount += spaceAvailable;
                        MyProductionItem myNewItem = new MyProductionItem(myItem.ItemId, myItem.BlueprintId, excess);
                        holdingBin.Add(myNewItem);
                    }
                } else {
                    // all to holding bin
                    Log($"{myItemIdx}: XS {myItem.Amount} {GetShortName(myItem.BlueprintId)}");
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
                    Log($"ADD {myItem.Amount} {GetShortName(myItem.BlueprintId)}");
                    assembler.AddQueueItem(myItem.BlueprintId, myItem.Amount);
                    takeCount -= (int)myItem.Amount;

                } else {
                    // take only part and put the rest back
                    Log($"ADD {takeCount}/{myItem.Amount} {GetShortName(myItem.BlueprintId)}");
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
        Log($"ERROR: Len holding bin: {holdingBin.Count}");
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

public void Log(string message)
{
    message = $"{logCounter++}: {message.Trim()}";

    Echo(message);

    string oldText = myScreen.GetText();
    // only keep most recent 50 lines
    IEnumerable<string> oldLines = oldText.Split(new string[] {"\r\n","\n"}, StringSplitOptions.RemoveEmptyEntries).Take(50);
    string newText = message + Environment.NewLine.ToString()
        + string.Join(Environment.NewLine.ToString(), oldLines);
    myScreen.WriteText(newText);
}
