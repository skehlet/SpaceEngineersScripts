IMyRemoteControl remoteControl;
IMyTextPanel lcd2;
IMyTextPanel lcd4;
IMyTextPanel lcd5;
Vector3D thrustToGps;

double totalDistance = -1;
double halfDistance = -1;
bool doThrust = false;
int PHASE = 0; // TODO: Enum or somethign else?
long PHASE_1_START;
double PHASE_1_TIME;
Vector3D PHASE_1_START_POS;
double PHASE_1_DISTANCE;

Speedometer MySpeedometer;
// Accelerometer MyAccelerometer;
Calibrator MyCalibrator;
// double ACCELERATION = 0;
// double DECELERATION = 0;
double ACCELERATION = 8.4; // dont' want to calibrate every time
double DECELERATION = 8.4;



const int MAX_SPEED = 50;

public Program()
{
    Runtime.UpdateFrequency = UpdateFrequency.None; 
    // lcd1 = GridTerminalSystem.GetBlockWithName("LCD Panel") as IMyTextPanel;
    // lcd2 = GridTerminalSystem.GetBlockWithName("LCD Panel 2") as IMyTextPanel;
    lcd2 = GridTerminalSystem.GetBlockWithName("LCD Panel 2") as IMyTextPanel;
    lcd4 = GridTerminalSystem.GetBlockWithName("LCD Panel 4") as IMyTextPanel;
    lcd5 = GridTerminalSystem.GetBlockWithName("LCD Panel 5") as IMyTextPanel;
    remoteControl = GetFirstBlockOfType<IMyRemoteControl>();
    // forwardCamera = GridTerminalSystem.GetBlockWithName("Camera FORWARD") as IMyCameraBlock;
    // upCamera = GridTerminalSystem.GetBlockWithName("Camera UP") as IMyCameraBlock;
    // rightCamera = GridTerminalSystem.GetBlockWithName("Camera RIGHT") as IMyCameraBlock;
    // Echo("lcd1: " + lcd1);
    // Echo("lcd2: " + lcd2);
    // Echo("remoteControl: " + remoteControl);
    foreach (IMyThrust thruster in FilterBlocks<IMyThrust>()) {
        Echo("thruster: " + thruster.CustomName);
    }

    MySpeedometer = new Speedometer(remoteControl, lcd2);
    // MyAccelerometer = new Accelerometer(remoteControl, lcd5);
    MyCalibrator = new Calibrator(this, lcd4);

    Runtime.UpdateFrequency = UpdateFrequency.Update10;
}

public void Main(string argument, UpdateType updateSource)
{
    try {
        if ((updateSource & (UpdateType.Update1 | UpdateType.Update10 | UpdateType.Update100)) != 0) {

            if ((updateSource & UpdateType.Update1) != 0) {
            }
            if ((updateSource & UpdateType.Update10) != 0) {
                MySpeedometer.Update();
                if (doThrust) {
                    Thrust(lcd4);
                }
                if (MyCalibrator.ShouldRun()) {
                    MyCalibrator.Run();
                }
            }
            // if ((updateSource & UpdateType.Update100) != 0) {
            //     // MyAccelerometer.Update();
            // }

        } else {
            if (argument == "STOP") {
                Echo("STOP");
                doThrust = false;
                PHASE = 0;
                MyCalibrator.Stop();
                Runtime.UpdateFrequency = UpdateFrequency.None;
                ReleaseThrusters();

            } else if (argument == "DEBUG") {
                Debug(lcd4);

            } else if (argument.StartsWith("THRUST")) {
                Echo("THRUST");
                doThrust = !doThrust;
                Runtime.UpdateFrequency |= UpdateFrequency.Update10; 

                String gpsArg = argument.Substring(argument.IndexOf(" ") + 1);
                // GPS:ithilyn #1:64521.01:254580.78:5824518.62:
                string[] gpsPieces = gpsArg.Split(':');
                if (gpsPieces.Length != 6) {
                    Echo("Bad GPS found " + gpsPieces.Length + " pieces");
                    return;
                }
                string label = gpsPieces[1];
                double gpsX = Convert.ToDouble(gpsPieces[2]);
                double gpsY = Convert.ToDouble(gpsPieces[3]);
                double gpsZ = Convert.ToDouble(gpsPieces[4]);

                thrustToGps = new Vector3D(gpsX, gpsY, gpsZ);
                Echo("thrustToGps: " + thrustToGps);

            } else if (argument == "CALIBRATE") {
                MyCalibrator.Start();
                Runtime.UpdateFrequency |= UpdateFrequency.Update10;
                // Runtime.UpdateFrequency |= UpdateFrequency.Update100;

            } else {
                Echo("INVALID ARG" + argument);
                return;
            }
        }
    } catch (Exception e) {
        Echo($"Exception: {e}\n---");
        throw;
    }

}

