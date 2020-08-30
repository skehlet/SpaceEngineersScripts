// ithilyn's script
bool doUpdateLcds = false;
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

const double RAD_TO_DEG = 180.0 / Math.PI;



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
    // Vector3D.TryParse(someString, out someVector)

    if ((updateSource & (UpdateType.Update1 | UpdateType.Update10 | UpdateType.Update100)) != 0) {
        if (doUpdateLcds) {
            UpdateLcds();
        }
        if (doRotate) {
            RotateToEarth();
        }

    } else {
        if (argument == "STOP") {
            Echo("STOP");
            Runtime.UpdateFrequency = UpdateFrequency.None;
            doUpdateLcds = false;
            doRotate = false;
            ReleaseGyros();
        } else if (argument == "LCDS") {
            Echo("LCDS");
            Runtime.UpdateFrequency = UpdateFrequency.Update10; 
            doUpdateLcds = !doUpdateLcds;
        } else if (argument == "ROTATE") {
            Echo("ROTATE");
            Runtime.UpdateFrequency = UpdateFrequency.Update10; 
            doRotate = !doRotate;
        } else {
            Echo("INVALID ARG" + argument);
            return;
        }
    }
}

public void UpdateLcds()
{
    StringBuilder sb = new StringBuilder();

    // "Give me my GPS (my position in the World)"
    Vector3D wGps = remoteControl.WorldMatrix.Translation; // block.WorldMatrix.Translation is the same as block.GetPosition() btw
    sb.AppendLine($"GPS X: {wGps.X:0.00}");
    sb.AppendLine($"GPS Y: {wGps.Y:0.00}");
    sb.AppendLine($"GPS Z: {wGps.Z:0.00}");

    // "Give me the GPS (world position) 3m x, 6m y, and 9m z from my current position"
    Vector3D relPos = new Vector3D(3, 6, 9);
    Vector3D wPos = Vector3D.Transform(relPos, remoteControl.WorldMatrix);
    sb.AppendLine("Rel pos (3, 6, 9) -> World pos:");
    sb.AppendLine($"wPos X: {wPos.X:0.00}");
    sb.AppendLine($"wPos Y: {wPos.Y:0.00}");
    sb.AppendLine($"wPos Z: {wPos.Z:0.00}");


    // TODO: figure this out

    // // sb.AppendLine($"dir: ({wDir.X:0.00}, {wDir.Y:0.00}, {wDir.Z:0.00})");

    // //convert the local vector to a world direction vector
    // Vector3D bodyDirection = new Vector3D(1,2,3);
    // Vector3D wDir = Vector3D.TransformNormal(bodyDirection, remoteControl.WorldMatrix);
    // sb.AppendLine($"wDir: ({wDir.X:0.00}, {wDir.Y:0.00}, {wDir.Z:0.00})");


    WriteToPanel(lcd1, sb.ToString());



    //Get orientation from remoteControl  
    Matrix orientation; 
    remoteControl.Orientation.GetMatrix(out orientation); 
    Vector3D rcForward = orientation.Forward;
    Vector3D rcUp = orientation.Up;
    Vector3D rcLeft = orientation.Left;

    sb.Clear();
    sb.AppendLine("RC vs Gyros -------");
    sb.AppendLine($"rc F: ({rcForward.X:0.00}, {rcForward.Y:0.00}, {rcForward.Z:0.00})");
    for (int i = 0; i < gyros.Count; i++) {
        IMyGyro gyro = gyros[i];
        gyro.Orientation.GetMatrix(out orientation);
        Vector3D gyroForward = Vector3D.Transform(rcForward, MatrixD.Transpose(orientation));
        sb.AppendLine($"G{i} F: ({gyroForward.X:0.00}, {gyroForward.Y:0.00}, {gyroForward.Z:0.00})");
    }

    sb.AppendLine($"rc U: ({rcUp.X:0.00}, {rcUp.Y:0.00}, {rcUp.Z:0.00})");
    for (int i = 0; i < gyros.Count; i++) {
        IMyGyro gyro = gyros[i];
        gyro.Orientation.GetMatrix(out orientation); 
        Vector3D gyroUp = Vector3D.Transform(rcUp, MatrixD.Transpose(orientation)); 
        sb.AppendLine($"G{i} U: ({gyroUp.X:0.00}, {gyroUp.Y:0.00}, {gyroUp.Z:0.00})");
    }

    sb.AppendLine($"rc L: ({rcLeft.X:0.00}, {rcLeft.Y:0.00}, {rcLeft.Z:0.00})");
    for (int i = 0; i < gyros.Count; i++) {
        IMyGyro gyro = gyros[i];
        gyro.Orientation.GetMatrix(out orientation);
        Vector3D gyroLeft = Vector3D.Transform(rcLeft, MatrixD.Transpose(orientation));
        sb.AppendLine($"G{i} L: ({gyroLeft.X:0.00}, {gyroLeft.Y:0.00}, {gyroLeft.Z:0.00})");
    }

    sb.AppendLine("RC Forward Rel vs Abs -------");
    // Body Direction to World Direction
    // convert the local, normalized rcForward vector to a world direction (normalized) vector
    Vector3D absForward = Vector3D.TransformNormal(rcForward, remoteControl.WorldMatrix);
    sb.AppendLine($"rc F: ({rcForward.X:0.00}, {rcForward.Y:0.00}, {rcForward.Z:0.00})");
    sb.AppendLine($"abs F: ({absForward.X:0.00}, {absForward.Y:0.00}, {absForward.Z:0.00})");

    WriteToPanel(lcd2, sb.ToString());


    // sb.Clear();

    // // // World Direction to Body Direction
    // // Vector3D earthGps = new Vector3D(0, 0, 0); // Doh! this isa  position not a distance vector
    // // Vector3D relV = Vector3D.TransformNormal(earthGps, MatrixD.Transpose(remoteControl.WorldMatrix));
    // // sb.AppendLine($"relV: ({relV.X:0.00}, {relV.Y:0.00}, {relV.Z:0.00})");


    // WriteToPanel(lcd3, sb.ToString());
}

