/*
Notes:
- One drill at the end of the z pistons.
- This script will adapt to however many x, y, and z pistons you have.
- Piston orientations are auto detected. You only need to name them if you want
  them extended "in order" instead of randomly.
- You have to figure out how tall it needs to be, see notes.

When setting up a new station:
- Set all pistons to have Share inertia tensor.
- On the Sorter for the Ejector, remember to set a Whitelist with Stone and Ice,
  and select Drain All.
- On the Ejector Connector, set Throw Out: On.
- On the ship Connector, set a Blacklist with Stone and Ice, and set Drain All
  (to pull from the Drills).
- I added support to tag only the drills+pistons+cargo containers you want it to
  consider. Add your tag value to the Programmable Block's CustomData (without
  any square brackets). Then, on each piston+drill+cargo, add [YOUR-TAG] to the
  Name field. You will need to run the command "RESET" and recompile after doing
  this.

When activating the programmable block: Toggle block on Recompile You should see
it align to [15, 15] (or your middle if not 3 x and y pistons) and then begin
drilling.

To park it so it can be blueprinted: Run with argument: RESET
*/
List<IMyExtendedPistonBase> xPistons = null;
List<IMyExtendedPistonBase> yPistons = null;
List<IMyExtendedPistonBase> zPistons = null;
List<IMyExtendedPistonBase> zPausedPistons = null;
List<IMyShipDrill> drills = null;
enum Mode {
    PARK, BEGIN, ALIGN, DRILL, PAUSE, DONE
}
enum AlignPhase {
    Z_START, Z_FINISH, XY_START, XY_FINISH
}
Mode mode = Mode.BEGIN;
AlignPhase phase = AlignPhase.Z_START;
int zInProgress = -1;
List<int> ZONES = new List<int>();
bool isParking = false;
int PISTON_MAX_LENGTH = 10; // default is 10m; some mods provide other piston types

List<IMyInventory> inventories = new List<IMyInventory>();
int MAX_INV_PERCENTAGE = 90;
const float INITIAL_SPEED = 0.5F; // this should be positive
IMyTextSurface myScreen = null;
const float FONT_SIZE = 0.75F;
int logCounter = 1;
string filterTag = null;

public Program()
{
	myScreen = Me.GetSurface(0);
    myScreen.ContentType = ContentType.TEXT_AND_IMAGE;
    myScreen.FontSize = FONT_SIZE;
    myScreen.Alignment = VRage.Game.GUI.TextPanel.TextAlignment.LEFT;

    filterTag = Me.CustomData.Trim();
    if (!String.IsNullOrEmpty(filterTag)) {
        Log($"Tag: {filterTag}");
    }

    DiscoverPistons();
    DisablePistons();
    xPistons.ForEach(p => p.Velocity = INITIAL_SPEED);
    yPistons.ForEach(p => p.Velocity = INITIAL_SPEED);
    zPistons.ForEach(p => p.Velocity = INITIAL_SPEED);
    Log($"Found: {xPistons.Count} X pistons");
    Log($"Found: {yPistons.Count} Y pistons");
    Log($"Found: {zPistons.Count} Z pistons");

    DiscoverDrills();
    DisableDrills();
    Log($"Found: {drills.Count} drills");
    if (drills.Count == 0) {
        Log("Error, no drills found");
        return;
    }

    DiscoverInventories();
    Log($"Found: {inventories.Count} inventories");

    if (IsFirstTime()) {
        InitZones();
        Log($"First time, initialized zones");
    } else {
        LoadZonesFromStorage();
        Log("Loaded ZONES: " + String.Join(", ", ZONES));
    }

    Runtime.UpdateFrequency = UpdateFrequency.Update100;
}

public void Main(string argument, UpdateType updateSource)
{
    if (argument.ToUpper() == "RESET") {
        Log("Resetting ZONES and parking");
        InitZones();
        mode = Mode.PARK;
        return;
    }
    if (argument.ToUpper() == "ZONES") {
        Log($"{ZONES.Count} ZONES: " + String.Join(", ", ZONES));
        return;
    } else if (argument.ToUpper() == "NEXT") {
        ZONES.RemoveAt(0);
        mode = Mode.BEGIN;
        return;
    }
    switch (mode) {
        case Mode.PARK:  DoParkMode(); break;
        case Mode.BEGIN: DoBeginMode(); break;
        case Mode.ALIGN: DoAlignMode(); break;
        case Mode.DRILL: DoDrillMode(); break;
        case Mode.PAUSE: DoPauseMode(); break;
    }
}

