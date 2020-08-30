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

    // Trying law of cosines technique here:
    sb.Clear();
    sb.AppendLine("Law of Cos technique ----");
    Vector3D earthPos = new Vector3D(0, 0, 0);
    double pitch = 0;
    double yaw = 0;
    GetDirectionTo(earthPos, remoteControl, forwardCamera, upCamera, rightCamera, ref pitch, ref yaw);
    sb.AppendLine($"Yaw: {yaw}");
    sb.AppendLine($"Pitch: {pitch}");
    // these values are in degrees
    WriteToPanel(lcd3, sb.ToString());



    // Trying Whip's compass technique here
    sb.Clear();
    sb.AppendLine("Projection technique ----");

    ProjectionTechnique(sb, remoteControl, earthPos, out pitch, out yaw);
    sb.AppendLine($"Pitch: {pitch}");
    sb.AppendLine($"Yaw: {yaw}");





    // // Given pitch and yaw, actually rotate the ship:

    if (Math.Abs(pitch) < 0.1 && Math.Abs(yaw) < 0.1) {
        doRotate = false;
        ReleaseGyros();
        return;
    }

    for (int i = 0; i < gyros.Count; i++) {
        // TODO: this assumes the gyro is in alignment with the remote control. Need to do vector math to convert if not
        IMyGyro gyro = gyros[i];
        float pitchRpms = calculateGyroRpms(gyro, pitch, sb);
        float yawRpms = calculateGyroRpms(gyro, yaw, sb);
        sb.AppendLine($"pitchRpms: {pitchRpms}");
        sb.AppendLine($"yawRpms: {yawRpms}");
        gyro.SetValueFloat("Pitch", pitchRpms);
        gyro.SetValueFloat("Yaw", yawRpms);
        gyro.SetValueFloat("Power", 1.0f);
        gyro.SetValueBool("Override", true);
    }

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



// From: https://forum.keenswh.com/threads/getdirectionto-function.7242345/
/*
VRageMath.Vector3D TV         This is the target vector.
IMyTerminalBlock Origin       Origin block.
IMyTerminalBlock Forward      Block directly in front of the origin.
IMyTerminalBlock Up           Block directly above the origin.
IMyTerminalBlock Right        Block directly to the right of the origin.
ref double Pitch              Reference to Pitch.
ref double Yaw                Reference to Yaw.
*/
void GetDirectionTo(VRageMath.Vector3D T, IMyTerminalBlock Origin, IMyTerminalBlock Forward, IMyTerminalBlock Up, IMyTerminalBlock Right, ref double Pitch, ref double Yaw)
{
    VRageMath.Vector3D O = Origin.GetPosition();     //Get positions of reference blocks.
    VRageMath.Vector3D F = Forward.GetPosition();
    VRageMath.Vector3D U = Up.GetPosition();
    VRageMath.Vector3D R = Right.GetPosition();

    double TO = (O - T).Length();     //Get magnitudes of vectors.

    double TF = (F - T).Length();
    double TU = (U - T).Length();
    double TR = (R - T).Length();

    double OF = (F - O).Length();
    double OU = (U - O).Length();
    double OR = (R - O).Length();

    double ThetaP = Math.Acos((TU * TU - OU * OU - TO * TO) / (-2 * OU * TO));     //Use law of cosines to determine angles.
    double ThetaY = Math.Acos((TR * TR - OR * OR - TO * TO) / (-2 * OR * TO));

    double RPitch = 90 - (ThetaP * 180 / Math.PI);     //Convert from radians to degrees.
    double RYaw = 90 - (ThetaY * 180 / Math.PI);

    if (TO < TF) RPitch = 180 - RPitch;     //Normalize angles to -180 to 180 degrees.
    if (RPitch > 180) RPitch = -1 * (360 - RPitch);

    if (TO < TF) RYaw = 180 - RYaw;
    if (RYaw > 180) RYaw = -1 * (360 - RYaw);

    Pitch = RPitch;     //Set Pitch and Yaw outputs.
    Yaw = RYaw;
}

