// Notes on terminology:
// The drilling area is broken into a number of zones in an XY grid.
// The length of each X/Y dimension of the grid is the starting position (all pistons retracted) plus 4 blocks for each extended piston.
// Grid Coordinates are X+Y values, zero-based, starting in the lower left corner.
// Zones are numbered starting with one in the lower left corner.
// The position of each zone is the number of meters in each of the X and Y directions.

List<IMyExtendedPistonBase> xPistons = null;
List<IMyExtendedPistonBase> yPistons = null;
List<IMyExtendedPistonBase> zPistons = null;
IMyTextSurface myScreen = null;
int logCounter = 1;
const float FONT_SIZE = 1.0F;
List<int> ZONES = new List<int>();

public Program()
{
	myScreen = Me.GetSurface(0);
    myScreen.ContentType = ContentType.TEXT_AND_IMAGE;
    myScreen.FontSize = FONT_SIZE;
    myScreen.Alignment = VRage.Game.GUI.TextPanel.TextAlignment.LEFT;

    DiscoverPistons();
    xPistons.ForEach(p => Log($"X: {p.CustomName}"));
    yPistons.ForEach(p => Log($"Y: {p.CustomName}"));
    zPistons.ForEach(p => Log($"Z: {p.CustomName}"));

    // Runtime.UpdateFrequency = UpdateFrequency.Update100;
}

public void Main(string argument, UpdateType updateSource)
{
    if (argument.ToUpper() == "INIT") {
        InitZones();
    } else if (argument.ToUpper() == "ZONES") {
        ZONES.ForEach(zone => {
            float[] pos = GetPositionForZone(zone);
            float x = pos[0];
            float y = pos[1];
            Log($"Zone {zone}: [{x}, {y}]");
        });
    } else if (argument.ToUpper() == "PISTONS") {
        DiscoverPistons();
        // xPistons.ForEach(p => Log($"X: {p.CustomName}"));
        // yPistons.ForEach(p => Log($"Y: {p.CustomName}"));
        // zPistons.ForEach(p => Log($"Z: {p.CustomName}"));
        Log($"{xPistons.Count} X pistons");
        Log($"{yPistons.Count} Y pistons");
        Log($"{zPistons.Count} Z pistons");
    }
}

/*
The "Z" pistons are the ones with "down" pointing the same way as the Programmable Block's "up"
The first "X" piston is the piston closest to me (that's not a Z), it and all remaining pistons with the same orientation are the "X" pistons
The "Y" pistons are whatever's left.
*/
void DiscoverPistons() {
    float closeEnough = 0.1F;

    zPistons = FilterBlocks<IMyExtendedPistonBase>(piston => {
        return IsZero(Me.WorldMatrix.Up - piston.WorldMatrix.Down, closeEnough);
    });

    IMyExtendedPistonBase closestPiston = FilterBlocks<IMyExtendedPistonBase>(piston => !zPistons.Contains(piston))
        .OrderBy(piston => vecToRange(Me.GetPosition(), piston.GetPosition()))
        .First();
    if (closestPiston == null) {
        throw new Exception("Unable to find closest piston");
    }

    xPistons = FilterBlocks<IMyExtendedPistonBase>(piston => {
        return IsZero(closestPiston.WorldMatrix.Down - piston.WorldMatrix.Down, closeEnough);
    });

    IMyExtendedPistonBase firstY = null;
    yPistons = FilterBlocks<IMyExtendedPistonBase>(piston => {
        if (xPistons.Contains(piston) || zPistons.Contains(piston)) {
            return false;
        }
        if (firstY == null) {
            firstY = piston;
            return true;
        }
        return IsZero(firstY.WorldMatrix.Down - piston.WorldMatrix.Down, closeEnough);
    });
}


// from Cats' cats-ray code
double vecToRange( Vector3D tar, Vector3D org ) { 
    return Math.Sqrt(Math.Pow( tar.X - org.X, 2 ) + Math.Pow( tar.Y - org.Y, 2 ) + Math.Pow( tar.Z - org.Z, 2 )); 
} 



// from Whip's code
public bool IsZero(Vector3D v, double epsilon = 1e-4)
{
    if (Math.Abs(v.X) > epsilon) return false;
    if (Math.Abs(v.Y) > epsilon) return false;
    if (Math.Abs(v.Z) > epsilon) return false;
    return true;
}



void InitZones() {
    ZONES.Clear();

    // find the middle. work around in a spiral fashion, staying within the confines of the available zones

    int xAxisLength = 1 + xPistons.Count * 4;
    int yAxisLength = 1 + yPistons.Count * 4;
    int numZones = xAxisLength * yAxisLength;

    int x = (int)Math.Floor(1.0 * xAxisLength / 2);
    int y = (int)Math.Floor(1.0 * yAxisLength / 2);

    // add the middle zone
    int zone = GetZoneAtGridCoords(x, y);
    Log($"Middle zone {zone} at [{x}, {y}]");
    ZONES.Add(zone);

    // then spiral out from it
    bool hopAlongX = true;
    bool hopPositively = true;
    int hopsToDo = 1;

    while (ZONES.Count < numZones) {
        int hopsDone = 0;

        while (hopsDone++ < hopsToDo) {
            if (hopAlongX) {
                x += hopPositively ? 1 : -1;
            } else {
                y += hopPositively ? 1 : -1;
            }

            zone = GetZoneAtGridCoords(x, y);
            if (zone == -1) {
                Log($"Skipping invalid coords [{x}, {y}]");
            } else {
                Log($"+zone {zone} at [{x}, {y}]");
                ZONES.Add(zone);
            }
        }

        if (!hopAlongX) {
            // increase number of hops and flip direction after completing y hops
            hopsToDo++;
            hopPositively = !hopPositively;
        }
        hopAlongX = !hopAlongX;
    }
}

// Returns -1 on invalid zone.
int GetZoneAtGridCoords(int x, int y) {
    int xAxisLength = 1 + xPistons.Count * 4;
    int yAxisLength = 1 + yPistons.Count * 4;

    if (x < 0 || y < 0 || x > xAxisLength - 1 || y > yAxisLength - 1) {
        return -1;
    }

    return (x + 1) + (y * xAxisLength);
}

float[] GetPositionForZone(int zone) {
    zone--; // make zone zero-based, easier to do the math
    int xAxisLength = 1 + xPistons.Count * 4;
    int xSpot = zone % xAxisLength;
    // Each spot past the first requires extending the piston by one block size (2.5m)
    float x = xSpot * 2.5F;

    int yAxisLength = 1 + yPistons.Count * 4;
    int ySpot = (int)Math.Floor(1.0 * zone / xAxisLength);
    float y = ySpot * 2.5F;

    // Log($"xAxisLength: {xAxisLength}, xSpot: {xSpot}, x: {x}");
    // Log($"yAxisLength: {yAxisLength}, ySpot: {ySpot}, y: {y}");

    return new float[] { x, y };
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

public List<T> FilterBlocks<T>(Func<T, Boolean> filter = null) where T : class, IMyTerminalBlock
{
    var blocks = new List<T>();
    GridTerminalSystem.GetBlocksOfType(blocks, x => {
        if (!x.IsSameConstructAs(Me)) return false;
        return (filter == null) || filter(x);
    });
    return blocks.ConvertAll(x => (T)x);
}
