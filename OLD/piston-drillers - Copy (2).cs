/*
Notes:
- Set all pistons to have Share inertia tensor.
- Set all pistons to have Max Impulse Axis/NonAxis to 600kN.
*/
List<IMyExtendedPistonBase> xPistons = null;
List<IMyExtendedPistonBase> yPistons = null;
List<IMyExtendedPistonBase> zPistons = null;
List<IMyExtendedPistonBase> xPausedPistons = null;
List<IMyExtendedPistonBase> yPausedPistons = null;
List<IMyExtendedPistonBase> zPausedPistons = null;
List<IMyShipDrill> drills = null;
enum Mode {
    SNAKING, SNAKING_PAUSED, RETRACTING, NONE
}
enum PistonDirection {
    X, Y, Z, XY, NONE
}
enum PistonAction {
    EXTENDING, RETRACTING
}
Mode mode = Mode.NONE;
PistonDirection direction = PistonDirection.X;
PistonAction xAction = PistonAction.EXTENDING;
PistonAction yAction = PistonAction.EXTENDING;
int xInProgress = -1;
int yInProgress = -1;
int zInProgress = -1;
double yMetersNeeded = 0;
const double Y_METERS_PER_ROW = 7.5; // must be multiple of 2.5 and 30 (3 pistons)

List<IMyInventory> inventories = new List<IMyInventory>();
int MAX_INV_PERCENTAGE = 50;
List<IMyTextPanel> textPanels = null;
const float FONT_SIZE = 1.0F;
int logCounter = 1;
bool pauseWasManual = false;
const float INITIAL_SPEED = 0.5F; // this should be positive

public Program()
{
    textPanels = FilterBlocks<IMyTextPanel>(b => b.CustomName.Contains("[DRILL]"));
    if (textPanels.Count() == 0) {
        Log("Warning: No Text Panel(s) with [DRILL] found. Add one or more.");
    }
    textPanels.ForEach(textPanel => {
        textPanel.ContentType = ContentType.TEXT_AND_IMAGE;
        textPanel.FontSize = FONT_SIZE;
        textPanel.Alignment = VRage.Game.GUI.TextPanel.TextAlignment.LEFT;
        Log("Using Text Panel: " + textPanel.CustomName);
    });

    DiscoverInventories();

    ResetVars();

    xPistons = FilterBlocks<IMyExtendedPistonBase>(p => p.CustomName.Contains("Piston X")).OrderBy(p => p.CustomName).ToList();
    yPistons = FilterBlocks<IMyExtendedPistonBase>(p => p.CustomName.Contains("Piston Y")).OrderBy(p => p.CustomName).ToList();
    zPistons = FilterBlocks<IMyExtendedPistonBase>(p => p.CustomName.Contains("Piston Z")).OrderBy(p => p.CustomName).ToList();

    DisableAllPistons();

    xPistons.ForEach(p => p.Velocity = INITIAL_SPEED);
    yPistons.ForEach(p => p.Velocity = INITIAL_SPEED);
    zPistons.ForEach(p => p.Velocity = INITIAL_SPEED);

    drills = FilterBlocks<IMyShipDrill>();
    StopDrills();
}

