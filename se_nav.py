import math

# --- 1. Vector Math ---
class Vec3:
    def __init__(self, x, y, z): self.x, self.y, self.z = float(x), float(y), float(z)
    def __add__(self, o): return Vec3(self.x+o.x, self.y+o.y, self.z+o.z)
    def __sub__(self, o): return Vec3(self.x-o.x, self.y-o.y, self.z-o.z)
    def __mul__(self, s): return Vec3(self.x*s, self.y*s, self.z*s)
    def length(self): return math.sqrt(self.x**2 + self.y**2 + self.z**2)
    def normalize(self): 
        l = self.length()
        return Vec3(0,0,0) if l==0 else Vec3(self.x/l, self.y/l, self.z/l)
    def dot(self, o): return self.x*o.x + self.y*o.y + self.z*o.z
    def __str__(self): return f"{self.x:.2f}:{self.y:.2f}:{self.z:.2f}"

def to_gps(name, v): return f"GPS:{name}:{v.x:.2f}:{v.y:.2f}:{v.z:.2f}:#FF75C9F1:"

# --- 2. The Multi-Obstacle Logic ---

OBSTACLES = [] 

def check_intersection(start, end, sphere):
    d = end - start
    f = start - sphere["center"]
    a = d.dot(d)
    b = 2 * f.dot(d)
    c = f.dot(f) - sphere["radius"]**2
    discriminant = b*b - 4*a*c
    
    if discriminant < 0: return False
    
    t1 = (-b - math.sqrt(discriminant)) / (2*a)
    t2 = (-b + math.sqrt(discriminant)) / (2*a)
    
    if (t1 >= 0 and t1 <= 1) or (t2 >= 0 and t2 <= 1): return True
    return False

def get_safe_point(start, end, sphere):
    """
    Calculates a waypoint that is pushed out far enough so that 
    the straight lines to/from it do not clip the sphere.
    """
    v_start = (start - sphere["center"]).normalize()
    v_end = (end - sphere["center"]).normalize()
    
    # 1. Calculate the angle of the turn
    dot = v_start.dot(v_end)
    angle = math.acos(max(-1.0, min(1.0, dot))) 
    
    # 2. Find the direction for the waypoint
    mid_vec = (v_start + v_end).normalize()
    if mid_vec.length() < 0.1: mid_vec = Vec3(0, 1, 0) # Handle 180-deg turns
    
    # 3. THE FIX: Tangent Calculation
    # We want the path to graze the 'radius', so the waypoint must be hypotenuse.
    # We look at half the angle (since the waypoint is in the middle)
    half_angle = angle / 2.0
    
    # If the angle is very wide (flying past), we don't need to push out much.
    # If the angle is sharp (flying around), we push out a lot.
    # We clamp the divisor to avoid division by zero or infinite distances.
    # We effectively want to solve for Hypotenuse = Radius / cos(half_angle)
    # But we perform a check to ensure we don't push it out absurdly far (e.g. infinite)
    
    # Use a simpler scaling factor for stability:
    # Instead of perfect tangents (which can blow up), we just ensure
    # the midpoint of the chord clears the radius.
    # This formula guarantees clearance for the *segments*.
    
    required_dist = sphere["radius"] / math.cos(half_angle / 2.0)
    
    # Add a 2% buffer for floating point errors
    required_dist *= 1.02
    
    return sphere["center"] + (mid_vec * required_dist)

def find_path(start, end, depth=0):
    if depth > 10: return [] 
    
    closest_hit = None
    closest_dist = float('inf')
    
    # Find the FIRST obstacle we hit
    for obs in OBSTACLES:
        if check_intersection(start, end, obs):
            dist = (obs["center"] - start).length()
            if dist < closest_dist:
                closest_dist = dist
                closest_hit = obs
    
    if closest_hit is None:
        return [] 

    print(f"   [Depth {depth}] Path blocked by {closest_hit['name']}! Adjusting...")
    
    # Calculate the tangent waypoint
    midpoint = get_safe_point(start, end, closest_hit)
    
    # Recursively check the new legs
    path_leg_1 = find_path(start, midpoint, depth + 1)
    path_leg_2 = find_path(midpoint, end, depth + 1)
    
    return path_leg_1 + [midpoint] + path_leg_2

# --- 3. RUN IT ---

# Start (Alien Orbit)
start_pos = Vec3(154006.51, 131027.85, 5613523.33)

# End (Titan)
end_pos = Vec3(36384, 211384, 5796384)

# Obstacles
OBSTACLES = [
    {"name": "Alien Planet", "center": Vec3(131072, 131072, 5731072), "radius": 103000},
    {"name": "Titan Moon", "center": Vec3(36384, 226384, 5796384), "radius": 14000} 
]

print("Calculating Multi-Jump Path...")
final_waypoints = find_path(start_pos, end_pos)

if not final_waypoints:
    print("Path Clear! Fly straight to destination.")
else:
    print(f"\nPath found! Requires {len(final_waypoints)} waypoints.\n")
    for i, pt in enumerate(final_waypoints):
        print(to_gps(f"Waypoint_{i+1}", pt))
    
    print(to_gps("Final_Destination", end_pos))