IMyRadioAntenna antenna = null;
List<IMyInventory> inventories = new List<IMyInventory>();
IMyTextSurface myScreen = null;
const float FONT_SIZE = 0.75F;
int logCounter = 1;

public Program()
{
	myScreen = Me.GetSurface(0);
    myScreen.ContentType = ContentType.TEXT_AND_IMAGE;
    myScreen.FontSize = FONT_SIZE;
    myScreen.Alignment = VRage.Game.GUI.TextPanel.TextAlignment.LEFT;

    antenna = GetFirstBlockOfType<IMyRadioAntenna>(b => b.CustomName.Contains("[VOL]"));
    if (antenna == null) {
        Log("Error: No Antenna with [VOL] found, go add one.");
        return;
    }
    Log("Using Antenna: " + antenna.CustomName);
    DiscoverInventories();
    if (inventories.Count == 0) return;
    Runtime.UpdateFrequency = UpdateFrequency.Update100;
}

public void Main(string argument, UpdateType updateSource)
{
    StringBuilder sb = new StringBuilder();
    MyFixedPoint currentVolume = 0;
    MyFixedPoint maxVolume = 0;

    foreach (var inv in inventories) {
        currentVolume += inv.CurrentVolume;
        maxVolume += inv.MaxVolume;
    }

    double percentage = 100.0 * currentVolume.ToIntSafe() / maxVolume.ToIntSafe();

    var result = "";
    if (!String.IsNullOrEmpty(Me.CustomData)) {
        result = Me.CustomData + " ";
    }
    result += $"{(double)currentVolume:0.0}kL" + " " + $"{percentage:0.0}%";
    antenna.HudText = result;
}

public void DiscoverInventories()
{
    FilterBlocks<IMyCargoContainer>().ForEach(AddInventories);
    FilterBlocks<IMyCockpit>().ForEach(AddInventories);
    FilterBlocks<IMyShipDrill>().ForEach(AddInventories);
}

void AddInventories(IMyTerminalBlock b)
{
    Log($"Found: {b.DisplayNameText}");
    for (int i = 0; i < b.InventoryCount; i++) {
        inventories.Add(b.GetInventory(i));
    }
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
    // only keep most recent 25 lines
    IEnumerable<string> oldLines = oldText.Split(new string[] {"\r\n","\n"}, StringSplitOptions.RemoveEmptyEntries).Take(25);
    string newText = message + Environment.NewLine.ToString()
        + string.Join(Environment.NewLine.ToString(), oldLines);
    myScreen.WriteText(newText);
}
