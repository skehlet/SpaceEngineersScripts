/* 
*   Cats makes the best programmers! 
 
*   How to use 'gs_Prefix'(Prefix) & 'gs_LCD_Tag'(Tag) 
*   'gs_Prefix' defines the Prefix which appears at the start of target LCD names 
*   The script will only use LCDs that have the Prefix. 
*   For example: With the gs_Prefix '[RC]' an LCD panel with the name '[RC] LCD' will be used but one with the name '[FP] LCD', 'LCD [RC]' or just 'LCD' will not 
*   if gs_Prefix is set to null then it is ignored and lcd screens will be used regardless of their Prefix 
 
*   gs_LCD_Tag' defines the Tag which appeats anywhere in the name of target LCDs 
*   The script will only use LCDs which have the Tag in their name. 
*   For example: with the gs_LCD_Tag '[OUT]' an LCD panel with the name '[OUT] LCD', 'LCD [OUT]' or '[OUT]' will be used, but one with the name 'LCD', 'LCD [IN]' or '[IN] LCD' will not. 
*   if gs_LCD_Tag is set to null then it is ignored and lcd screens will be used regardless of their name 
 
*   setting both Prefix and Tag will mean that only an LCD that matches both conditions will be used 
*   setting both Prefix and Tag to null will result in _ALL_ LCDs being used 
* 
*   'gb_DisableRecall' if false will cause the script to use storage to save its settings and will auto read them on reset or world reload 
*   This will make it quite difficult to change many settings when this is enabled 
*   So if you wish to change the settings by editing the script set this to false, so the script will use whatever defaults you set, write those to storage and then 'gb_DisableRecall' can be set back to true again 
* 
*   Commands: 
*   'DoScan' - Perform a scan 
*   'DoUpdate' - Update Display without performing scan 
*   'Clear_His' - Clears the history 
*   'Set_Rng:<int>' - Set range to int 
*   'Inc_Rng:<int>' - Increase range by int 
*   'Dec_Rng:<int>' - Decrease range by int 
*   'parse_dist:ON|OFF' -  Enables or disables the parsing of distances to  m/km format (default is meters without 'm' indicator) 
*   'show_id:ON|OFF' - Enables or disables the showing of detected entity ID 
*   'show_gps:ON|OFF' - Enables or disables the showing of GPS of detected entity 
*   'show_his:ON|OFF' - Enables or disables the showing of History 
 
*   multiple commands can be seperated by ',' eg: Set_Rng:100,DoScan,Set_Rng:10 (which would set range to 100, do a scan and then reset range to 10) 
*/ 
 
string gs_Prefix = null; // prefix of lcd panel to use (set to null to ignore prefix) 
string gs_LCD_Tag = "[OUT]"; // tag of lcd panel to use (set to null to ignore tag) 
// example of LCd using prefix and tag: "[RC] LCD [OUT]"

// full name of camera to use (prefix and tag only apply to LCDs, set camera name to whatever you like as long as it matches what is in this variable) 
string gs_Cam_Name = "Camera FORWARD"; 


string gs_StoreTag = "CBRF"; // used in storage variable to identify a valid storage value 
char gc_CmdSpace = ','; // used in parsing multiple commands 
char gc_StoreSpace = '|'; // used in storage 

// range of ray to cast (must be non-zero posative ~ script enforces 1 minimum) 
int gi_Range = 2000; 

// Used as LCD title and name of the script 
string gs_Title = "RayCast Range Finder";  // set to anything you want other than null, this has no impact on the script 
string gs_TopLine = "_Long_Range_Scan_Data_"; 

bool gb_DisableRecall = true; // if true the script will not recall settings from storage allowing the settings to be changed more easily by editing the script  
// while gb_DisableRecall is true the script will write to and update the setting storage so it will overwrite all old saved settings 
bool gb_DistParse = true; // parse meters to KM and truncate decimals to the 2 most significant figures 
bool gb_ShowID = true;    // show Detected entity IDs 
bool gb_ShowGPS = true;    // show Detected entity GPS 
bool gb_ShowHistory = true; // show history if nothing detected 