public void Main(string argument, UpdateType updateSource)
{
    if (argument.ToUpper() == "RETRACT") {
        StartRetracting();
        Runtime.UpdateFrequency = UpdateFrequency.Update10;
        return;
    }
    if (argument.ToUpper() == "START" || argument.ToUpper() == "SNAKE") {
        StartSnaking();
        Runtime.UpdateFrequency = UpdateFrequency.Update10;
        return;
    }
    if (argument.ToUpper() == "PAUSE") {
        if (mode == Mode.SNAKING) {
            Log("Pausing");
            PauseSnaking();
            pauseWasManual = true;
        }
        return;
    }
    if (argument.ToUpper() == "UNPAUSE") {
        if (mode == Mode.SNAKING_PAUSED) {
            Log("Unpausing");
            UnpauseSnaking();
        }
        return;
    }
    if (argument.ToUpper() == "STOP") {
        DisableAllPistons();
        StopDrills();
        ResetVars();
        Runtime.UpdateFrequency = UpdateFrequency.None;
        return;
    }
    if (argument.ToUpper() == "FASTER") {
        Log("Faster");
        xPistons.ForEach(p => p.Velocity *= 2);
        yPistons.ForEach(p => p.Velocity *= 2);
        zPistons.ForEach(p => p.Velocity *= 2);
    }
    if (argument.ToUpper() == "SLOWER") {
        Log("Slower");
        xPistons.ForEach(p => p.Velocity /= 2);
        yPistons.ForEach(p => p.Velocity /= 2);
        zPistons.ForEach(p => p.Velocity /= 2);
    }
    if (argument.ToUpper() == "RESET_SPEED") {
        Log($"Speed back at {INITIAL_SPEED}");
        xPistons.ForEach(p => p.Velocity = p.Velocity / Math.Abs(p.Velocity) * INITIAL_SPEED); // preserve positive or negative
        yPistons.ForEach(p => p.Velocity = p.Velocity / Math.Abs(p.Velocity) * INITIAL_SPEED);
        zPistons.ForEach(p => p.Velocity = p.Velocity / Math.Abs(p.Velocity) * INITIAL_SPEED);
    }

    if (mode == Mode.RETRACTING) {
        HandleRetracting();

    } else if (mode == Mode.SNAKING) {
        // pause if the inventory is over x%
        double percentage = GetInventoryFullPercent();
        if (percentage >= MAX_INV_PERCENTAGE) {
            Log($"Inv at {percentage:0.0}%, pausing");
            PauseSnaking();
            pauseWasManual = false;
            return;
        }

        // pause if anything isn't functional
        if (!AreAllPistonsFunctional() || !AreAllDrillsFunctional()) {
            Log("ERROR: There are non-functional pistons, pausing");
            PauseSnaking();
            pauseWasManual = false;
            return;
        }

        HandleSnaking();

    } else if (mode == Mode.SNAKING_PAUSED) {
        if (pauseWasManual) {
            return;
        }
        double percentage = GetInventoryFullPercent();
        if (percentage < MAX_INV_PERCENTAGE && AreAllPistonsFunctional() && AreAllDrillsFunctional()) {
            Log($"Unpausing");
            UnpauseSnaking();
        }
    }
}

void ResetVars() {
    mode = Mode.NONE;
    direction = PistonDirection.NONE;
    xAction = PistonAction.EXTENDING;
    yAction = PistonAction.EXTENDING;
    xInProgress = -1;
    yInProgress = -1;
    zInProgress = -1;
}

public void StartRetracting() {
    StopDrills();
    DisableAllPistons();
    ResetVars();
    mode = Mode.RETRACTING;
    direction = PistonDirection.Z;
}

public void HandleRetracting() {
    if (direction == PistonDirection.Z) {
        if (zInProgress == -1) {
            // Initiate retracting all Z pistons
            Log("Retracting all Zs");
            zPistons.ForEach(RetractFully);
            zInProgress = 0;
        } else {
            // wait for all pistons to be Retracted
            // var stillRetracting = zPistons.FindAll(p => p.Status != PistonStatus.Retracted);
            var stillRetracting = zPistons.FindAll(p => p.CurrentPosition != 0);
            if (stillRetracting.Count == 0) {
                // done
                Log("All Z pistons retracted, now X+Y");
                zPistons.ForEach(Disable);
                zInProgress = -1;
                direction = PistonDirection.XY;
            }
        }
    } else if (direction == PistonDirection.XY) {
        if (xInProgress == -1) {
            // Initiate retracting all X and Y pistons
            xPistons.ForEach(RetractFully);
            yPistons.ForEach(RetractFully);
            xInProgress = 0;
        } else {
            // wait for all pistons to be Retracted
            // var xStillRetracting = xPistons.FindAll(p => p.Status != PistonStatus.Retracted);
            // var yStillRetracting = yPistons.FindAll(p => p.Status != PistonStatus.Retracted);
            var xStillRetracting = xPistons.FindAll(p => p.CurrentPosition != 0);
            var yStillRetracting = yPistons.FindAll(p => p.CurrentPosition != 0);
            if (xStillRetracting.Count == 0 && yStillRetracting.Count == 0) {
                // done
                Log("DONE RETRACTING");
                xPistons.ForEach(Disable);
                yPistons.ForEach(Disable);
                xInProgress = -1;
                direction = PistonDirection.NONE;
                mode = Mode.NONE;
            }
        }
    }
}

public void StartSnaking() {
    // if (!AreAllPistonsRetracted()) {
    //     Log("ERROR: Pistons must be retracted to start snaking");
    //     Log("(Issue command RETRACT)");
    //     return;
    // }
    DisableAllPistons();
    ResetVars();
    StartDrills();
    mode = Mode.SNAKING;
    direction = PistonDirection.X;
}

void PauseSnaking() {
    PausePistons();
    StopDrills();
    mode = Mode.SNAKING_PAUSED;
}

void UnpauseSnaking() {
    UnpausePistons();
    StartDrills();
    mode = Mode.SNAKING;
}

