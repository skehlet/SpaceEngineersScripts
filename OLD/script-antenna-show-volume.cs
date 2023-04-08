IMyRadioAntenna antenna = null;
List<IMyInventory> inventories = new List<IMyInventory>();

T GetFirstBlockOfType<T>() where T : class, IMyTerminalBlock
{
    var blocks = new List<T>();
    GridTerminalSystem.GetBlocksOfType(blocks);
    foreach (var block in blocks)
    {
        if (block.IsSameConstructAs(Me)) return block;
    }
    return null;
}

public void DiscoverInventories()
{
    List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
    GridTerminalSystem.GetBlocks(blocks);
    foreach (var block in blocks) {
        //if (block is IMyFunctionalBlock functionalBlock && !functionalBlock.Enabled) continue;
//		if (!block.IsWorking) continue;
        if (!block.IsSameConstructAs(Me)) continue; // only my ship
//		if (block is IMyReactor reactor) continue; // Exclude reactors
//		if (block.HasInventory) {
//			for (int i = 0; i < block.InventoryCount; i++) {
//				var inv = block.GetInventory(i);
//				inventories.Add(inv);
//			}
//		}

        String name = block.DisplayNameText;
        if (name.Contains("Medium Cargo Container")
            || name.Contains("Small Cargo Container")
            || name.Contains("Fighter Cockpit")
            || name.Contains("Cockpit")
            || name.Contains("Industrial Cockpit")
            || name.Contains("Drill")) {
            if (block.HasInventory) {
                Echo(":: " + block.DisplayNameText);
                for (int i = 0; i < block.InventoryCount; i++) {
                    var inv = block.GetInventory(i);
                    inventories.Add(inv);
                }
            }
        }
    }
}

public Program()
{
    antenna = GetFirstBlockOfType<IMyRadioAntenna>();
    if (antenna == null) {
        Echo("No antenna found");
        return;
    }
    Echo("Found antenna: " + antenna.CustomName);
    DiscoverInventories();
    if (inventories.Count == 0) return;
    Runtime.UpdateFrequency = UpdateFrequency.Update100;
}

public void Main(string argument, UpdateType updateSource)
{
    VRage.MyFixedPoint currentVolume = 0; 
    VRage.MyFixedPoint maxVolume = 0; 

    foreach (var inv in inventories) {
        currentVolume += inv.CurrentVolume; 
        maxVolume += inv.MaxVolume; 
    }

    double percentage = ((double)currentVolume / (double)maxVolume) * 100;
    double dbcurrentVolume = Math.Round((double)currentVolume, 2);
    double dbmaxVolume = Math.Round((double)maxVolume, 2);

    var result = "";
    if (!String.IsNullOrEmpty(Me.CustomData)) {
        result = Me.CustomData + " ";
    }
    result += dbcurrentVolume.ToString() + "kL " + percentage.ToString("0.00") + "%";
    antenna.HudText = result;
}

