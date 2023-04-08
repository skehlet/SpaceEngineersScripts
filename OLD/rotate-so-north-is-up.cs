IMyTextSurface myScreen = null;
IMyTextSurface myLog = null;
IMyRemoteControl remoteControl;
IMyProgrammableBlock samProgrammableBlock = null;
int logCounter = 1;
const float FONT_SIZE = 1.0F;
// double lastRunTimeMs = 0;

enum State {
    STOPPED, GOTO_1, GOTO_2, GOTO_3, GOTO_4, DONE
}
State state = State.STOPPED;

string[] gpses = new string[] {
    "",
    "GPS:ithilyn #1:115138.06:33034.27:5689582.65:#FF75C9F1:",
    "GPS:ithilyn #2:115139.98:33052.32:5689588.56:#FF75C9F1:",
    "GPS:ithilyn #3:115167.27:33051.1:5689583.45:#FF75C9F1:",
    "GPS:ithilyn #4:115165.36:33033.12:5689577.56:#FF75C9F1:"
};



public Program()
{
	myScreen = Me.GetSurface(0);
    SetupSurfaceForText(myScreen);

    Log("Rotate so North is up v1.0");

    IMyTextPanel panel = GetFirstBlockOfType<IMyTextPanel>(b => b.CustomName.Contains("[PATHING"));
    if (panel != null) {
        Log($"Using log: {panel.CustomName}");
        // Turns out TextPanels are actually TextSurfaceProviders with one surface?
        myLog = ((IMyTextSurfaceProvider)panel).GetSurface(0);
        SetupSurfaceForText(myLog);
    }

    remoteControl = GetFirstBlockOfType<IMyRemoteControl>();
    if (remoteControl == null) {
        Log("Error: No remote control block found, go add one.");
        return;
    }
    Log($"Using remote control: {remoteControl.CustomName}");

    samProgrammableBlock = GetFirstBlockOfType<IMyProgrammableBlock>(b => b.CustomName.Contains("[SAM"));
    if (samProgrammableBlock == null) {
        Log("Error: No programmable block with [SAM found, go add one.");
        return;
    }
    Log($"Using SAM programmable block: {samProgrammableBlock.CustomName}");

    // Runtime.UpdateFrequency = UpdateFrequency.Update100;
}

public void Main(string argument, UpdateType updateSource)
{
    // double now = GetTimeInMs();
    // if (now - lastRunTimeMs < 5000) {
    //     return;
    // }
    // Log($"{now}: source: {updateSource}");
    // lastRunTimeMs = now;

    if (updateSource == UpdateType.Terminal) {
        if (argument.ToUpper() == "BEGIN") {
            MoveTo1();
        }
    } else if (updateSource == UpdateType.Trigger) {
        Log($"I was triggered, current state: {state}");
        switch (state) {
            case State.STOPPED: MoveTo1(); break;
            case State.GOTO_1:  MoveTo2(); break;
            case State.GOTO_2:  MoveTo3(); break;
            case State.GOTO_3:  MoveTo4(); break;
            case State.GOTO_4:  Done(); break;
        }
    }
}

bool MoveTo1() {
    Log("Moving to #1");
    state = State.GOTO_1;
    return TellSamToGoToGps(gpses[1]);
}

bool MoveTo2() {
    Log("Moving to #2");
    state = State.GOTO_2;
    return TellSamToGoToGps(gpses[2]);
}

bool MoveTo3() {
    Log("Moving to #3");
    state = State.GOTO_3;
    return TellSamToGoToGps(gpses[3]);
}

bool MoveTo4() {
    Log("Moving to #4");
    state = State.GOTO_4;
    return TellSamToGoToGps(gpses[4]);
}

bool TellSamToGoToGps(string gps) {
    return samProgrammableBlock.TryRun($"START {gps}");
}

void Done() {
    state = State.STOPPED;
    Log("All done");
}


// void DoCrap() {
//     StringBuilder sb = new StringBuilder();

//     Vector3D wGps = remoteControl.GetPosition();
//     sb.AppendLine($"GPS X: {wGps.X:0.00}");
//     sb.AppendLine($"GPS Y: {wGps.Y:0.00}");
//     sb.AppendLine($"GPS Z: {wGps.Z:0.00}");