// if gb_Turn_On_Cam is true the script will turn the camera on if disabled 
bool gb_Turn_On_Cam = true; 

// if gb_Scan_Without_Cmd is true a scan will be done every time the programable block is run without a command 
// if gb_Scan_Without_Cmd is false a scan will only be done when the programable block is run with the command "DoScan" (not case sensative) 
bool gb_Scan_Without_Cmd = false; 

/* 
    Edit below here with caution 
*/ 
// types of object detectable by ray 
string[] g_EntityType = { "Large Ship", "Small Ship", "Human", "Entity", "Object", "Asteroid", "Planet", "Meteor", "Missile", "None", "Unknown", "Error" }; 
// relationship of object detected by ray 
string[] g_Relationships = { "Friend", "Ally", "Neutral", "Hostile", "Unowned", "Error" }; 

/* 
    The script sets what is below here 
*/ 
MyDetectedEntityInfo g_ScanDataLog = new MyDetectedEntityInfo();

/*
    Functions
*/
// performed when PB is activated
void Main( string arg ) {
    Echo( "ET: "+ Runtime.TimeSinceLastRun.TotalSeconds ); // EOR
    Echo( " CAT - "+ gs_Title +'\n' );
    if( arg.Length != 0 ) { // recieved a command(s) seperated by ,
        bool scanned = false;
        bool update = false;
        string[] cmds = arg.ToUpper().Split( gc_CmdSpace ); // parse arg by ',' to seperate commands 
        for( int i = 0; i < cmds.Length; i++ ) { // check each command 
            if( ParseCommand( cmds[i], out scanned, scanned ) ) {
                update = true;
            }
        }
        if( update ) { 
            writeStorage(); 
            doUpdate(); 
        } 
    } else { 
        Echo( "No Command Given" ); 
        if( gb_Scan_Without_Cmd ) { // scan in absence of command? 
            Echo( "Running Scan" ); 
            doScan(); 
        } else { 
            Echo( "Performing Update" ); 
            doUpdate(); 
        } 
    }
} 

// performed on reset or world load 
public Program() { 
    Echo( Storage ); 
    if( gb_DisableRecall || !readStorage() ) { // check if settings are to be recovered, if so attempt recovery, if failed recovery update settings 
        Echo( "Updating Storage" ); 
        writeStorage(); // update stored settings 
    }
    setTimer( true );
} 

// set auto update on/off
private void setTimer( bool on ) {
    if( on ) {
        Runtime.UpdateFrequency = UpdateFrequency.Update100;
        return;
    }
    Runtime.UpdateFrequency = UpdateFrequency.None;
}

// read settings from PB storage 
private bool readStorage() { 
    string[] data = Storage.Split( gc_StoreSpace ); 
    if( data[0] == gs_StoreTag && data.Length == 8 ) { 
        if( data[1] == "1" ) { 
            gb_DistParse = true; 
        } else { 
            gb_DistParse = false; 
        } 
        if( data[2] == "1" ) { 
            gb_ShowID = true; 
        } else { 
            gb_ShowID = false; 
        } 
        if( data[3] == "1" ) { 
            gb_ShowGPS = true; 
        } else { 
            gb_ShowGPS = false; 
        } 
        if( data[4] == "1" ) { 
            gb_ShowHistory = true; 
        } else { 
            gb_ShowHistory = false; 
        } 
        if( data[5] == "1" ) { 
            gb_Turn_On_Cam = true; 
        } else { 
            gb_Turn_On_Cam = false; 
        } 
        if( data[6] == "1" ) { 
            gb_Scan_Without_Cmd = true; 
        } else { 
            gb_Scan_Without_Cmd = false; 
        } 
        int rng = 0; 
        if( int.TryParse( data[7], out rng ) ) { 
            gi_Range = rng; 
        } else { 
            gi_Range = 2000; 
            Echo( "failed" ); 
        } 
        return true; 
    } 
    return false; 
} 
 
