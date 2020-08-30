const float FONT_SIZE = 7.0F;

List<IMyTextPanel> textPanels = null;
List<IMyInventory> inventories = new List<IMyInventory>();

public Program()
{
    textPanels = FilterBlocks<IMyTextPanel>(b => b.CustomName.Contains("[VOL]"));
    if (textPanels.Count() == 0) {
        Echo("Error: No Text Panel(s) with [VOL] found. Add one or more.");
        return;
    }
    textPanels.ForEach(textPanel => {
        textPanel.ContentType = ContentType.TEXT_AND_IMAGE;
        textPanel.FontSize = FONT_SIZE;
        textPanel.Alignment = VRage.Game.GUI.TextPanel.TextAlignment.LEFT;
        Echo("Using Text Panel: " + textPanel.CustomName);
    });
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

    if (!String.IsNullOrEmpty(Me.CustomData)) {
        sb.AppendLine(Me.CustomData);
    }
    sb.AppendLine($"{(double)currentVolume:0.0}kL");
    sb.AppendLine($"{percentage:0.0}%");

    textPanels.ForEach(textPanel => textPanel.WriteText(sb.ToString()));
}

public void DiscoverInventories()
{
    FilterBlocks<IMyCargoContainer>().ForEach(AddInventories);
    FilterBlocks<IMyCockpit>().ForEach(AddInventories);
    FilterBlocks<IMyShipDrill>().ForEach(AddInventories);
}

void AddInventories(IMyTerminalBlock b)
{
    Echo($">> {b.DisplayNameText}");
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