void ProjectionTechnique(StringBuilder sb, IMyTerminalBlock reference, Vector3D targetPos, out double pitch, out double yaw)
{
    // In Vector3D: (not sure this is right)
    // Left/Right is X
    // Up/Down is Y
    // Forward/Backward is Z

    // sb.AppendLine($"T pos: ({targetPos.X:0.00}, {targetPos.Y:0.00}, {targetPos.Z:0.00})");
    Vector3D target = targetPos - reference.WorldMatrix.Translation;
    target.Normalize();
    // sb.AppendLine($"T: ({target.X:0.00}, {target.Y:0.00}, {target.Z:0.00})");
    // sb.AppendLine($"T: ({target.GetDim(3):0.00}, {target.GetDim(1):0.00}, {target.GetDim(2):0.00})");

    Vector3D forward = reference.WorldMatrix.Forward;
    Vector3D up = reference.WorldMatrix.Up;
    Vector3D left = reference.WorldMatrix.Left;
    // sb.AppendLine($"F: ({forward.X:0.00}, {forward.Y:0.00}, {forward.Z:0.00})");
    // sb.AppendLine($"U: ({up.X:0.00}, {up.Y:0.00}, {up.Z:0.00})");
    // sb.AppendLine($"L: ({left.X:0.00}, {left.Y:0.00}, {left.Z:0.00})");

    // To get the pitch, project target vector onto a plane comprised of the forward and up vectors 
    Vector3D targetProjForwardVec;
    Projection(ref target, ref forward, out targetProjForwardVec);
    // sb.AppendLine($"PF: ({targetProjForwardVec.X:0.00}, {targetProjForwardVec.Y:0.00}, {targetProjForwardVec.Z:0.00})");
    Vector3D targetProjUpVec;
    Projection(ref target, ref up, out targetProjUpVec);
    // sb.AppendLine($"PU: ({targetProjUpVec.X:0.00}, {targetProjUpVec.Y:0.00}, {targetProjUpVec.Z:0.00})");
    Vector3D targetProjPlaneVec = targetProjUpVec + targetProjForwardVec;
    // sb.AppendLine($"TPPV: ({targetProjPlaneVec.X:0.00}, {targetProjPlaneVec.Y:0.00}, {targetProjPlaneVec.Z:0.00})");

    pitch = AngleBetween(ref forward, ref targetProjPlaneVec) * RAD_TO_DEG;
    // sb.AppendLine("Pitch 1: " + pitch);

    Vector3D cross = Vector3D.Cross(forward, targetProjPlaneVec);
    if (Vector3D.Dot(left, cross) > 0) {
        pitch *= -1;
    }



    // pitch = AngleBetween2(
    //     new Vector2D(forward.Z, forward.X), 
    //     new Vector2D(targetProjPlaneVec.Z, targetProjPlaneVec.X)
    // ) * RAD_TO_DEG;
    // sb.AppendLine("Pitch 2: " + pitch);


    // To get the yaw, project target vector onto a plane comprised of the forward and left vectors 
    Vector3D targetProjLeftVec;
    Projection(ref target, ref left, out targetProjLeftVec);
    // sb.AppendLine($"PR: ({targetProjLeftVec.X:0.00}, {targetProjLeftVec.Y:0.00}, {targetProjLeftVec.Z:0.00})");
    Vector3D targetProjPlaneVec2 = targetProjLeftVec + targetProjForwardVec;
    // sb.AppendLine($"TPPV2: ({targetProjPlaneVec2.X:0.00}, {targetProjPlaneVec2.Y:0.00}, {targetProjPlaneVec2.Z:0.00})");

    yaw = AngleBetween(ref forward, ref targetProjPlaneVec2) * RAD_TO_DEG;

    cross = Vector3D.Cross(forward, targetProjPlaneVec2);
    if (Vector3D.Dot(up, cross) < 0) {
        yaw *= -1;
    }



    // yaw = AngleBetween2(
    //     new Vector2D(left.Y, left.X),
    //     new Vector2D(targetProjPlaneVec2.Y, targetProjPlaneVec2.X)
    // ) * RAD_TO_DEG;
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
    // This uses the dot product formula, but solves for the angle
    double dot;
    Vector3D.Dot(ref a, ref b, out dot);
    return Math.Acos(MathHelper.Clamp(dot / (a.Length() * b.Length()), -1, 1));

    // https://www.mathworks.com/matlabcentral/answers/180131-how-can-i-find-the-angle-between-two-vectors-including-directional-information#:~:text=Secondly%2C%20the%20angle%20between%20vectors,from%20it%2C%20making%20it%20negative.
    //  a = atan2d(x1*y2 - y1*x2, x1*x2 + y1*y2);
}

// // public double AngleBetween2(double v1x, double v1y, double v2x, double v2y)
// public double AngleBetween2(Vector2D a, Vector2D b)
// {
//     // Using atan2 to find angle between two vectors
//     // https://stackoverflow.com/a/21484228/296829
//     // angle = atan2(vector2.y, vector2.x) - atan2(vector1.y, vector1.x);
//     // if (angle > M_PI)        { angle -= 2 * M_PI; }
//     // else if (angle <= -M_PI) { angle += 2 * M_PI; }

//     double angle = Math.Atan2(b.Y, b.X) - Math.Atan2(a.Y, a.X);
//     if (angle > Math.PI) {
//         angle -= 2 * Math.PI;
//     } else if (angle <= -Math.PI) {
//         angle += 2 * Math.PI;
//     }
//     return angle;
// }

// /// <summary>
// /// Computes cosine of the angle between 2 vectors.
// /// </summary>
// public double CosBetween(ref Vector3D a, ref Vector3D b)
// {
//     if (IsZero(ref a) || IsZero(ref b))
//     {
//         return 0;
//     }
//     double dot;
//     Vector3D.Dot(ref a, ref b, out dot);
//     // return MathHelper.Clamp(dot / Math.Sqrt(a.LengthSquared() * b.LengthSquared()), -1, 1);

//     double cos = dot / (a.Length() * b.Length());
//     return MathHelper.Clamp(cos, -1, 1);
// }

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