// write current settings to storage 
private void writeStorage() { 
    StringBuilder sb = new StringBuilder( gs_StoreTag ); 
    if( gb_DistParse ) { 
        sb.Append( gc_StoreSpace ).Append( 1 ); 
    } else { 
        sb.Append( gc_StoreSpace ).Append( 0 ); 
    } 
    if( gb_ShowID ) { 
        sb.Append( gc_StoreSpace ).Append( 1 ); 
    } else { 
        sb.Append( gc_StoreSpace ).Append( 0 ); 
    } 
    if( gb_ShowGPS ) { 
        sb.Append( gc_StoreSpace ).Append( 1 ); 
    } else { 
        sb.Append( gc_StoreSpace ).Append( 0 ); 
    } 
    if( gb_ShowHistory ) { 
        sb.Append( gc_StoreSpace ).Append( 1 ); 
    } else { 
        sb.Append( gc_StoreSpace ).Append( 0 ); 
    } 
    if( gb_Turn_On_Cam ) { 
        sb.Append( gc_StoreSpace ).Append( 1 ); 
    } else { 
        sb.Append( gc_StoreSpace ).Append( 0 ); 
    } 
    if( gb_Scan_Without_Cmd ) { 
        sb.Append( gc_StoreSpace ).Append( 1 ); 
    } else { 
        sb.Append( gc_StoreSpace ).Append( 0 ); 
    } 
    sb.Append( gc_StoreSpace ).Append( gi_Range ); 
    Storage = sb.ToString(); 
} 
 
/* parse single command string to see if it is a valid command 
*  
*  returns true if command changes settings 
*  out bool back will be true if scanned is true, or if scanned if false and a scan is performed 
*  out bool back will be false if scanned is false and a scan is not performed 
*/ 
private bool ParseCommand( string strIn, out bool back, bool scanned = false ) { 
    bool output = false; 
    if( strIn == "DOSCAN" ) { // command is do scan 
        if( scanned != true ){ // havent scanned yet 
            Echo( "Cmd: Performing Scan" ); 
            scanned = true; 
            doScan(); 
        } else { // already scanned 
            Echo( "Cmd: Skip Scan" ); 
        } 
    } else if( strIn == "DOUPDATE" ) { // command is do scan 
        Echo( "Cmd: Performing Scan" ); 
        doUpdate(); 
    } else if( strIn == "CLEAR_HIS" ) { // decrease range command 
        g_ScanDataLog = new MyDetectedEntityInfo(); 
    } else { // check for advanced commands 
        string[] subFrag = strIn.Split( ':' ); 
        if( subFrag.Length == 2 ) { // all comands are made up of a string an an int 
            if( subFrag[0] == "SET_RNG" ) { // set range command 
                int rng = 0; 
                if( Int32.TryParse( subFrag[1], out rng ) ) { 
                    gi_Range = rng; 
                    Echo( "Cmd: Range Set: "+ gi_Range ); 
                } else { 
                    Echo( "Cmd: Range Set: Failed" ); 
                } 
            } else if( subFrag[0] == "INC_RNG" ) { // increase range command 
                int rng = 0; 
                if( Int32.TryParse( subFrag[1], out rng ) ) { 
                    gi_Range += rng; 
                    Echo( "Cmd: Inc Range: "+ gi_Range ); 
                } else { 
                    Echo( "Cmd: Inc Range: Failed" ); 
                } 
            } else if( subFrag[0] == "DEC_RNG" ) { // decrease range command 
                int rng = 0; 
                if( Int32.TryParse( subFrag[1], out rng ) ) { 
                    gi_Range -= rng; 
                    Echo( "Cmd: Dec Range: "+ gi_Range ); 
                } else { 
                    Echo( "Cmd: Dec Range: Failed" ); 
                } 
            } else if( subFrag[0] == "PARSE_DIST" ) { 
                Echo( "Updating Distance" ); 
                if( subFrag[1] == "ON" ) { 
                    gb_DistParse = true; 
                } else { 
                    gb_DistParse = false; 
                } 
            } else if( subFrag[0] == "SHOW_ID" ) { 
                Echo( "Updating Show_ID" ); 
                if( subFrag[1] == "ON" ) { 
                    gb_ShowID = true; 
                } else { 
                    gb_ShowID = false; 
                } 
            } else if( subFrag[0] == "SHOW_GPS" ) { 
                Echo( "Updating Show_GPS" ); 
                if( subFrag[1] == "ON" ) { 
                    gb_ShowGPS = true; 
                } else { 
                    gb_ShowGPS = false; 
                } 
            } else if( subFrag[0] == "SHOW_HIS" ) { 
                Echo( "Updating Show_History" ); 
                if( subFrag[1] == "ON" ) { 
                    gb_ShowHistory = true; 
                } else { 
                    gb_ShowHistory = false; 
                } 
            } else { // command not recognised 
                Echo( "Bad Command: "+ strIn ); 
            } 
            output = true; 
        } else { // command too short 
            Echo( "Bad Command: "+ strIn ); 
        } 
    } 
    back = scanned; 
    return output; 
} 
 
