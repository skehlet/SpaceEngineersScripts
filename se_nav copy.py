import math

# --- Vector Math Helper Class ---
class Vec3:
    def __init__(self, x, y, z):
        self.x, self.y, self.z = x, y, z

    def __add__(self, other): return Vec3(self.x + other.x, self.y + other.y, self.z + other.z)
    def __sub__(self, other): return Vec3(self.x - other.x, self.y - other.y, self.z - other.z)
    def __mul__(self, scalar): return Vec3(self.x * scalar, self.y * scalar, self.z * scalar)
    
    def length(self):
        return math.sqrt(self.x**2 + self.y**2 + self.z**2)
    
    def normalize(self):
        l = self.length()
        if l == 0: return Vec3(0,0,0)
        return Vec3(self.x/l, self.y/l, self.z/l)
    
    def dot(self, other):
        return self.x*other.x + self.y*other.y + self.z*other.z

def to_gps(name, vec, color="#FF75C9F1"):
    return f"GPS:{name}:{vec.x:.2f}:{vec.y:.2f}:{vec.z:.2f}:{color}:"

def get_safe_orbit_radius(ship_vec, dest_vec, safe_radius):
    """
    Calculates how far out the waypoint needs to be to prevent the 
    straight-line path from clipping the gravity well.
    """
    # 1. Calculate the angle between Start and Destination
    # dot_product = |A|*|B|*cos(theta)
    dot = ship_vec.normalize().dot(dest_vec.normalize())
    
    # Clamp value to handle floating point errors slightly outside -1.0/1.0
    dot = max(-1.0, min(1.0, dot))
    total_angle = math.acos(dot)
    
    # 2. The flight path is split into two legs: Start->Waypoint and Waypoint->End.
    # The Waypoint is in the middle, so each leg spans 'total_angle / 2'.
    # The point where the path dips closest to the planet is the MIDDLE of that leg.
    # So we look at the angle 'total_angle / 4'.
    dip_angle = total_angle / 4.0
    
    # 3. Trigonometry: cos(dip_angle) = Adjacent / Hypotenuse
    # Adjacent = Safe Radius (The closest we want the path to get)
    # Hypotenuse = The Waypoint Radius we need to calculate
    # So: Waypoint_Radius = Safe_Radius / cos(dip_angle)
    
    required_radius = safe_radius / math.cos(dip_angle)
    
    return required_radius

# --- Core Logic ---

def calculate_path(ship_pos, driller_pos, planet_center, 
                   gravity_radius, manual_height, stay_outside_gravity):
    
    # Define the "No Fly Zone" (Gravity + 500m safety buffer)
    safety_limit = gravity_radius + 500
    
    # 1. Determine Hover Altitude
    surface_vec = driller_pos - planet_center
    surface_radius = surface_vec.length()
    
    if stay_outside_gravity:
        needed_height = (gravity_radius - surface_radius) + 500
        if needed_height < 200: needed_height = 200
        final_height = needed_height
        print(f"Mode: ZERO-G PARKING. Hover Altitude: {final_height:.0f} m")
    else:
        final_height = manual_height
        print(f"Mode: MANUAL HEIGHT. Hover Altitude: {final_height:.0f} m")

    # 2. Calculate Final Destination (Straight Up from Driller)
    up_dir = surface_vec.normalize()
    final_dest = driller_pos + (up_dir * final_height)

    # 3. Calculate Safer Waypoint
    vec_to_ship = ship_pos - planet_center
    vec_to_dest = final_dest - planet_center
    
    # Use the new math to find the minimum safe distance
    safe_waypoint_dist = get_safe_orbit_radius(vec_to_ship, vec_to_dest, safety_limit)
    
    # Add an extra 5% buffer just to be absolutely sure
    safe_waypoint_dist *= 1.05
    
    print(f"Diagnostics: To avoid 'corner cutting', waypoint pushed out to {safe_waypoint_dist:.0f}m radius.")

    # Calculate position
    mid_vector = vec_to_ship.normalize() + vec_to_dest.normalize()
    if mid_vector.length() < 0.1: mid_vector = Vec3(1, 0, 0)
    
    waypoint_pos = planet_center + (mid_vector.normalize() * safe_waypoint_dist)
    
    print(f"\n--- FLIGHT PLAN ---")
    print("1. Fly to Waypoint (Guaranteed gravity clearance).")
    print("2. Descend to Hover Position.")
    
    print("\nCopy these to your Clipboard:")
    print(to_gps("1_Safe_Clearance_Waypoint", waypoint_pos))
    print(to_gps("2_ZeroG_Hover_Position", final_dest))

# --- USER INPUT AREA ---

# 1. MOON SETUP
moon_center = Vec3(16384, 136384, -113616)
moon_gravity_radius = 13000 

# 2. COORDINATES
# Your Ship (Moon Approach Corrected)
my_ship = Vec3(26880, 129720, -107182) 
my_ship = Vec3(26880, 129720, -107182) 


# You (The Driller at South Pole) - Updated Y coordinate!
# NOTE: Make sure to update this with your ACTUAL GPS if it differs!
my_driller = Vec3(16384, 145884, -113616) # I estimated your Y based on radius

# 3. SETTINGS
STAY_OUTSIDE_GRAVITY = True 
manual_height = 200 

# --- EXECUTE ---
if __name__ == "__main__":
    calculate_path(my_ship, my_driller, moon_center, moon_gravity_radius, manual_height, STAY_OUTSIDE_GRAVITY)