public void Debug(IMyTextPanel lcd) {
    StringBuilder sb = new StringBuilder();

    sb.AppendLine("Forward thrusters:");
    FindForwardThrusters().ForEach((t) => sb.AppendLine(t.CustomName));

    sb.AppendLine("Backward thrusters:");
    FindBackwardThrusters().ForEach((t) => sb.AppendLine(t.CustomName));

    WriteToPanel(lcd, sb.ToString());
}

public void Thrust(IMyTextPanel lcd) {
    StringBuilder sb = new StringBuilder();

    sb.AppendLine($"Total distance: {totalDistance}");
    var remainingDistance = CalculateDistance(remoteControl.GetPosition(), thrustToGps);
    sb.AppendLine($"Remaining distance: {remainingDistance}");

    // PHASEs: init (0), accelerating (1), coasting (2), decelerating (3), done (4)


    if (PHASE == 0) {
        PHASE = 1;
        PHASE_1_START = UnixTimeNow();
        PHASE_1_START_POS = remoteControl.GetPosition();
        totalDistance = CalculateDistance(PHASE_1_START_POS, thrustToGps);
        halfDistance = totalDistance / 2;
        FindForwardThrusters().ForEach(t => t.ThrustOverridePercentage = 100);

    } else if (PHASE == 1) {
        PHASE_1_TIME = UnixTimeNow() - PHASE_1_START;
        sb.AppendLine($"Accelerating for {PHASE_1_TIME}s");

        // TODO: given the known deceleration rate, calculate how much distance I'll need to stop at the current speed
        // so either hit MAX_SPEED, or
        // stop accelerating when I would need the remaining distance (+25m say) to stop
        // TODO: leave some fudge factor, then coast in the last x meters
        double currentSpeed = MySpeedometer.Speed;
        double distanceToStop = Math.Pow(currentSpeed, 2) / (2 * DECELERATION);
        distanceToStop += 25; // fudge
        sb.AppendLine($"Stopping distance: {distanceToStop:.0}m");

        // accelerating phase ends when either the max speed or the 1/2 way point is hit
        // if (MySpeedometer.Speed >= MAX_SPEED || remainingDistance < halfDistance) {
        // TODO need some kind of buffer, constantly overshooting
        if (MySpeedometer.Speed >= MAX_SPEED) {
            PHASE = 2;
            // PHASE_1_DISTANCE = CalculateDistance(remoteControl.GetPosition(), PHASE_1_START_POS);
            FindForwardThrusters().ForEach(t => t.ThrustOverridePercentage = 0);
            FindBackwardThrusters().ForEach(t => t.Enabled = false);

        } else if (remainingDistance <= distanceToStop) {
            PHASE = 3;
            // PHASE_1_DISTANCE = CalculateDistance(remoteControl.GetPosition(), PHASE_1_START_POS);
            FindForwardThrusters().ForEach(t => t.ThrustOverridePercentage = 0);
            FindBackwardThrusters().ForEach(t => {
                t.Enabled = true;
                t.ThrustOverridePercentage = 100;
            });
        }

    } else if (PHASE == 2) {
        sb.AppendLine("Coasting");
        // 1. ensure we maintain our speed (for example, if in atmosphere?) do I need to do this?
        // 2. need to do math to figure out when to start decelerating
        // during the accelerating phase, we should have kept track of how long it took to either reach max speed or 1/2 way (PHASE_1_TIME)
        // Now we need to just reverse that.
        // When the time we have left <= PHASE_1_TIME, move to PHASE 3
        // How do you account for that with deceleration??
        // You should be able to figure out what your rate of acceleration was.
        // Or, how far you travelled while in accelerating phase.
        // Just use the same distance for the decelerating phase.
        double currentSpeed = MySpeedometer.Speed;
        double distanceToStop = Math.Pow(currentSpeed, 2) / (2 * DECELERATION);
        sb.AppendLine($"Stopping distance: {distanceToStop:.0}m");

        // if (remainingDistance <= PHASE_1_DISTANCE * 1.1) {
        if (remainingDistance <= distanceToStop) {
            PHASE = 3;
            FindBackwardThrusters().ForEach(t => {
                t.Enabled = true;
                t.ThrustOverridePercentage = 100;
            });
        }

    } else if (PHASE == 3) {
        sb.AppendLine("Decelerating");
        // here you have to do some math to figure out how much power to apply? do i? or just leave at 100%
        // are we there yet? If so, back to PHASE 0
        // TODO need to slow down thrusters gradually as we approach zero speed
        // TODO: need to handle overshot. What happens is the engines fire up in reverse, we're no where near the right spot, and never stop
        // this logic might be better done just watching until we hit speed 0
        // if (CalculateDistance(remoteControl.GetPosition(), thrustToGps) < 10) {
        // TODO: some type of algorithm here to coast in over the remaining meters
        if (MySpeedometer.Speed < 5) {
            PHASE = 4;
            FindBackwardThrusters().ForEach(t => {
                t.ThrustOverridePercentage = 0;
                t.Enabled = false;
            });
        }

    } else if (PHASE == 4) {
        sb.AppendLine("Coasting in");
        // if (MySpeedometer.Speed < 0 || CalculateDistance(remoteControl.GetPosition(), thrustToGps) < 10) {
        if (CalculateDistance(remoteControl.GetPosition(), thrustToGps) < 5) {
            doThrust = false;
            ReleaseThrusters();
            PHASE = 0;
        }
    }

    sb.AppendLine($"SPEED: {MySpeedometer.Speed}");

    WriteToPanel(lcd, sb.ToString());
}