/* checks the camera for opperation 
*  returns true if camera is set for opperation 
*  returns false if camera is not set for opperation 
*  out string[] status contains data about the camera status 
*   statis[0] camera condition 
*   status[1] available charge 
*   status[2] NA (no longer used) 
*/   
private bool testCam( IMyCameraBlock cam, out string[] status ) { 
    bool clear = true; 
    status = new string[3]; 
    if( cam == null ) { // crit-fail: the camera is destroyed or misnamed 
        Echo( "Err: Camera not found" ); 
        status[0] = "NA"; 
        status[1] = "NA"; 
        status[2] = "NA"; 
        clear = false; 
    } else if( !cam.IsFunctional ) { // crit-fail: the camera is damaged beyond use 
        Echo( "Err: Camera Damaged" ); 
        status[0] = "Damaged"; 
        status[1] = "NA"; 
        status[2] = "NA"; 
        clear = false; 
    } else { 
        if( !cam.IsWorking ) {  // crit/non-crit the camera is turned off 
            if( !gb_Turn_On_Cam ) { // turn cam on if off? 
                Echo( "Err: Camera Offline" ); 
                status[0] = "Offline"; 
                status[1] = cam.AvailableScanRange.ToString(); 
                status[2] = "NA"; 
                clear = false; 
            } else { // cam is off (crit-fail) 
                Echo( "Cam: Powered On" ); 
                cam.ApplyAction( "OnOff_On" ); 
            } 
        } 
        if( !cam.CanScan( gi_Range ) ) { // non-crit the camera doesnt have enough charge 
            Echo( "Err: Camera charge low" ); 
            status[0] = "Low Charge"; 
            status[1] = cam.AvailableScanRange.ToString(); 
            status[2] = "NA"; 
            clear = false; 
            if( !cam.EnableRaycast == true ) {
                cam.EnableRaycast = true;
            } 
        }
        //if( !clear && !cam.EnableRaycast ) { // non-crit the camera not raycasting 
            //Echo( "Cam: Raycast Enabled" ); 
            //cam.EnableRaycast = true; 
        //}
        if( clear ) { 
            status[0] = "Ready"; 
            status[1] = cam.AvailableScanRange.ToString(); 
            status[2] = "NA"; 
            if( gi_Range <= 0 ) { // non-crit, range to low 
                Echo( "Err: Min Range 1" ); 
                gi_Range = 1; 
            } 
            if( !cam.EnableRaycast && (gi_Range*5) > cam.AvailableScanRange ) {
                status[0] = "Charging Started";
                cam.EnableRaycast = true; 
            } else if( cam.EnableRaycast && (gi_Range*5) <= cam.AvailableScanRange ) {
                status[0] = "Charging Finished";
                cam.EnableRaycast = false; 
            } 
        } 
    } 
    return clear; 
} 
 
