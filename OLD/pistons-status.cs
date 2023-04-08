List<IMyExtendedPistonBase> pistons = null;
List<IMyTextPanel> textPanels = null;

const float FONT_SIZE = 1.0F;

public Program()
{
    textPanels = FilterBlocks<IMyTextPanel>(b => b.CustomName.Contains("[PISTONS]"));
    if (textPanels.Count() == 0) {
        Echo("Warning: No Text Panel(s) with [PISTONS] found. Add one or more.");
    }
    textPanels.ForEach(textPanel => {
        textPanel.ContentType = ContentType.TEXT_AND_IMAGE;
        textPanel.FontSize = FONT_SIZE;
        textPanel.Alignment = VRage.Game.GUI.TextPanel.TextAlignment.LEFT;
        Echo($"Using Text Panel: {textPanel.CustomName}");
    });

    pistons = FilterBlocks<IMyExtendedPistonBase>().OrderBy(p => p.CustomName).ToList();
    Runtime.UpdateFrequency = UpdateFrequency.Update100;
}

public void Main(string argument, UpdateType updateSource)
{
    StringBuilder sb = new StringBuilder();

    pistons.ForEach(p => {
        sb.AppendLine($"{p.CustomName}: {p.CurrentPosition}");
    });

    textPanels.ForEach(p => {
        p.WriteText(sb.ToString());
    });
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
