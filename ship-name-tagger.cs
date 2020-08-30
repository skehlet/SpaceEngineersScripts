public Program()
{
    Echo("Renamer v1.0");
}


public void Main(string argument, UpdateType updateSource)
{
    if (String.IsNullOrEmpty(Me.CustomData)) {
        Echo("Error, provide ship tag name in Custom Data");
        return;
    }

    string ShipTag = Me.CustomData;
    Echo($"Using {ShipTag} for ship tag");

    var blocks = FilterBlocks<IMyTerminalBlock>();
    Echo($"Found {blocks.Count} blocks");
    foreach (var block in blocks)
    {
        if (!block.CustomName.StartsWith(ShipTag + " ")) {
            Echo("renaming: " + block.CustomName);
            block.CustomName = ShipTag + " " + block.CustomName;
        }
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