// update display without performing a new scan 
private void doUpdate() { 
    StringBuilder sb = new StringBuilder( gs_TopLine ).Append( '\n' ); 
    IMyCameraBlock cam = (IMyCameraBlock)GridTerminalSystem.GetBlockWithName( gs_Cam_Name ); 
    string[] status = new string[3]; 
    testCam( cam, out status ); 
    sb.Append( "  Cam Status: " ).Append( status[0] ).Append( '\n' ); 
    sb.Append( "  Cam Charge: " ).Append( status[1] ).Append( '\n' ); 
    sb.Append( "  Target Range: " ).Append( gi_Range ).Append( '\n' ); 
 
    sb.Append( parseHistory( g_ScanDataLog, cam ) ); 
    display( sb.ToString() ); 
} 
 
/*  perform a scan and update display with results 
* 
*   returns true if hit is detected 
*/ 
private bool doScan() { 
    bool hit = false; 
    StringBuilder sb = new StringBuilder( gs_TopLine ).Append( '\n' ); 
    IMyCameraBlock cam = (IMyCameraBlock)GridTerminalSystem.GetBlockWithName( gs_Cam_Name ); 
 
    MyDetectedEntityInfo ScanData = new MyDetectedEntityInfo(); 
    string[] status = new string[3]; 
    if( !testCam( cam, out status ) ) { // test for crit-fail 
        sb.Append( "  Cam Status: " ).Append( status[0] ).Append( '\n' ); 
        sb.Append( "  Cam Charge: " ).Append( status[1] ).Append( '\n' ); 
        sb.Append( "  Target Range: " ).Append( gi_Range ).Append( '\n' ); 
 
        sb.Append( parseHistory( g_ScanDataLog, cam ) ); 
    } else { // all clear 
        sb.Append( "  Cam Status: " ).Append( status[0] ).Append( '\n' ); 
        ScanData = cam.Raycast( gi_Range, 0, 0 ); 
        sb.Append( "  Cam Charge: " ).Append( cam.AvailableScanRange ).Append( '\n' ); // update range post scan 
        sb.Append( "  Target Range: " ).Append( gi_Range ).Append( '\n' ); 
 
        if( ScanData.IsEmpty() ) { 
            sb.Append( "\n  _Detected_\n" ); 
            sb.Append( "   None\n" ); 
 
            sb.Append( parseHistory( g_ScanDataLog, cam ) ); 
        } else { 
            hit = true; 
            g_ScanDataLog = ScanData; 
            sb.Append( "\n  _Detected_\n" ); 
            List<string> data = procDetectedEntity( ScanData ); 
            for( int i = 0; i < data.Count; i++ ) { 
                sb.Append( "   " ).Append( data[i] ).Append( '\n' ); 
            } 
            sb.Append( "   Range: " ).Append( procDist( vecToRange( g_ScanDataLog.Position, cam.GetPosition() ) ) ).Append( '\n' ); 
            sb.Append( "   RTI: " ).Append( procDist( vecToRange( g_ScanDataLog.HitPosition.Value, cam.GetPosition() ) ) ).Append( '\n' ); 
        } 
    } 
    display( sb.ToString() ); 
    return hit; 
} 
 