class Calibrator
{
    private Program MyProgram;
    private IMyTextPanel lcd;
    private bool shouldRun = false;
    private int phase = 0;
    private long startTimeMs = 0;
    private long totalTimeMs = 0;
    // private double acceleration = 0;
    // private double deceleration = 0;
    private Accelerometer MyAccelerometer;
    private long sampleCount = 0;

    public Calibrator(Program MyProgram, IMyTextPanel lcd)
    {
        this.MyProgram = MyProgram;
        this.lcd = lcd;
        this.MyAccelerometer = new Accelerometer(MyProgram.remoteControl, MyProgram.lcd5);
    }

    public void Start()
    {
        shouldRun = true;
        phase = 0;
    }

    public void Stop()
    {
        shouldRun = false;
        phase = 0;
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("Calibrator stopped");
        WriteToPanel(lcd, sb.ToString());
    }

    public bool ShouldRun()
    {
        return shouldRun;
    }

    public void Run()
    {
        StringBuilder sb = new StringBuilder();

        switch (phase)
        {
            case 0:
                sb.AppendLine("Starting");
                startTimeMs = GetTimeInMs();
                totalTimeMs = 0;
                MyProgram.FindForwardThrusters().ForEach(t => t.ThrustOverridePercentage = 100);
                // should really verify speed is zero
                // and all engines are enabled?
                phase = 1;
                break;

            case 1:
                // measure acceleration
                totalTimeMs = GetTimeInMs() - startTimeMs;

                if ((++sampleCount % 5) == 0) {
                    MyAccelerometer.Update();
                }

                sb.AppendLine($"Accelerating for {totalTimeMs/1000:0.0}s");
                sb.AppendLine($"Speed: {MyProgram.MySpeedometer.Speed:0.0}m/s");
                sb.AppendLine($"Acceleration: {MyAccelerometer.Acceleration:0.0}m/s^2");

                if (totalTimeMs > 10000) {
                    MyProgram.ACCELERATION = MyAccelerometer.Acceleration;
                    MyProgram.FindForwardThrusters().ForEach(t => t.ThrustOverridePercentage = 0);
                    // FindBackwardThrusters().ForEach(t => t.ThrustOverridePercentage = 100);
                    startTimeMs = GetTimeInMs();
                    totalTimeMs = 0;
                    phase = 2;
                }
                break;

            case 2:
                // measure deceleration
                totalTimeMs = GetTimeInMs() - startTimeMs;

                if ((++sampleCount % 5) == 0) {
                    MyAccelerometer.Update();
                }

                sb.AppendLine($"Decelerating for {totalTimeMs/1000:0.0}s");
                sb.AppendLine($"Speed: {MyProgram.MySpeedometer.Speed:0.0}m/s");
                sb.AppendLine($"Deceleration: {MyAccelerometer.Acceleration:0.0}m/s^2");

                if (totalTimeMs > 5000) {
                    MyProgram.DECELERATION = MyAccelerometer.Acceleration;
                    MyProgram.FindBackwardThrusters().ForEach(t => t.ThrustOverridePercentage = 0);
                    phase = 3;
                }
                break;

            case 3:
                MyProgram.ReleaseThrusters();
                sb.AppendLine("Calibration results:");
                sb.AppendLine($"Acceleration: {MyProgram.ACCELERATION:0.0}m/s^2");
                sb.AppendLine($"Deceleration: {MyProgram.DECELERATION:0.0}m/s^2");
                shouldRun = false;
                break;
        }

        WriteToPanel(lcd, sb.ToString());
    }

