// ithilyn's script
bool doUpdateGps = false;
bool doRotate = false;
IMyTextPanel lcd1;
IMyTextPanel lcd2;
IMyTextPanel lcd3;
IMyTextPanel lcd4;
IMyRemoteControl remoteControl;
List<IMyGyro> gyros;
IMyCameraBlock forwardCamera;
IMyCameraBlock upCamera;
IMyCameraBlock rightCamera;
Vector3D rotateToGps;

const double CTRL_COEFF = 0.75;

public Program()
{
    Runtime.UpdateFrequency = UpdateFrequency.None; 
    lcd1 = GridTerminalSystem.GetBlockWithName("LCD Panel") as IMyTextPanel;
    lcd2 = GridTerminalSystem.GetBlockWithName("LCD Panel 2") as IMyTextPanel;
    lcd3 = GridTerminalSystem.GetBlockWithName("LCD Panel 3") as IMyTextPanel;
    lcd4 = GridTerminalSystem.GetBlockWithName("LCD Panel 4") as IMyTextPanel;
    remoteControl = GetFirstBlockOfType<IMyRemoteControl>();
    gyros = FindGyros();
    forwardCamera = GridTerminalSystem.GetBlockWithName("Camera FORWARD") as IMyCameraBlock;
    upCamera = GridTerminalSystem.GetBlockWithName("Camera UP") as IMyCameraBlock;
    rightCamera = GridTerminalSystem.GetBlockWithName("Camera RIGHT") as IMyCameraBlock;
    Echo("lcd1: " + lcd1);
    Echo("lcd2: " + lcd2);
    Echo("remoteControl: " + remoteControl);
    foreach (IMyGyro gyro in gyros) {
        Echo("gyro: " + gyro);
    }
}

public List<IMyGyro> FindGyros() {
    var blocks = new List<IMyTerminalBlock>(); 
    GridTerminalSystem.GetBlocksOfType<IMyGyro>(blocks, x => x.CubeGrid == Me.CubeGrid); 
    return blocks.ConvertAll(x => (IMyGyro)x); 
}

public void ReleaseGyros() {
    foreach (IMyGyro gyro in gyros) {
        gyro.SetValueBool("Override", false); 
    }
}

public void Main(string argument, UpdateType updateSource)
{
    if ((updateSource & (UpdateType.Update1 | UpdateType.Update10 | UpdateType.Update100)) != 0) {
        if (doUpdateGps) {
            UpdateGps(lcd1);
        }
        if (doRotate) {
            RotateToGps(lcd2);
        }

    } else {
        if (argument == "STOP") {
            Echo("STOP");
            doUpdateGps = false;
            doRotate = false;
            Runtime.UpdateFrequency = UpdateFrequency.None;
            ReleaseGyros();

        } else if (argument == "LCDS") {
            Echo("LCDS");
            doUpdateGps = !doUpdateGps;
            Runtime.UpdateFrequency = UpdateFrequency.Update10; 

        } else if (argument.StartsWith("ROTATE")) {
            Echo("ROTATE");
            doRotate = !doRotate;
            Runtime.UpdateFrequency = UpdateFrequency.Update10; 

            String gpsArg = argument.Substring("ROTATE".Length + 1);
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

            rotateToGps = new Vector3D(gpsX, gpsY, gpsZ);
            Echo("rotateToGps: " + rotateToGps);

        } else {
            Echo("INVALID ARG" + argument);
            return;
        }
    }
}

public void UpdateGps(IMyTextPanel lcd)
{
    StringBuilder sb = new StringBuilder();

    Vector3D wGps = remoteControl.GetPosition();
    sb.AppendLine($"GPS X: {wGps.X:0.00}");
    sb.AppendLine($"GPS Y: {wGps.Y:0.00}");
    sb.AppendLine($"GPS Z: {wGps.Z:0.00}");

    WriteToPanel(lcd, sb.ToString());
}