/* parse history for display 
* 
*   returns a string builder preformated for display with last detected entity values 
*/ 
private StringBuilder parseHistory( MyDetectedEntityInfo entity, IMyCameraBlock cam = null ) { 
    StringBuilder sb = new StringBuilder( ); 
    if( gb_ShowHistory ) { 
        sb.Append( "\n  _History_\n" ); 
        if( !entity.IsEmpty() ) { 
            List<string> data = procDetectedEntity( entity ); 
            for( int i = 0; i < data.Count; i++ ) { 
                sb.Append( "   " ).Append( data[i] ).Append( '\n' ); 
            } 
            if( cam != null ) { 
                sb.Append( "   Range: " ).Append( procDist( vecToRange( entity.Position, cam.GetPosition() ) ) ).Append( '\n' ); 
                sb.Append( "   RTI: " ).Append( procDist( vecToRange( entity.HitPosition.Value, cam.GetPosition() ) ) ).Append( '\n' ); 
            } else { 
                sb.Append( "   Range: Unknown\n" ); 
                sb.Append( "   RTI: Unknown\n" ); 
            } 
        } else { 
            sb.Append( "   None\n" ); 
        } 
    } 
    return sb; 
} 
 
/* process distance values for display 
* 
*   Returns stringbuilder preformated for display 
*/ 
private StringBuilder procDist( double dis ) { 
    StringBuilder sb = new StringBuilder(); 
    if( gb_DistParse ) { 
        string end = "m"; 
        if( dis >= 1000 ) { 
            dis = dis / 1000; 
            end = "km"; 
        } 
        dis = Math.Round( dis, 2 ); 
        sb.Append( dis ).Append( end ); 
    } else { 
        sb.Append( dis ); 
    } 
    return sb; 
} 
 
/* calculate distance between two vectors (which I somehow screwed up horribly originaly) 
* 
*   returned double is the distance between two provided vectors 
*/ 
private double vecToRange( Vector3D tar, Vector3D org ) { 
    return Math.Sqrt(Math.Pow( tar.X - org.X, 2 ) + Math.Pow( tar.Y - org.Y, 2 ) + Math.Pow( tar.Z - org.Z, 2 )); 
} 
 
/* process detected entity for display 
*   returned list contains: name, type, status [gps], speed, [id]. (bracketed items may be disabled in settings) 
*   List format could do with revision.... 
*/ 
private List<string> procDetectedEntity( MyDetectedEntityInfo scanData ) { 
    List<string> data = new List<string>(); 
    data.Add( new StringBuilder( "Name: " ).Append( scanData.Name ).ToString() ); 
    data.Add( new StringBuilder( "Type: " ).Append( g_EntityType[findType( scanData )] ).ToString() ); 
    data.Add( new StringBuilder( "Status: " ).Append( g_Relationships[findRelationship( scanData )] ).ToString() ); 
    if( gb_ShowGPS ) { 
        //data.Add( vecToGPS( scanData.Position, scanData.Name ) ); 
        if (scanData.HitPosition.HasValue) {
            data.Add( vecToGPS( scanData.HitPosition.Value, scanData.Name ) ); 
        }
    } 
    data.Add( new StringBuilder( "Speed: " ).Append( Math.Round( vecMag( scanData.Velocity ), 2 ) ).ToString() ); 
    if( gb_ShowID ) { 
        data.Add( new StringBuilder( "ID: " ).Append( scanData.EntityId ).ToString() ); 
    } 
    return data; 
} 
 
/* get the magnature of a vector 
* 
*   returned double is the magnature of provided vector 
*/ 
private double vecMag( Vector3D vec ) { 
    return Math.Sqrt( Math.Pow(vec.X,2) + Math.Pow(vec.Y,2) + Math.Pow(vec.Z,2) ); 
} 
 
/* vector to string format GPS coordinate 
* 
*   returned string is the SE format GP location 
*/ 
private string vecToGPS( Vector3D vec, string name = "Pos" ) { 
    StringBuilder sb = new StringBuilder( "GPS:" ).Append( name ).Append( ":" ).Append( Math.Floor(vec.X) ).Append( ":" ).Append( Math.Floor(vec.Y) ).Append( ":" ).Append( Math.Floor(vec.Z) ).Append( ':' ); 
    return sb.ToString(); 
} 
 