public void RotateToEarth() {
    StringBuilder sb = new StringBuilder();
    sb.AppendLine($"Gyro -> Earth ---------");

    // Trying Archon's auto-leveler technique here:
    // //Get orientation from remoteControl  
    // Matrix orientation; 
    // remoteControl.Orientation.GetMatrix(out orientation); 
    // Vector3D rcForward = orientation.Forward;
    // Vector3D earthPos = new Vector3D(0, 0, 0);

    // for (int i = 0; i < gyros.Count; i++) {
    //     IMyGyro gyro = gyros[i];
    //     gyro.Orientation.GetMatrix(out orientation);
    //     // Vector3D gyroForward = Vector3D.Transform(rcForward, MatrixD.Transpose(orientation));

    //     // World direction
    //     // Vector3D earthDir = earthPos - gyro.WorldMatrix.Translation;
    //     Vector3D earthDir = gyro.WorldMatrix.Translation - earthPos; // not sure on the direction here
    //     sb.AppendLine($"G{i} dir: ({earthDir.X:0.00}, {earthDir.Y:0.00}, {earthDir.Z:0.00})");
    //     earthDir.Normalize();

    //     // World direction to Body direction
    //     Vector3D gyroEarth = Vector3D.TransformNormal(earthDir, MatrixD.Transpose(gyro.WorldMatrix)); 
    //     sb.AppendLine($"G{i} E: ({gyroEarth.X:0.00}, {gyroEarth.Y:0.00}, {gyroEarth.Z:0.00})");



    //     // var localDown = Vector3D.Transform(down, MatrixD.Transpose(orientation)); 
    //     var localForward = Vector3D.Transform(rcForward, MatrixD.Transpose(orientation)); 
    //     sb.AppendLine($"G{i} LF: ({localForward.X:0.00}, {localForward.Y:0.00}, {localForward.Z:0.00})");
 
    //     // var localGrav = Vector3D.Transform(grav, MatrixD.Transpose(g.WorldMatrix.GetOrientation())); 
    //     var localEarth = Vector3D.Transform(gyroEarth, MatrixD.Transpose(gyro.WorldMatrix.GetOrientation())); 
    //     sb.AppendLine($"G{i} LE: ({localEarth.X:0.00}, {localEarth.Y:0.00}, {localEarth.Z:0.00})");



    //     double CTRL_COEFF = 0.3;
    //     var rot = Vector3D.Cross(localForward, localEarth); 
    //     double ang = rot.Length(); 
    //     ang = Math.Atan2(ang, Math.Sqrt(Math.Max(0.0, 1.0 - ang * ang))); 
    //     double ctrl_vel = gyro.GetMaximum<float>("Yaw") * (ang / Math.PI) * CTRL_COEFF; 

    //     ctrl_vel = Math.Min(gyro.GetMaximum<float>("Yaw"), ctrl_vel); 
    //     ctrl_vel = Math.Max(0.01, ctrl_vel); //Gyros don't work well at very low speeds 
    //     rot.Normalize(); 
    //     rot *= ctrl_vel; 
    //     sb.AppendLine($"Pitch: {(float)rot.GetDim(0)}");
    //     sb.AppendLine($"Yaw: -{(float)rot.GetDim(1)}");
    //     sb.AppendLine($"Roll: -{(float)rot.GetDim(2)}");
    //     gyro.SetValueFloat("Pitch", (float)rot.GetDim(0)); 
    //     gyro.SetValueFloat("Yaw", -(float)rot.GetDim(1)); 
    //     gyro.SetValueFloat("Roll", -(float)rot.GetDim(2)); 

    //     gyro.SetValueFloat("Power", 1.0f); 
    //     gyro.SetValueBool("Override", true); 
    // }

    Vector3D earthPos = new Vector3D(0, 0, 0);

    sb.Clear();
    ArchonsTechnique(sb, remoteControl, earthPos);
    WriteToPanel(lcd3, sb.ToString());

    double pitch = 0;
    double yaw = 0;

    sb.Clear();
    // ProjectionTechnique(sb, remoteControl, earthPos, out pitch, out yaw);
    ProjectionTechnique(sb, remoteControl, earthPos, out pitch, out yaw);
    sb.AppendLine($"Pitch: {pitch}");
    sb.AppendLine($"Yaw: {yaw}");


    // // Given pitch and yaw, actually rotate the ship:
    // if (Math.Abs(pitch) < 0.1 && Math.Abs(yaw) < 0.1) {
    //     doRotate = false;
    //     ReleaseGyros();
    //     return;
    // }

    // for (int i = 0; i < gyros.Count; i++) {
    //     // TODO: this assumes the gyro is in alignment with the remote control. Need to do vector math to convert if not
    //     IMyGyro gyro = gyros[i];
    //     float pitchRpms = calculateGyroRpms(gyro, pitch, sb);
    //     float yawRpms = calculateGyroRpms(gyro, yaw, sb);
    //     sb.AppendLine($"pitchRpms: {pitchRpms}");
    //     sb.AppendLine($"yawRpms: {yawRpms}");
    //     // gyro.SetValueFloat("Pitch", pitchRpms);
    //     // gyro.SetValueFloat("Yaw", yawRpms);
    //     // gyro.SetValueFloat("Power", 1.0f);
    //     // gyro.SetValueBool("Override", true);
    // }

    WriteToPanel(lcd4, sb.ToString());
}