public void Save()
{
    List<String> storageData = new List<String>();
    storageData.Add($"ZONES={String.Join(",", ZONES)}");
    Storage = String.Join(";", storageData);
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
    Log($"Found: {zPistons.Count} Z pistons");
    if (zPistons.Count == 0) {
        throw new Exception("No Z pistons found");
    }

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

bool IsFirstTime() {
    return String.IsNullOrEmpty(Storage);
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
                // Normal to see one of these in the output, the last one
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

int GetCurrentZone() {
    return ZONES.Count > 0 ? ZONES[0] : -1;
}

float[] GetPositionForZone(int zone) {
    zone--; // make the given zone zero-based, easier to do the math
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

void LoadZonesFromStorage() {
    string[] storedData = Storage.Split(';');
    foreach (var data in storedData) {
        if (data.StartsWith("ZONES=")) {
            string[] zonePieces = data.Split('=');
            string[] zoneStrings = zonePieces[1].Split(',');
            foreach (var zoneString in zoneStrings) {
                int zone;
                if (int.TryParse(zoneString, out zone)) {
                    ZONES.Add(zone);
                }
            }
        }
    }
}

float[] GetCurrentPosition() {
    float x = xPistons.Select(p => p.CurrentPosition).Sum();
    float y = yPistons.Select(p => p.CurrentPosition).Sum();
    float[] pos = new float[2];
    pos[0] = x;
    pos[1] = y;
    return pos;
}

void DoParkMode() {
    isParking = true;
    ZONES.Insert(0, 1);
    DisableDrills();
    DisablePistons();
    mode = Mode.ALIGN;
    phase = AlignPhase.Z_START;
}

void DoneParking() {
    Log("Done parking, inactivating");
    isParking = false;
    ZONES.RemoveAt(0);
    mode = Mode.DONE;
    Runtime.UpdateFrequency = UpdateFrequency.None;
}

void DoBeginMode() {
    if (ZONES.Count == 0) {
        Log("ALL ZONES DRILLED");
        mode = Mode.PARK;
    } else if (IsAlignedToCurrentZone()) {
        Log("We are aligned, now drilling");
        mode = Mode.DRILL;
    } else {
        Log($"Aligning to zone {GetCurrentZone()}");
        DisableDrills();
        DisablePistons();
        mode = Mode.ALIGN;
        phase = AlignPhase.Z_START;
    }
}

bool IsAlignedToCurrentZone() {
    float[] expectedPosition = GetPositionForZone(GetCurrentZone());
    float[] actualPosition = GetCurrentPosition();
    return (expectedPosition[0] == actualPosition[0] && expectedPosition[1] == actualPosition[1]);
}

void DoAlignMode() {
    if (phase == AlignPhase.Z_START) {
        Log("Retracting Zs");
        zPistons.ForEach(RetractFully);
        phase = AlignPhase.Z_FINISH;

    } else if (phase == AlignPhase.Z_FINISH) {
        var doneRetracting = zPistons.FindAll(IsPistonDone);
        if (doneRetracting.Count == zPistons.Count) {
            // done
            zPistons.ForEach(Disable);
            Log("All Z pistons retracted");
            phase = AlignPhase.XY_START;
        }

    } else if (phase == AlignPhase.XY_START) {
        float[] desiredPosition = GetPositionForZone(GetCurrentZone());
        float xLengthRemaining = desiredPosition[0];
        float yLengthRemaining = desiredPosition[1];
        Log($"Aligning to zone {GetCurrentZone()}: [{xLengthRemaining}, {yLengthRemaining}]");

        xPistons.ForEach(p => {
            if (xLengthRemaining > 0) {
                float thisPistonsLength = Math.Min(PISTON_MAX_LENGTH, xLengthRemaining);
                SetPistonLength(p, thisPistonsLength);
                xLengthRemaining -= thisPistonsLength;
            } else {
                SetPistonLength(p, 0);
            }
        });
        yPistons.ForEach(p => {
            if (yLengthRemaining > 0) {
                float thisPistonsLength = Math.Min(PISTON_MAX_LENGTH, yLengthRemaining);
                SetPistonLength(p, thisPistonsLength);
                yLengthRemaining -= thisPistonsLength;
            } else {
                SetPistonLength(p, 0);
            }
        });
        phase = AlignPhase.XY_FINISH;

    } else if (phase == AlignPhase.XY_FINISH) {
        var xDoneAligning = xPistons.FindAll(IsPistonDone);
        var yDoneAligning = yPistons.FindAll(IsPistonDone);
        if (xDoneAligning.Count == xPistons.Count &&
            yDoneAligning.Count == yPistons.Count) {
            // done
            xPistons.ForEach(Disable);
            yPistons.ForEach(Disable);
            Log("Done aligning, all X and Y pistons aligned");
            if (isParking) {
                DoneParking();
            } else {
                mode = Mode.BEGIN;
            }
        }
    }
}

void DoDrillMode() {
    // pause if the inventory is over x%
    double percentage = GetInventoryFullPercent();
    if (percentage >= MAX_INV_PERCENTAGE) {
        Log($"Inv at {percentage:0.0}%, pausing");
        PauseDrilling();
        return;
    }

    // pause if anything isn't functional
    if (!AreAllPistonsFunctional() || !AreAllDrillsFunctional()) {
        Log("ERROR: There are non-functional pistons or drills, pausing");
        PauseDrilling();
        return;
    }

    if (zInProgress == -1) {
        for (int i = 0; i < zPistons.Count; i++) {
            var piston = zPistons[i];
            if (piston.CurrentPosition < PISTON_MAX_LENGTH) {
                zInProgress = i;
                EnableDrills();
                ExtendFully(piston);
                break;
            }
        }

        if (zInProgress == -1) {
            Log($"No more Zs, done with this zone");
            DisableDrills();
            ZONES.RemoveAt(0);
            mode = Mode.BEGIN;
        }

    } else {
        var piston = zPistons[zInProgress];
        if (IsPistonDone(piston)) {
            // done.
            Log($"Z{zInProgress + 1} done");
            Disable(piston);
            zInProgress = -1;
        }
    }
}

void DoPauseMode() {
    double percentage = GetInventoryFullPercent();
    if (percentage < MAX_INV_PERCENTAGE && AreAllPistonsFunctional() && AreAllDrillsFunctional()) {
        Log($"Unpausing");
        UnpauseDrilling();
    }
}

void PauseDrilling() {
    PausePistons();
    DisableDrills();
    mode = Mode.PAUSE;
}

void UnpauseDrilling() {
    UnpausePistons();
    EnableDrills();
    mode = Mode.DRILL;
}

void PausePistons() {
    zPausedPistons = zPistons.FindAll(p => p.Enabled == true);
    zPausedPistons.ForEach(Disable);
}

void UnpausePistons() {
    zPausedPistons.ForEach(Enable);
    zPausedPistons = null;
}

void Enable(IMyFunctionalBlock block) {
    block.Enabled = true;
}

void Disable(IMyFunctionalBlock block) {
    block.Enabled = false;
}

void DisablePistons() {
    xPistons.ForEach(Disable);
    yPistons.ForEach(Disable);
    zPistons.ForEach(Disable);
}

void ExtendFully(IMyExtendedPistonBase piston) {
    SetPistonLength(piston, PISTON_MAX_LENGTH);
}

void RetractFully(IMyExtendedPistonBase piston) {
    SetPistonLength(piston, 0);
}

void SetPistonLength(IMyExtendedPistonBase piston, float length) {
    piston.MinLimit = length;
    piston.MaxLimit = length;
    if (piston.CurrentPosition > length) {
        piston.Retract();
    } else {
        piston.Extend();
    }
    piston.Enabled = true;
}

bool IsPistonDone(IMyExtendedPistonBase piston) {
    return piston.CurrentPosition == piston.MinLimit; // MinLimit and MaxLimit will always be the same and the length I want it to be
}

public bool AreAllPistonsFunctional() {
    // TODO: look into CurrentDamage, not just IsFunctional
    var brokenXs = xPistons.FindAll(p => !p.IsFunctional);
    var brokenYs = yPistons.FindAll(p => !p.IsFunctional);
    var brokenZs = zPistons.FindAll(p => !p.IsFunctional);
    return (brokenXs.Count == 0 && brokenYs.Count == 0 && brokenZs.Count == 0);
}

void DiscoverDrills() {
    drills = FilterBlocks<IMyShipDrill>();
}

void EnableDrills() {
    drills.ForEach(Enable);
}

void DisableDrills() {
    drills.ForEach(Disable);
}

bool AreAllDrillsFunctional() {
    // there should only be one drill, so this should be improved
    var functionalDrills = drills.FindAll(d => d.IsFunctional);
    var brokenDrills = drills.FindAll(d => !d.IsFunctional);
    return functionalDrills.Count > 0 && brokenDrills.Count == 0;
}

public double GetInventoryFullPercent() {
    MyFixedPoint currentVolume = 0;
    MyFixedPoint maxVolume = 0;
    foreach (var inv in inventories) {
        currentVolume += inv.CurrentVolume;
        maxVolume += inv.MaxVolume;
    }
    return 100.0 * currentVolume.ToIntSafe() / maxVolume.ToIntSafe();
}

public void DiscoverInventories()
{
    FilterBlocks<IMyCargoContainer>().ForEach(AddInventories);
    FilterBlocks<IMyCockpit>().ForEach(AddInventories);
    drills.ForEach(AddInventories);
}

void AddInventories(IMyTerminalBlock b)
{
    Log($"Found inventory: {b.DisplayNameText}");
    for (int i = 0; i < b.InventoryCount; i++) {
        inventories.Add(b.GetInventory(i));
    }
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

public List<T> FilterBlocks<T>(Func<T, Boolean> filter = null) where T : class, IMyTerminalBlock
{
    var blocks = new List<T>();
    GridTerminalSystem.GetBlocksOfType(blocks, x => {
        if (!x.IsSameConstructAs(Me)) return false;
        // If a tag was defined (in CustomData), only accept blocks with that tag
        // Log($"FB: {x.DisplayNameText}");
        // For some reason the square brackets are getting filtered out of DisplaNameText,
        // so just look for a match of the name, no brackets.
        if (!String.IsNullOrEmpty(filterTag) && 
            !x.DisplayNameText.Contains(filterTag)) {
            Log($"FB: {x.DisplayNameText} did not match filter {filterTag}");
            return false;
        }
        return (filter == null) || filter(x);
    });
    return blocks.ConvertAll(x => (T)x);
}

public T GetFirstBlockOfType<T>(Func<T, Boolean> filter = null) where T : class, IMyTerminalBlock
{
    var blocks = FilterBlocks<T>(filter);
    return (blocks.Count == 0) ? null : blocks.First();
}