    // public double Acceleration
    // {
    //     get { return acceleration; }
    // }

    // public double Deceleration
    // {
    //     get { return deceleration; }
    // }
}


class Speedometer
{
    private IMyTerminalBlock reference;
    private IMyTextPanel lcd;
    private Vector3D previousPosition = new Vector3D(0, 0, 0);
    private long previousTicks = 0;
    private double speed = 0;

    public Speedometer(IMyTerminalBlock reference, IMyTextPanel lcd)
    {
        this.reference = reference;
        this.lcd = lcd;
    }

    public double Speed
    {
        get { return speed; }
    }

    public void Update()
    {
        Vector3D currentPosition = reference.GetPosition();
        double distanceMoved = (currentPosition - previousPosition).Length();
        previousPosition = currentPosition;

        long ticks = GetTicks();
        long ticksDelta = ticks - previousTicks;
        previousTicks = ticks;
        if (ticksDelta == 0) {
            speed = 0;
        }

        speed = distanceMoved * TimeSpan.TicksPerSecond / ticksDelta;

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("--- Speedometer ---");
        sb.AppendLine($"distanceMoved: {distanceMoved:0.000}m");
        // sb.AppendLine($"ticksDelta: {ticksDelta}");
        sb.AppendLine($"Speed: {speed:0.0}m/s");
        WriteToPanel(lcd, sb.ToString());
    }
}

class Accelerometer
{
    // private IMyTerminalBlock reference;
    private Speedometer MySpeedometer;
    private IMyTextPanel lcd;
    private Vector3D previousPosition = new Vector3D(0, 0, 0);
    private double previousSpeed = 0;
    private long previousTicks = 0;
    private double acceleration = 0;

    public Accelerometer(IMyTerminalBlock reference, IMyTextPanel lcd)
    {
        // this.reference = reference;
        this.MySpeedometer = new Speedometer(reference, lcd);
        this.lcd = lcd;
    }

    public double Acceleration
    {
        get { return acceleration; }
    }

    public void Update()
    {
        long ticks = GetTicks();
        long ticksDelta = ticks - previousTicks;
        previousTicks = ticks;

        MySpeedometer.Update();
        double speedDelta = MySpeedometer.Speed - previousSpeed;
        previousSpeed = MySpeedometer.Speed;

        acceleration = speedDelta * TimeSpan.TicksPerSecond / ticksDelta;

        StringBuilder sb = new StringBuilder();
        // sb.AppendLine("--- Speedometer ---");
        // sb.AppendLine($"distanceMoved: {distanceMoved:0.000}m");
        // // sb.AppendLine($"ticksDelta: {ticksDelta}");
        // sb.AppendLine($"Speed: {speed:0.0}m/s");
        sb.AppendLine("--- Accelerometer ---");
        sb.AppendLine($"speedDelta: {speedDelta:0.00}m/s");
        sb.AppendLine($"acceleration: {acceleration:0.0}m/s^2");
        WriteToPanel(lcd, sb.ToString());
    }
}

// class Speedometer
// {
//     private IMyTerminalBlock reference;
//     private IMyTextPanel lcd;
//     private Vector3D previousPosition = new Vector3D(0, 0, 0);
//     // private long previousTimeMs = 0;
//     private long previousTicks = 0;
//     private double speed = 0;

//     public Speedometer(IMyTerminalBlock reference, IMyTextPanel lcd)
//     {
//         this.reference = reference;
//         this.lcd = lcd;
//     }

//     public double Speed
//     {
//         get { return speed; }
//     }

//     public void Update()
//     {
//         Vector3D currentPosition = reference.GetPosition();
//         double distanceMoved = (currentPosition - previousPosition).Length();
//         previousPosition = currentPosition;

//         long ticks = GetTicks();
//         long ticksDelta = ticks - previousTicks;
//         previousTicks = ticks;
//         if (ticksDelta == 0) {
//             speed = 0;
//         }

//         speed = distanceMoved * TimeSpan.TicksPerSecond / ticksDelta;

