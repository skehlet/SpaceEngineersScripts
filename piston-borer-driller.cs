/*
Notes:
- Set all pistons to have Share inertia tensor.
- Set all X and Y pistons to have Max Impulse Axis/NonAxis to 100kN.
- Set all Z pistons to have Max Impulse Axis/NonAxis to 600kN.

When activating the programmable block:
Toggle block on
Recompile
You should see it align to [15, 15] and then begin drilling.
*/
List<IMyExtendedPistonBase> xPistons = null;
List<IMyExtendedPistonBase> yPistons = null;
List<IMyExtendedPistonBase> zPistons = null;
List<IMyExtendedPistonBase> zPausedPistons = null;
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

List<IMyInventory> inventories = new List<IMyInventory>();
int MAX_INV_PERCENTAGE = 90;
List<IMyTextSurface> textSurfaces = null;
const float FONT_SIZE = 1.0F;
int logCounter = 1;
const float INITIAL_SPEED = 0.5F; // this should be positive

public Program()
{
    textSurfaces = FilterBlocks<IMyTextPanel>(b => b.CustomName.Contains("[DRILL]")).ConvertAll(x => (IMyTextSurface)x);
    if (textSurfaces.Count() == 0) {
        Log("Warning: No Text Panel(s) with [DRILL] found. Add one or more.");
    }
	textSurfaces.Add(Me.GetSurface(0));
    textSurfaces.ForEach(textSurface => {
        textSurface.ContentType = ContentType.TEXT_AND_IMAGE;
        textSurface.FontSize = FONT_SIZE;
        textSurface.Alignment = VRage.Game.GUI.TextPanel.TextAlignment.LEFT;
        if (textSurface is IMyTextPanel) {
            Log($"Using Text Panel: {((IMyTextPanel)textSurface).CustomName}");
        }
    });

    if (IsFirstTime()) {
        InitZones();
        Log($"First time, initialized 25 zones");
    } else {
        LoadZonesFromStorage();
        Log("Loaded ZONES: " + String.Join(", ", ZONES));
    }

    DiscoverInventories();

    xPistons = FilterBlocks<IMyExtendedPistonBase>(p => p.CustomName.Contains("Piston X")).OrderBy(p => p.CustomName).ToList();
    yPistons = FilterBlocks<IMyExtendedPistonBase>(p => p.CustomName.Contains("Piston Y")).OrderBy(p => p.CustomName).ToList();
    zPistons = FilterBlocks<IMyExtendedPistonBase>(p => p.CustomName.Contains("Piston Z")).OrderBy(p => p.CustomName).ToList();
    DisablePistons();
    xPistons.ForEach(p => p.Velocity = INITIAL_SPEED);
    yPistons.ForEach(p => p.Velocity = INITIAL_SPEED);
    zPistons.ForEach(p => p.Velocity = INITIAL_SPEED);

    DisableDrills();

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
        Log("ZONES: " + String.Join(", ", ZONES));
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

bool IsFirstTime() {
    return String.IsNullOrEmpty(Storage);
}

void InitZones() {
    ZONES.Clear();
    ZONES.Add(13);
    ZONES.Add(12);
    ZONES.Add(17);
    ZONES.Add(18);
    ZONES.Add(19);
    ZONES.Add(14);
    ZONES.Add(9);
    ZONES.Add(8);
    ZONES.Add(7);
    ZONES.Add(6);
    ZONES.Add(11);
    ZONES.Add(16);
    ZONES.Add(21);
    ZONES.Add(22);
    ZONES.Add(23);
    ZONES.Add(24);
    ZONES.Add(25);
    ZONES.Add(20);
    ZONES.Add(15);
    ZONES.Add(10);
    ZONES.Add(5);
    ZONES.Add(4);
    ZONES.Add(3);
    ZONES.Add(2);
    ZONES.Add(1);
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

int GetCurrentZone() {
    return ZONES.Count > 0 ? ZONES[0] : -1;
}

/*
 * This is a 5x5 grid of "zones" where each zone is a 3x3 block square, sized to match 
 * the 9 drillers at the end of the pistons. Zones are counted starting from the bottom left.
 * Zone 1 is at 0,0, Zone 2 is 3 blocks (7.5m) to the right, etc.
 * 
 */
float[] GetPositionForZone(int zone) {
    float x, y;
    switch (zone) {
        case 1: x = 0; y = 0; break;
        case 2: x = 7.5F; y = 0; break;
        case 3: x = 15; y = 0; break;
        case 4: x = 22.5F; y = 0; break;
        case 5: x = 30; y = 0; break;
        case 6: x = 0; y = 7.5F; break;
        case 7: x = 7.5F; y = 7.5F; break;
        case 8: x = 15; y = 7.5F; break;
        case 9: x = 22.5F; y = 7.5F; break;
        case 10: x = 30; y = 7.5F; break;
        case 11: x = 0; y = 15; break;
        case 12: x = 7.5F; y = 15; break;
        case 13: x = 15; y = 15; break;
        case 14: x = 22.5F; y = 15; break;
        case 15: x = 30; y = 15; break;
        case 16: x = 0; y = 22.5F; break;
        case 17: x = 7.5F; y = 22.5F; break;
        case 18: x = 15; y = 22.5F; break;
        case 19: x = 22.5F; y = 22.5F; break;
        case 20: x = 30; y = 22.5F; break;
        case 21: x = 0; y = 30; break;
        case 22: x = 7.5F; y = 30; break;
        case 23: x = 15; y = 30; break;
        case 24: x = 22.5F; y = 30; break;
        case 25: x = 30; y = 30; break;
        default: x = 0; y = 0; break;
    }
    float[] pos = new float[2];
    pos[0] = x;
    pos[1] = y;
    return pos;
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
        Log("We are aligned");
        mode = Mode.DRILL;
    } else {
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
                float thisPistonsLength = Math.Min(10, xLengthRemaining);
                SetPistonLength(p, thisPistonsLength);
                xLengthRemaining -= thisPistonsLength;
            } else {
                SetPistonLength(p, 0);
            }
        });
        yPistons.ForEach(p => {
            if (yLengthRemaining > 0) {
                float thisPistonsLength = Math.Min(10, yLengthRemaining);
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
            if (piston.CurrentPosition < 10) {
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
    SetPistonLength(piston, 10);
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
    var brokenXs = xPistons.FindAll(p => !p.IsFunctional);
    var brokenYs = yPistons.FindAll(p => !p.IsFunctional);
    var brokenZs = zPistons.FindAll(p => !p.IsFunctional);
    return (brokenXs.Count == 0 && brokenYs.Count == 0 && brokenZs.Count == 0);
}

void EnableDrills() {
    FilterBlocks<IMyShipDrill>().ForEach(Enable);
}

void DisableDrills() {
    FilterBlocks<IMyShipDrill>().ForEach(Disable);
}

bool AreAllDrillsFunctional() {
    var brokenDrills = FilterBlocks<IMyShipDrill>().FindAll(d => !d.IsFunctional);
    return brokenDrills.Count == 0;
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
    FilterBlocks<IMyShipDrill>().ForEach(AddInventories);
}

void AddInventories(IMyTerminalBlock b)
{
    Log($"Found: {b.DisplayNameText}");
    for (int i = 0; i < b.InventoryCount; i++) {
        inventories.Add(b.GetInventory(i));
    }
}

public void Log(string message)
{
    message = $"{logCounter++}: {message.Trim()}";

    Echo(message);

    if (textSurfaces != null) {
        textSurfaces.ForEach(t => {
            string oldText = t.GetText();
            // only keep most recent 25 lines
            IEnumerable<string> oldLines = oldText.Split(new string[] {"\r\n","\n"}, StringSplitOptions.RemoveEmptyEntries).Take(25);
            string newText = message + "\n" + string.Join(Environment.NewLine.ToString(), oldLines);
            t.WriteText(newText);
        });
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