public void RotateToGps(IMyTextPanel lcd) {
    StringBuilder sb = new StringBuilder();
    sb.AppendLine($"Rotating to {rotateToGps}");
    ArchonsTechnique(sb, remoteControl);
    WriteToPanel(lcd, sb.ToString());
}

// Based on Archon's Autolevel script
void ArchonsTechnique(StringBuilder sb, IMyTerminalBlock reference)
{
    Vector3D target = Vector3D.Normalize(rotateToGps - reference.GetPosition());

    Matrix orientation; 
    reference.Orientation.GetMatrix(out orientation); 
    Vector3D forward = orientation.Forward; 

    for (int i = 0; i < gyros.Count; ++i) 
    { 
        var g = gyros[i]; 
 
        g.Orientation.GetMatrix(out orientation);
        var localForward = Vector3D.Transform(forward, MatrixD.Transpose(orientation)); 
        // sb.AppendLine($"LF: ({localForward.X:0.00}, {localForward.Y:0.00}, {localForward.Z:0.00})");

        var localTarget = Vector3D.Transform(target, MatrixD.Transpose(g.WorldMatrix.GetOrientation())); 
        // sb.AppendLine($"LT: ({localTarget.X:0.00}, {localTarget.Y:0.00}, {localTarget.Z:0.00})");
 
        Vector3D cross = Vector3D.Cross(localForward, localTarget); 
        // sb.AppendLine($"X: X: {cross.X:0.00}, Y: {cross.Y:0.00}, Z: {cross.Z:0.00}");

        // determine a "boost" factor
        // Some serious voodoo here, but it's very smooth
        double magnitude = cross.Length(); 
        // sb.AppendLine($"magnitude: {magnitude}");
        magnitude = Math.Atan2(magnitude, Math.Sqrt(Math.Max(0.0, 1.0 - magnitude * magnitude))); 
        // sb.AppendLine($"voodoo: {magnitude}");

        if (magnitude < 0.01) {
            sb.AppendLine("DONE");
            doRotate = false;
            ReleaseGyros();
            return;
        }

        double boost = g.GetMaximum<float>("Yaw") * (magnitude / Math.PI) * CTRL_COEFF; 
        boost = Math.Min(g.GetMaximum<float>("Yaw"), boost); 
        boost = Math.Max(0.01, boost); //Gyros don't work well at very low speeds 
        // sb.AppendLine($"boost: {boost}");

        cross.Normalize(); 
        // sb.AppendLine($"X: X: {cross.X:0.00}, Y: {cross.Y:0.00}, Z: {cross.Z:0.00}");
        cross *= boost; 
        // sb.AppendLine($"X: X: {cross.X:0.00}, Y: {cross.Y:0.00}, Z: {cross.Z:0.00}");

        double pitch = cross.GetDim(0);
        double yaw = -cross.GetDim(1);
        double roll = -cross.GetDim(2);
        sb.AppendLine($"P: {pitch:0.00}, Y: {yaw:0.00}, R: {roll:0.00}");

        g.SetValueFloat("Pitch", (float)pitch); 
        g.SetValueFloat("Yaw", (float)yaw); 
        g.SetValueFloat("Roll", (float)roll); 
        g.SetValueFloat("Power", 1.0f); 
        g.SetValueBool("Override", true); 
    } 

}

public void WriteToPanel(IMyTextPanel lcd, string message)
{
    if (lcd == null) {
        return;
    }
    lcd.ContentType = ContentType.TEXT_AND_IMAGE;
    lcd.FontSize = 1.5F;
    lcd.Alignment = VRage.Game.GUI.TextPanel.TextAlignment.LEFT;
    lcd.WriteText(message);
}

public T GetFirstBlockOfType<T>() where T : class, IMyTerminalBlock
{
    var blocks = new List<T>();
    GridTerminalSystem.GetBlocksOfType(blocks);
    foreach (var block in blocks)
    {
        if (block.IsSameConstructAs(Me)) return block;
    }
    return null;
}