//         StringBuilder sb = new StringBuilder();
//         sb.AppendLine("--- Speedometer ---");
//         sb.AppendLine($"distanceMoved: {distanceMoved:0.000}m");
//         sb.AppendLine($"ticksDelta: {ticksDelta}");
//         sb.AppendLine($"Speed: {speed:0.0}m/s");
//         WriteToPanel(lcd, sb.ToString());
//     }
// }

// class Accelerometer
// {
//     private IMyTerminalBlock reference;
//     private Vector3D previousPosition = new Vector3D(0, 0, 0);
//     private long previousTicks = 0;
//     private double previousSpeed = 0;
//     private double speed = 0;
//     private double acceleration = 0;

//     // private Speedometer MySpeedometer;
//     private IMyTextPanel lcd;

//     public Accelerometer(IMyTerminalBlock reference, IMyTextPanel lcd)
//     {
//         this.reference = reference;
//         this.lcd = lcd;
//     }

//     public double Speed
//     {
//         get { return speed; }
//     }

//     public double Acceleration
//     {
//         get { return acceleration; }
//     }

//     public void Update()
//     {
//         long ticks = GetTicks();
//         long ticksDelta = ticks - previousTicks;
//         previousTicks = ticks;


//         Vector3D currentPosition = reference.GetPosition();
//         double distanceMoved = (currentPosition - previousPosition).Length();
//         previousPosition = currentPosition;

//         if (ticksDelta == 0) {
//             speed = 0;
//         } else {
//             speed = distanceMoved * TimeSpan.TicksPerSecond / ticksDelta;
//         }

//         double speedDelta = speed - previousSpeed;
//         previousSpeed = speed;

//         // acceleration = speedDelta * 1000.0 / timeMsDelta;
//         acceleration = speedDelta * TimeSpan.TicksPerSecond / ticksDelta;

//         StringBuilder sb = new StringBuilder();
//         sb.AppendLine("--- Speedometer ---");
//         sb.AppendLine($"distanceMoved: {distanceMoved:0.000}m");
//         // sb.AppendLine($"ticksDelta: {ticksDelta}");
//         sb.AppendLine($"Speed: {speed:0.0}m/s");
//         sb.AppendLine("--- Accelerometer ---");
//         sb.AppendLine($"speedDelta: {speedDelta:0.00}m/s");
//         sb.AppendLine($"acceleration: {acceleration:0.0}m/s^2");
//         WriteToPanel(lcd, sb.ToString());
//     }
// }



private double CalculateDistance(Vector3D dst, Vector3D src) {
    return Math.Sqrt(Math.Pow( dst.X - src.X, 2 ) + Math.Pow( dst.Y - src.Y, 2 ) + Math.Pow( dst.Z - src.Z, 2 ));
} 

public long UnixTimeNow()
{
    var timeSpan = (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0));
    return (long)timeSpan.TotalSeconds;
}

public static long GetTimeInMs()
{
    return DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
}

public static long GetTicks()
{
    return DateTime.Now.Ticks;
}

public List<IMyThrust> FindThrustersWithMatchingOrientation(Vector3D orientation)
{
    Matrix myOrientation; 
    return FilterBlocks<IMyThrust>(thruster => {
        thruster.Orientation.GetMatrix(out myOrientation);
        return myOrientation.Forward == orientation;
    });
}

public List<IMyThrust> FindForwardThrusters()
{
    Matrix rcOrientation; 
    remoteControl.Orientation.GetMatrix(out rcOrientation); 
    // note: thrusters face the way they exhaust
    return FindThrustersWithMatchingOrientation(rcOrientation.Backward);
}

public List<IMyThrust> FindBackwardThrusters()
{
    Matrix rcOrientation; 
    remoteControl.Orientation.GetMatrix(out rcOrientation); 
    // note: thrusters face the way they exhaust
    return FindThrustersWithMatchingOrientation(rcOrientation.Forward);
}

public void ReleaseThrusters() {
    FilterBlocks<IMyThrust>().ForEach(t => {
        t.ThrustOverridePercentage = 0;
        t.Enabled = true;
    });
    StringBuilder sb = new StringBuilder();
    sb.AppendLine("STOPPED");
    WriteToPanel(lcd4, sb.ToString());
}

public static void WriteToPanel(IMyTextPanel lcd, string message)
{
    if (lcd == null) {
        return;
    }
    lcd.ContentType = ContentType.TEXT_AND_IMAGE;
    lcd.FontSize = 1.5F;
    lcd.Alignment = VRage.Game.GUI.TextPanel.TextAlignment.LEFT;
    lcd.WriteText(message);
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

public T GetFirstBlockOfType<T>() where T : class, IMyTerminalBlock
{
    return FilterBlocks<T>().First();
}
