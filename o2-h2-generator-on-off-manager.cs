IMyTextSurface myScreen = null;
int logCounter = 1;
const float FONT_SIZE = 1.0F;
double lastRunTimeMs = 0;
List<IMyGasGenerator> generators = null;

public Program()
{
	myScreen = Me.GetSurface(0);
    myScreen.ContentType = ContentType.TEXT_AND_IMAGE;
    myScreen.FontSize = FONT_SIZE;
    myScreen.Alignment = VRage.Game.GUI.TextPanel.TextAlignment.LEFT;

    Log("O2/H2 Generator On/Off Manager v0.1");
    Runtime.UpdateFrequency = UpdateFrequency.Update100;
}

public void Main(string argument, UpdateType updateSource)
{
    double now = GetTimeInMs();
    if (now - lastRunTimeMs < 5000) {
        return;
    }
    Log($"Running at {now}");
    lastRunTimeMs = now;

    ManageGenerators();
}

void ManageGenerators()
{
    generators = FindGenerators();

    double averageFillRatio = GetAverageFillRatio();
    if (averageFillRatio > 0.9) {
        Log($"Disabling generators ({averageFillRatio})");
        DisableGenerators();
    } else {
        Log($"Enabling generators ({averageFillRatio})");
        EnableGenerators();
    }
}

List<IMyGasGenerator> FindGenerators()
{
    return FilterBlocks<IMyGasGenerator>(generator => {
        // Log($"Generator: {generator.DetailedInfo}");
        if (!generator.IsFunctional) {
            Log($"Skipping {generator.Name}, not functional");
            return false;
        }
        return true;
    });
}

double GetAverageFillRatio()
{
    List<IMyGasTank> tanks = FindTanks();
    double sum = 0;
    double avg = 0;
    int count = 0;
    tanks.ForEach(tank => {
        sum += tank.FilledRatio;
        count++;
    });
    if (count > 0) {
        avg = sum / count;
    }
    Log($"{count} tanks, fill avg: {avg}");
    return avg;
}

List<IMyGasTank> FindTanks()
{
    return FilterBlocks<IMyGasTank>(tank => {
        // Log($"Tank: {tank.DetailedInfo}");
        if (!tank.IsWorking) {
            Log($"Skipping {tank.Name}, not working");
            return false;
        }
        return true;
    });
}




void Enable(IMyFunctionalBlock block) {
    block.Enabled = true;
}

void Disable(IMyFunctionalBlock block) {
    block.Enabled = false;
}

void EnableGenerators() {
    generators.ForEach(Enable);
}

void DisableGenerators() {
    generators.ForEach(Disable);
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