public void HandleSnaking() {
    if (direction == PistonDirection.X) {
        if (xInProgress == -1) {
            for (int i = 0; i < xPistons.Count; i++) {
                var piston = xPistons[i];
                if (xAction == PistonAction.EXTENDING) {
                    if (piston.Status != PistonStatus.Extended) {
                        Log($"X{i} extending");
                        ExtendFully(piston);
                        xInProgress = i;
                        break;
                    }
                } else {
                    if (piston.Status != PistonStatus.Retracted) {
                        Log($"X{i} retracting");
                        RetractFully(piston);
                        xInProgress = i;
                        break;
                    }
                }
            }

            if (xInProgress == -1) {
                // couldn't find a X piston to activate, go to Y
                Log($"No more Xs, to Y");
                direction = PistonDirection.Y;
                yMetersNeeded = Y_METERS_PER_ROW;
            }

        } else {
            var piston = xPistons[xInProgress];
            if (IsPistonDone(piston, xAction)) {
                Log($"X{xInProgress} done");
                Disable(piston);
                xInProgress = -1;
            }
        }

    } else if (direction == PistonDirection.Y) {
        if (yInProgress == -1) {
            for (int i = 0; i < yPistons.Count; i++) {
                var piston = yPistons[i];
                if (yAction == PistonAction.EXTENDING) {
                    if (piston.CurrentPosition == 0) {
                        Log($"Y{i} extending to 1/4");
                        ExtendToOneQuarter(piston);
                        yInProgress = i;
                        break;
                    } else if (piston.CurrentPosition == 2.5) {
                        Log($"Y{i} extending to 1/2");
                        ExtendToHalf(piston);
                        yInProgress = i;
                        break;
                    } else if (piston.CurrentPosition == 5) {
                        Log($"Y{i} extending to 3/4");
                        ExtendToThreeQuarters(piston);
                        yInProgress = i;
                        break;
                    } else if (piston.CurrentPosition == 7.5) {
                        Log($"Y{i} extending fully");
                        ExtendFully(piston);
                        yInProgress = i;
                        break;
                    }

                } else {
                    if (piston.CurrentPosition == 10) {
                        Log($"Y{i} retracting to 3/4");
                        RetractToThreeQuarters(piston);
                        yInProgress = i;
                        break;
                    } else if (piston.CurrentPosition == 7.5) {
                        Log($"Y{i} retracting to 1/2");
                        RetractToHalfWay(piston);
                        yInProgress = i;
                        break;
                    } else if (piston.CurrentPosition == 5) {
                        Log($"Y{i} retracting to 1/4");
                        RetractToOneQuarter(piston);
                        yInProgress = i;
                        break;
                    } else if (piston.CurrentPosition == 2.5) {
                        Log($"Y{i} retracting fully");
                        RetractFully(piston);
                        yInProgress = i;
                        break;
                    }
                }
            }

            if (yInProgress == -1) {
                Log($"No more Ys, to Z");
                direction = PistonDirection.Z;
            }

        } else {
            var piston = yPistons[yInProgress];
            if (IsPistonDone(piston, yAction)) {
                // This Y done.
                Log($"Y{yInProgress} done");
                Disable(piston);
                yInProgress = -1;
                yMetersNeeded -= 2.5;
                if (yMetersNeeded == 0) {
                    // Y cycle done. Go back to X (and switch its extending/retracting action)
                    Log($"Ys done, back to X");
                    direction = PistonDirection.X;
                    ReverseXAction();
                }
            }
        }

    } else { // direction == PistonDirection.Z
        if (zInProgress == -1) {
            for (int i = 0; i < zPistons.Count; i++) {
                var piston = zPistons[i];
                if (piston.CurrentPosition == 0) {
                    Log($"Z{i} extending to 1/4");
                    ExtendToOneQuarter(piston);
                    zInProgress = i;
                    break;
                } else if (piston.CurrentPosition == 2.5) {
                    Log($"Z{i} extending to 1/2");
                    ExtendToHalf(piston);
                    zInProgress = i;
                    break;
                } else if (piston.CurrentPosition == 5) {
                    Log($"Z{i} extending to 3/4");
                    ExtendToThreeQuarters(piston);
                    zInProgress = i;
                    break;
                } else if (piston.CurrentPosition == 7.5) {
                    Log($"Z{i} extending fully");
                    ExtendFully(piston);
                    zInProgress = i;
                    break;
                }
            }

            if (zInProgress == -1) {
                // couldn't find a Z piston to act on. We're done.
                Log($"No more Zs, ALL DONE");
                direction = PistonDirection.NONE;
                mode = Mode.NONE;
                StopDrills();
                Runtime.UpdateFrequency = UpdateFrequency.None;
            }

        } else {
            var piston = zPistons[zInProgress];
            if (IsPistonDone(piston, PistonAction.EXTENDING)) {
                // done.
                Log($"Z{zInProgress} done, back to X");
                Disable(piston);
                zInProgress = -1;
                direction = PistonDirection.X;
                // reverse both X's and Y's extending vs. retracting action
                ReverseXAction();
                ReverseYAction();
            }
        }
    }
}

