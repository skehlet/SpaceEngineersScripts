string OLDNAME = "NickelHauler";

public Program()
{
    Echo("Renamer v1.1");
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

    if (!String.IsNullOrEmpty(OLDNAME)) {
        blocks.ForEach(b => {
            if (b.CustomName.StartsWith($"{OLDNAME} ")) {
                Echo($"Removing {OLDNAME} from {b.CustomName}");
                b.CustomName = b.CustomName.Remove(0, OLDNAME.Length + 1); // +1 for the space after the name
            }
        });
    }

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
