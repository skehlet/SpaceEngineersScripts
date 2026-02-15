// Configuration
const string ORE_TYPE = "Platinum";  // Change to ore you want
const double SEARCH_RADIUS = 30000;  // 30km radius
const int MAX_DEPOSITS = 10;         // How many to find

List<Vector3D> detectedOres = new List<Vector3D>();
bool waitingForCallback = false;

public Program()
{
    Runtime.UpdateFrequency = UpdateFrequency.Update100; // Run every 100 ticks
}

public void Main(string argument, UpdateType updateSource)
{
    // Handle manual commands
    if (!string.IsNullOrEmpty(argument))
    {
        if (argument.ToUpper() == "SCAN")
        {
            StartScan();
        }
        else if (argument.ToUpper() == "CLEAR")
        {
            detectedOres.Clear();
            Echo("GPS list cleared");
        }
        else if (argument.ToUpper() == "ADD")
        {
            AddToGPS();
        }
        return;
    }
    
    // Display current status
    Echo($"Ore Type: {ORE_TYPE}");
    Echo($"Search Radius: {SEARCH_RADIUS/1000:F1}km");
    Echo($"Deposits Found: {detectedOres.Count}");
    Echo($"\nScanning: {(waitingForCallback ? "Yes" : "No")}");
    Echo("\nCommands:");
    Echo("- SCAN: Search for ore");
    Echo("- ADD: Add found deposits to GPS");
    Echo("- CLEAR: Clear detected list");
}

void StartScan()
{
    if (waitingForCallback)
    {
        Echo("Scan already in progress...");
        return;
    }
    
    detectedOres.Clear();
    waitingForCallback = true;
    
    BoundingSphereD searchArea = new BoundingSphereD(Me.GetPosition(), SEARCH_RADIUS);
    
    ReforgedDetectN(searchArea, ORE_TYPE, MAX_DEPOSITS, (deposits) =>
    {
        detectedOres = deposits ?? new List<Vector3D>();
        waitingForCallback = false;
        Echo($"Scan complete! Found {detectedOres.Count} deposits");
    });
}

void AddToGPS()
{
    if (detectedOres.Count == 0)
    {
        Echo("No deposits to add. Run SCAN first.");
        return;
    }
    
    int added = 0;
    foreach (var position in detectedOres)
    {
        string gpsName = $"{ORE_TYPE} #{added + 1}";
        string gpsString = CreateGPS(gpsName, position);
        
        // Copy to clipboard-like functionality via CustomData
        Me.CustomData += gpsString + "\n";
        added++;
    }
    
    Echo($"Added {added} GPS coordinates to Custom Data");
    Echo("Copy them from the programmable block's Custom Data field");
}

string CreateGPS(string name, Vector3D position)
{
    return $"GPS:{name}:{position.X:F2}:{position.Y:F2}:{position.Z:F2}:";
}

void ReforgedDetectN(BoundingSphereD area, string minedOre, int count, Action<List<Vector3D>> callBack)
{
    Me.SetValue("ReforgedDetectN", new ValueTuple<BoundingSphereD, string, int, Action<List<Vector3D>>>(area, minedOre, count, callBack));
}