void Enable(IMyFunctionalBlock block) {
    block.Enabled = true;
}

void Disable(IMyFunctionalBlock block) {
    block.Enabled = false;
}

void Extend(IMyExtendedPistonBase piston, float limit) {
    piston.MaxLimit = limit;
    piston.Extend();
    piston.Enabled = true;
}

void ExtendToOneQuarter(IMyExtendedPistonBase piston) {
    Extend(piston, 2.5F);
}

void ExtendToHalf(IMyExtendedPistonBase piston) {
    Extend(piston, 5);
}

void ExtendToThreeQuarters(IMyExtendedPistonBase piston) {
    Extend(piston, 7.5F);
}

void ExtendFully(IMyExtendedPistonBase piston) {
    Extend(piston, 10);
}

void Retract(IMyExtendedPistonBase piston, float limit) {
    piston.MinLimit = limit;
    piston.Retract();
    piston.Enabled = true;
}

void RetractToThreeQuarters(IMyExtendedPistonBase piston) {
    Retract(piston, 7.5F);
}

void RetractToHalfWay(IMyExtendedPistonBase piston) {
    Retract(piston, 5);
}

void RetractToOneQuarter(IMyExtendedPistonBase piston) {
    Retract(piston, 2.5F);
}

void RetractFully(IMyExtendedPistonBase piston) {
    Retract(piston, 0);
}

public void DisableAllPistons() {
    xPistons.ForEach(Disable);
    yPistons.ForEach(Disable);
    zPistons.ForEach(Disable);
}

public void EnableAllPistons() {
    xPistons.ForEach(Enable);
    yPistons.ForEach(Enable);
    zPistons.ForEach(Enable);
}

void PausePistons() {
    xPausedPistons = xPistons.FindAll(p => p.Enabled == true);
    yPausedPistons = yPistons.FindAll(p => p.Enabled == true);
    zPausedPistons = zPistons.FindAll(p => p.Enabled == true);
    DisableAllPistons();
}

void UnpausePistons() {
    xPausedPistons.ForEach(Enable);
    yPausedPistons.ForEach(Enable);
    zPausedPistons.ForEach(Enable);
    xPausedPistons = null;
    yPausedPistons = null;
    zPausedPistons = null;
}

bool IsPistonDone(IMyExtendedPistonBase piston, PistonAction action) {
    if (action == PistonAction.EXTENDING) {
        return piston.Status == PistonStatus.Extended;
    } else {
        return piston.Status == PistonStatus.Retracted;
    }
}

void ReverseXAction() {
    if (xAction == PistonAction.EXTENDING) {
        xAction = PistonAction.RETRACTING;
    } else {
        xAction = PistonAction.EXTENDING;
    }
}

void ReverseYAction() {
    if (yAction == PistonAction.EXTENDING) {
        yAction = PistonAction.RETRACTING;
    } else {
        yAction = PistonAction.EXTENDING;
    }
}

public bool AreAllPistonsFunctional() {
    var brokenXs = xPistons.FindAll(p => !p.IsFunctional);
    var brokenYs = yPistons.FindAll(p => !p.IsFunctional);
    var brokenZs = zPistons.FindAll(p => !p.IsFunctional);
    return (brokenXs.Count == 0 && brokenYs.Count == 0 && brokenZs.Count == 0);
}

bool AreAllPistonsRetracted() {
    var badXs = xPistons.FindAll(p => p.CurrentPosition != 0);
    var badYs = yPistons.FindAll(p => p.CurrentPosition != 0);
    var badZs = zPistons.FindAll(p => p.CurrentPosition != 0);
    return (badXs.Count == 0 && badYs.Count == 0 && badZs.Count == 0);
}

void StartDrills() {
    drills.ForEach(Enable);
}

void StopDrills() {
    drills.ForEach(Disable);
}

bool AreAllDrillsFunctional() {
    var brokenDrills = drills.FindAll(d => !d.IsFunctional);
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

    textPanels.ForEach(t => {
        string oldText = t.GetText();
        // only keep most recent 25 lines
        IEnumerable<string> oldLines = oldText.Split(new string[] {"\r\n","\n"}, StringSplitOptions.RemoveEmptyEntries).Take(25);
        string newText = message + "\n" + string.Join(Environment.NewLine.ToString(), oldLines);
        t.WriteText(newText);
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

public T GetFirstBlockOfType<T>(Func<T, Boolean> filter = null) where T : class, IMyTerminalBlock
{
    var blocks = FilterBlocks<T>(filter);
    return (blocks.Count == 0) ? null : blocks.First();
}