public float calculateGyroRpms(IMyGyro gyro, double degrees, StringBuilder sb) {
    float maxRpms = gyro.GetMaximum<float>("Pitch"); // same for roll and yaw, is 60 for small grids, TODO: confirm


    double percent = Math.Abs(degrees / 180.0);
    double rpms = maxRpms * percent;


    // rpms = Math.Min(maxRpms, rpms);
    rpms = Math.Min(10, rpms);
    rpms = Math.Max(0.01, rpms);

    // sb.AppendLine("maxRpms: " + maxRpms);
    // sb.AppendLine("percent: " + percent);
    // sb.AppendLine("rpms: " + rpms);
    // sb.AppendLine("rpms2: " + rpms);

    float rpmsFloat = Convert.ToSingle(rpms);
    if (degrees > 0) {
        rpmsFloat *= -1;
    }

    return rpmsFloat;
}

// Based on Archon's Autolevel script
void ArchonsTechnique(StringBuilder sb, IMyTerminalBlock reference, Vector3D targetPos)
{
    Vector3D target = Vector3D.Normalize(targetPos - reference.GetPosition());

    Matrix orientation; 
    reference.Orientation.GetMatrix(out orientation); 
    Vector3D forward = orientation.Forward; 

    double CTRL_COEFF = 1;

    for (int i = 0; i < gyros.Count; ++i) 
    { 
        var g = gyros[i]; 
 
        g.Orientation.GetMatrix(out orientation); 
        var localForward = Vector3D.Transform(forward, MatrixD.Transpose(orientation)); 
        // sb.AppendLine($"LF: ({localForward.X:0.00}, {localForward.Y:0.00}, {localForward.Z:0.00})");

        var localTarget = Vector3D.Transform(target, MatrixD.Transpose(g.WorldMatrix.GetOrientation())); 
        // sb.AppendLine($"LT: ({localTarget.X:0.00}, {localTarget.Y:0.00}, {localTarget.Z:0.00})");
 
        Vector3D cross = Vector3D.Cross(localForward, localTarget); 
        sb.AppendLine($"X: X: {cross.X:0.00}, Y: {cross.Y:0.00}, Z: {cross.Z:0.00}");

        // determine a "boost" factor
        // Some serious voodoo here, but it's very smooth
        double magnitude = cross.Length(); 
        sb.AppendLine($"magnitude: {magnitude}");
        magnitude = Math.Atan2(magnitude, Math.Sqrt(Math.Max(0.0, 1.0 - magnitude * magnitude))); 
        sb.AppendLine($"voodoo: {magnitude}");

        if (magnitude < 0.01) {
            sb.AppendLine("DONE");
            doRotate = false;
            ReleaseGyros();
            return;
        }

        double boost = g.GetMaximum<float>("Yaw") * (magnitude / Math.PI) * CTRL_COEFF; 
        boost = Math.Min(g.GetMaximum<float>("Yaw"), boost); 
        boost = Math.Max(0.01, boost); //Gyros don't work well at very low speeds 
        sb.AppendLine($"boost: {boost}");

        cross.Normalize(); 
        sb.AppendLine($"X: X: {cross.X:0.00}, Y: {cross.Y:0.00}, Z: {cross.Z:0.00}");
        cross *= boost; 
        sb.AppendLine($"X: X: {cross.X:0.00}, Y: {cross.Y:0.00}, Z: {cross.Z:0.00}");

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


void ProjectionTechnique(StringBuilder sb, IMyTerminalBlock reference, Vector3D targetPos, out double pitch, out double yaw)
{
    Vector3D target = targetPos - reference.WorldMatrix.Translation;
    target.Normalize();
    Vector3D forward = reference.WorldMatrix.Forward;
    Vector3D up = reference.WorldMatrix.Up;
    Vector3D left = reference.WorldMatrix.Left;

    // To get the pitch, project target vector onto a plane comprised of the forward and up vectors 
    // To get the yaw, project target vector onto a plane comprised of the forward and left vectors 

    Vector3D projectedTargetForward;
    Vector3D projectedTargetUp;
    Vector3D projectedTargetLeft;

    Projection(ref target, ref forward, out projectedTargetForward);
    Projection(ref target, ref up, out projectedTargetUp);
    Vector3D projectedTarget = projectedTargetUp + projectedTargetForward;
    pitch = AngleBetween(ref forward, ref projectedTarget) * RAD_TO_DEG;
    Vector3D cross = Vector3D.Cross(forward, projectedTarget);
    if (Vector3D.Dot(left, cross) > 0) {
        pitch *= -1;
    }

    Projection(ref target, ref left, out projectedTargetLeft);
    projectedTarget = projectedTargetLeft + projectedTargetForward;
    yaw = AngleBetween(ref forward, ref projectedTarget) * RAD_TO_DEG;
    cross = Vector3D.Cross(forward, projectedTarget);
    if (Vector3D.Dot(up, cross) < 0) {
        yaw *= -1;
    }
}

// This is Whip's code from planetary compass script
/// <summary>
/// Projects vector a onto vector b
/// </summary>
public void Projection(ref Vector3D a, ref Vector3D b, out Vector3D result)
{
    if (IsZero(ref a) || IsZero(ref b))
    {
        result = Vector3D.Zero;
        return;
    }

    double dot;
    if (Vector3D.IsUnit(ref b))
    {
        Vector3D.Dot(ref a, ref b, out dot);
        Vector3D.Multiply(ref b, dot, out result);
        return;
    }

    double lenSq;
    Vector3D.Dot(ref a, ref b, out dot);
    lenSq = b.LengthSquared();
    Vector3D.Multiply(ref b, dot / lenSq, out result);
}

// originally from Whip's code but updated
/// <summary>
/// Computes angle between 2 vectors in radians.
/// </summary>
// https://www.wikihow.com/Find-the-Angle-Between-Two-Vectors
public double AngleBetween(ref Vector3D a, ref Vector3D b)
{
    if (IsZero(ref a) || IsZero(ref b))
    {
        return 0;
    }

    // // https://www.wikihow.com/Find-the-Angle-Between-Two-Vectors
    // This uses the dot product formula, solving for the angle
    double dot;
    Vector3D.Dot(ref a, ref b, out dot);
    return Math.Acos(MathHelper.Clamp(dot / (a.Length() * b.Length()), -1, 1));
}

// from Whip's code
public bool IsZero(ref Vector3D v, double epsilon = 1e-4)
{
    if (Math.Abs(v.X) > epsilon) return false;
    if (Math.Abs(v.Y) > epsilon) return false;
    if (Math.Abs(v.Z) > epsilon) return false;
    return true;
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