/* write to display LCDs 
* 
*   returns false if no displays are found 
*   This does not check for damaged displays, may be worth adding that... 
*/ 
private bool display( string data ) {
    doPrint( data ); // auto push to PB Display
    List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
    GridTerminalSystem.GetBlocksOfType<IMyTextPanel>( blocks,
            ( block => ( ( gs_Prefix == null || block.CustomName.StartsWith( gs_Prefix ) )
                    && ( gs_LCD_Tag == null || block.CustomName.Contains( gs_LCD_Tag ) ) ) )
            );
    if( blocks.Count != 0 ) { 
        for( int i = 0; i < blocks.Count; i++ ) { 
            var sp = (blocks[i] as IMyTextSurfaceProvider); // ahh retrofitting code.
            if( sp != null && sp.SurfaceCount >= 1 ) {
                SetScreen( sp.GetSurface( 0 ), data, 0.8f );
            }
        } 
        return true; 
    } 
    Echo( "Err: LCD Not Found" ); 
    Echo( data ); 
    return false; 
} 

private void doPrint( string data ) {
    var sp = (Me as IMyTextSurfaceProvider);
    if( sp == null || sp.SurfaceCount <= 0 ) {
        Echo( "PB Display Not Supported" );
        return;
    }
    SetScreen( sp.GetSurface( 0 ), data, 0.8f );
}

bool GB_RefreshLCDs = true;
public bool SetScreen( IMyTextSurface sur, string strIn, float size = 1.2f ) {
    if( sur != null && strIn != null ) {
        sur.WriteText( strIn, false );
        if( GB_RefreshLCDs ) {
            sur.Font = "Monospace";
            sur.FontColor = new Color( 255,100,100 );
            sur.FontSize = size;
            sur.Alignment = TextAlignment.LEFT;
            sur.TextPadding = 2.5f;
            sur.BackgroundColor = new Color( 0, 0, 0 );
            sur.ContentType = ContentType.TEXT_AND_IMAGE;
            var sprites = new List<string>();
            sur.GetSelectedImages( sprites );
            sur.ClearImagesFromSelection();
        }
        return true;
    }
    Echo( "Set Screen Failed" );
    return false;
}

// returns an int format entity type (indexed to g_EntityType) 
private int findType( MyDetectedEntityInfo entity ) { 
    var type = entity.Type; 
    if( type == MyDetectedEntityType.LargeGrid ) { 
        return 0; 
    } else if( type == MyDetectedEntityType.SmallGrid ) { 
        return 1; 
    } else if( type == MyDetectedEntityType.CharacterHuman ) { 
        return 2; 
    } else if( type == MyDetectedEntityType.CharacterOther ) { 
        return 3; 
    } else if( type == MyDetectedEntityType.FloatingObject ) { 
        return 4; 
    } else if( type == MyDetectedEntityType.Asteroid ) { 
        return 5; 
    } else if( type == MyDetectedEntityType.Planet ) { 
        return 6; 
    } else if( type == MyDetectedEntityType.Meteor ) { 
        return 7; 
    } else if( type == MyDetectedEntityType.Missile ) { 
        return 8; 
    } else if( type == MyDetectedEntityType.None ) { 
        return 9; 
    } else if( type == MyDetectedEntityType.Unknown ) { 
        return 10; 
    } 
    return 11; 
} 
 
// returns an int format relationship (indexed to g_Relationships) 
private int findRelationship( MyDetectedEntityInfo entity ) { 
    if( entity.Relationship == MyRelationsBetweenPlayerAndBlock.Owner ) { 
        return 0; 
    } else if( entity.Relationship == MyRelationsBetweenPlayerAndBlock.FactionShare ) { 
        return 1; 
    } else if( entity.Relationship == MyRelationsBetweenPlayerAndBlock.Neutral ) { 
        return 2; 
    } else if( entity.Relationship == MyRelationsBetweenPlayerAndBlock.Enemies ) { 
        return 3; 
    } else if( entity.Relationship == MyRelationsBetweenPlayerAndBlock.NoOwnership ) { 
        return 4; 
    } 
    return 5; 
} 