//     // "Give me the GPS (world position) 3m x, 6m y, and 9m z from my current position"
//     Vector3D relPos = new Vector3D(3, 6, 9);
//     Vector3D wPos = Vector3D.Transform(relPos, remoteControl.WorldMatrix);
//     sb.AppendLine("Rel pos (3, 6, 9) -> World pos:");
//     sb.AppendLine($"wPos X: {wPos.X:0.00}");
//     sb.AppendLine($"wPos Y: {wPos.Y:0.00}");
//     sb.AppendLine($"wPos Z: {wPos.Z:0.00}");

//     WriteToSurface(myScreen, sb.ToString());
// }



// List<IMyAssembler> FindAssemblers()
// {
//     return FilterBlocks<IMyAssembler>(assembler => {
//         //Log($"assembler: {assembler.DetailedInfo}");
//         if (!assembler.IsWorking) {
//             Log($"Skipping {assembler.Name}, not working");
//             return false;
//         }
//         if (assembler.DetailedInfo.Contains("Type: Survival Kit")) {
//             Log($"Skipping {assembler.Name}, is Survival kit");
//             return false; // filter out Survival kits
//         }
//         // if (!assembler.DetailedInfo.Contains("Type: Assembler")) {
//         //     Log($"Skipping {assembler.Name}, is Survival kit?");
//         //     return false; // filter out Survival kits
//         // }
//         if (assembler.Mode != MyAssemblerMode.Assembly) {
//             Log($"Skipping {assembler.Name}, in disassembly mode");
//             return false;
//         }
//         return true;
//     });
// }


// string GetShortName(MyDefinitionId longName)
// {
//     return longName.ToString().Split('/')[1]; // TODO error checks
// }

// void LoadBalanceAssemblers()
// {
//     // Sum up the number of item in all Assemblers.
//     // Find the average (round up).
//     // Sort Assemblers by item count, largest first.
//     // For each assembler:
//     //      Get a sum of the number of items
//     //      If it's more than the average:
//     //          Start walking through the items and counting until you hit the average. Move all excess items to the holding bin.
//     //      Else (If it's less than the average):
//     //          Calculate how many more items it should have, then pull those from the holding bin.
//     List<IMyAssembler> assemblers = FindAssemblers();
//     List<MyProductionItem> myItems = new List<MyProductionItem>();

//     if (assemblers.Count == 0) {
//         Log("No assemblers found");
//         return;
//     }

//     int allItemCount = assemblers.Select(assembler => {
//         assembler.GetQueue(myItems);
//         return myItems.Select(item => (int)item.Amount).Sum();
//     }).Sum();

//     int average = (int)Math.Ceiling(1.0 * allItemCount / assemblers.Count);
//     Log($"Tot: {allItemCount}/{assemblers.Count} Avg: {average}");

//     List<IMyAssembler> sortedAssemblers = assemblers.OrderByDescending(assembler => {
//         assembler.GetQueue(myItems);
//         return myItems.Select(item => (int)item.Amount).Sum();
//     }).ToList();

//     List<MyProductionItem> holdingBin = new List<MyProductionItem>();

//     sortedAssemblers.ForEach(assembler => {
//         assembler.GetQueue(myItems);
//         int myTotalItemCount = myItems.Select(item => (int)item.Amount).Sum();
//         Log($"{myTotalItemCount}/{average}: {assembler.CustomName}");

//         if (myTotalItemCount > average) {
//             int myItemCount = 0;
//             for (int myItemIdx = 0; myItemIdx < myItems.Count; myItemIdx++) {
//                 MyProductionItem myItem = myItems[myItemIdx];

//                 if (myItemCount < average) {
//                     int spaceAvailable = average - myItemCount;
//                     if (myItem.Amount <= spaceAvailable) {
//                         Log($"{myItemIdx}: OK {myItem.Amount} {GetShortName(myItem.BlueprintId)}");
//                         myItemCount += (int)myItem.Amount;
//                     } else {
//                         // must reduce this item and put excess into holding bin
//                         Log($"{myItemIdx}: ONLY {spaceAvailable}/{myItem.Amount} {GetShortName(myItem.BlueprintId)}");
//                         int excess = (int)myItem.Amount - spaceAvailable;
//                         assembler.RemoveQueueItem(myItemIdx, (double)excess);
//                         myItemCount += spaceAvailable;
//                         MyProductionItem myNewItem = new MyProductionItem(myItem.ItemId, myItem.BlueprintId, excess);
//                         holdingBin.Add(myNewItem);
//                     }
//                 } else {
//                     // all to holding bin
//                     Log($"{myItemIdx}: XS {myItem.Amount} {GetShortName(myItem.BlueprintId)}");
//                     assembler.RemoveQueueItem(myItemIdx, (double)myItem.Amount);
//                     holdingBin.Add(myItem);
//                 }
//             }
//         } else {
//             int takeCount = average - myTotalItemCount;
//             while (takeCount > 0 && holdingBin.Count > 0) {
//                 MyProductionItem myItem = holdingBin[0];
//                 holdingBin.RemoveAt(0);

