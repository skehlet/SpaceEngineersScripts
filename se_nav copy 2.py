import math

# --- 1. The Math Helper (Don't touch this part) ---
class Vec3:
    def __init__(self, x, y, z):
        self.x, self.y, self.z = float(x), float(y), float(z)
    def __add__(self, o): return Vec3(self.x+o.x, self.y+o.y, self.z+o.z)
    def __sub__(self, o): return Vec3(self.x-o.x, self.y-o.y, self.z-o.z)
    def __mul__(self, s): return Vec3(self.x*s, self.y*s, self.z*s)
    def length(self): return math.sqrt(self.x**2 + self.y**2 + self.z**2)
    def normalize(self): 
        l = self.length()
        return Vec3(0,0,0) if l==0 else Vec3(self.x/l, self.y/l, self.z/l)
    def dot(self, o): return self.x*o.x + self.y*o.y + self.z*o.z

def to_gps(name, v):
    return f"GPS:{name}:{v.x:.2f}:{v.y:.2f}:{v.z:.2f}:#FF75C9F1:"

# --- 2. The Logic (Simplified) ---

def check_intersection(start, end, sphere_center, sphere_radius):
    # This checks if the line from Start->End hits the sphere
    d = end - start
    f = start - sphere_center
    a = d.dot(d)
    b = 2 * f.dot(d)
    c = f.dot(f) - sphere_radius**2
    discriminant = b*b - 4*a*c
    
    if discriminant < 0: return False # Missed the sphere completely
    
    # Check if the hit is actually between start and end (not behind us)
    t1 = (-b - math.sqrt(discriminant)) / (2*a)
    t2 = (-b + math.sqrt(discriminant)) / (2*a)
    
    if (t1 >= 0 and t1 <= 1) or (t2 >= 0 and t2 <= 1):
        return True # Hit!
    return False

def get_detour_waypoint(start, end, sphere_center, sphere_radius):
    # 1. Find the "Bisector" (The direction halfway between start and end)
    v_start = (start - sphere_center).normalize()
    v_end = (end - sphere_center).normalize()
    mid_vec = (v_start + v_end).normalize()
    
    # 2. Calculate how far out we need to be to avoid "cutting the corner"
    # (Uses the Chord math we discussed last time, but simplified)
    dot = v_start.dot(v_end)
    angle = math.acos(max(-1.0, min(1.0, dot))) # Clamp to prevent errors
    
    # If angle is near 180 (flying straight through center), pick a side
    if mid_vec.length() < 0.1: mid_vec = Vec3(0, 1, 0)
    
    # Trigonometry to push the point out so the path clears the radius
    # We add 5% extra distance just to be safe
    safe_dist = (sphere_radius / math.cos(angle / 4.0)) * 1.05
    
    return sphere_center + (mid_vec * safe_dist)

# --- 3. YOUR SETTINGS ---

# A. WHERE ARE YOU?
# GPS:Alien orbit:154006.51:131027.85:5613523.33:
start_pos = Vec3(154006.51, 131027.85, 5613523.33)

# B. WHERE ARE YOU GOING?
# GPS:Titan north pole 0g park:36384:211384:5796384:
end_pos = Vec3(36384, 211384, 5796384)

# C. WHAT IS IN THE WAY?
# List your planets here: [ Center_X, Center_Y, Center_Z, Danger_Radius ]
obstacles = [
    # Alien Planet (Approx Radius 60km + Gravity 42km + buffer 1km) -> Set Radius to 103000
    {"name": "Alien Planet", "center": Vec3(131072, 131072, 5731072), "radius": 103000},
    
    # Titan Moon (Approx Radius 9500km + Gravity 3.5km + buffer 1km) -> Set Radius to 14000
    # Note: Titan is usually at 36384, 226384, 5796384
    {"name": "Titan Moon", "center": Vec3(36384, 226384, 5796384), "radius": 14000} 
]

# --- 4. EXECUTE ---
print(f"Checking path from Start to End...")
hit_detected = False

for planet in obstacles:
    if check_intersection(start_pos, end_pos, planet["center"], planet["radius"]):
        print(f"\n[ALERT] Path intersects with {planet['name']}!")
        
        # Calculate the safe spot
        waypoint = get_detour_waypoint(start_pos, end_pos, planet["center"], planet["radius"])
        
        print("Use this waypoint to fly around it:")
        print(to_gps(f"Avoid_{planet['name']}", waypoint))
        hit_detected = True
        break # We stop after the first collision to keep it simple

if not hit_detected:
    print("\n[CLEAR] No obstacles detected. You can fly straight there!")