//                 if (myItem.Amount <= takeCount) {
//                     // take it all
//                     Log($"ADD {myItem.Amount} {GetShortName(myItem.BlueprintId)}");
//                     assembler.AddQueueItem(myItem.BlueprintId, myItem.Amount);
//                     takeCount -= (int)myItem.Amount;

//                 } else {
//                     // take only part and put the rest back
//                     Log($"ADD {takeCount}/{myItem.Amount} {GetShortName(myItem.BlueprintId)}");
//                     assembler.AddQueueItem(myItem.BlueprintId, (double)takeCount);
//                     MyProductionItem myNewItem = new MyProductionItem(myItem.ItemId, myItem.BlueprintId, myItem.Amount - takeCount);
//                     holdingBin.Insert(0, myNewItem);
//                     break;
//                 }
//             }
//         }
//     });

//     if (holdingBin.Count != 0) {
//         // should not happen
//         Log($"ERROR: Len holding bin: {holdingBin.Count}");
//     }
// }

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

void SetupSurfaceForText(IMyTextSurface surface) {
    surface.ContentType = ContentType.TEXT_AND_IMAGE;
    surface.FontSize = FONT_SIZE;
    surface.Alignment = VRage.Game.GUI.TextPanel.TextAlignment.LEFT;
}

public void Log(string message)
{
    message = $"{logCounter++}: {message.Trim()}";
    Echo(message);
    // PrependTextToSurface(myScreen, message);
    if (myLog != null) {
        // myLog.ContentType = ContentType.TEXT_AND_IMAGE;
        // myLog.FontSize = 1.5F;
        // myLog.Alignment = VRage.Game.GUI.TextPanel.TextAlignment.LEFT;
        PrependTextToSurface(myLog, message);
    } else {
        Echo("myLog is null, cannot log");
    }
}

void PrependTextToSurface(IMyTextSurface surface, string message) {

    // var actualSurface = surface is IMyTextSurface
    //     ? (IMyTextSurface)surface
    //     : ((IMyTextSurfaceProvider)surface).GetSurface(0);


    string oldText = surface.GetText();
    // only keep most recent 50 lines
    IEnumerable<string> oldLines = oldText.Split(
        new string[] {"\r\n","\n"},
        StringSplitOptions.RemoveEmptyEntries
    ).Take(50);
    // Echo($"Found {oldLines.Count()} old lines, first line: {oldLines.First()}");
    string newText = message +
        Environment.NewLine.ToString() +
        string.Join(Environment.NewLine.ToString(), oldLines);
    // Echo($"Writing {newText.Count()} old lines");
    surface.WriteText(newText);
}

// void AppendTextToSurface(IMyTextSurface surface, string message) {
//     // string oldText = surface.GetText();
//     // // only keep most recent 50 lines
//     // IEnumerable<string> oldLines = oldText.Split(
//     //     new string[] {"\r\n","\n"},
//     //     StringSplitOptions.RemoveEmptyEntries
//     // ).Reverse().Take(50).Reverse(); // TakeLast() doesn't work?
//     // // Echo($"Found {oldLines.Count()} old lines");
//     // string newText = string.Join(Environment.NewLine.ToString(), oldLines) +
//     //     Environment.NewLine.ToString() + message;
//     // // Echo($"Writing {newText.Count()} old lines");
//     // surface.WriteText(newText);


//     surface.WriteText(message, true);
// }



// public void WriteToSurface(IMyTextSurface surface, string message)
// {
//     if (surface == null) {
//         return;
//     }
//     surface.ContentType = ContentType.TEXT_AND_IMAGE;
//     surface.FontSize = 1.5F;
//     surface.Alignment = VRage.Game.GUI.TextPanel.TextAlignment.LEFT;
//     surface.WriteText(message);
// }

