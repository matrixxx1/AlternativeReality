using System.Globalization;
using AlternateEarth.Shared;

namespace AlternateEarth.Geo;

public static class DrivewayGenerator
{
    public const double WidthMeters = 3;
    public const double MaximumLengthMeters = 40;

    public static IReadOnlyList<CanonicalEntity> Generate(IReadOnlyList<CanonicalEntity> features, WorldBounds? ownedArea = null)
    {
        var result = new List<CanonicalEntity>();
        var roads = new FeatureIndex(features.Where(IsAccessibleRoad));
        var obstacles = new FeatureIndex(features.Where(e => e.Kind is EntityKind.Building or EntityKind.Water or EntityKind.Fence or
            EntityKind.Tree or EntityKind.Bush or EntityKind.Vehicle or EntityKind.StreetLight || IsDriveway(e)));
        var doors = features.Where(e => e.Kind == EntityKind.Door && e.Properties.ContainsKey("buildingId"))
            .GroupBy(e => e.Properties["buildingId"]).ToDictionary(g => g.Key, g => g.OrderBy(e => e.Id, StringComparer.Ordinal).ToArray());
        var alreadyPaved = features.Where(IsDriveway).Select(e => e.Properties.GetValueOrDefault("buildingId")).ToHashSet();
        foreach (var house in features.Where(IsHouse).DistinctBy(e => e.Id).OrderBy(e => e.Id, StringComparer.Ordinal))
        {
            // A house straddling two imports belongs to exactly one cell; do not generate it again in its neighbor.
            if (ownedArea is not null && (house.Position.X < ownedArea.MinimumX || house.Position.X >= ownedArea.MaximumX ||
                house.Position.Y < ownedArea.MinimumY || house.Position.Y >= ownedArea.MaximumY)) continue;
            if (alreadyPaved.Contains(house.Id) || !doors.TryGetValue(house.Id, out var entrances)) continue;
            foreach (var door in entrances)
            {
                var start = new GeometryPoint(door.Position.X, door.Position.Y);
                var nearbyRoads = roads.Near(Box.Around(start, MaximumLengthMeters + 25)).Where(r => r.Position.Region == house.Position.Region).ToArray();
                if (nearbyRoads.Any(r => r.Properties.GetValueOrDefault("service") == "driveway" && EdgeDistance(start, r.Geometry, false) <= 4)) break;
                var candidates = new List<(CanonicalEntity Road, GeometryPoint[] Polygon, double Length)>();
                foreach (var road in nearbyRoads)
                for (var segment = 1; segment < road.Geometry.Count; segment++)
                {
                    var a = road.Geometry[segment - 1]; var b = road.Geometry[segment];
                    var length = Distance(a, b); if (length < WidthMeters) continue;
                    var nearest = Closest(start, a, b);
                    if (Distance(nearest, a) < WidthMeters / 2 || Distance(nearest, b) < WidthMeters / 2) continue;
                    var centerDistance = Distance(start, nearest);
                    var halfRoad = Math.Clamp(Number(road, "widthMeters", 5), 1, 20) / 2;
                    var gap = centerDistance - halfRoad;
                    if (gap < 1 || gap > MaximumLengthMeters) continue;
                    var dx = (nearest.X - start.X) / centerDistance; var dy = (nearest.Y - start.Y) / centerDistance;
                    var end = new GeometryPoint(nearest.X - dx * (halfRoad - .15), nearest.Y - dy * (halfRoad - .15));
                    var wall = WallAtDoor(house.Geometry, start); if (wall is null) continue;
                    var wallDx = wall.Value.B.X - wall.Value.A.X; var wallDy = wall.Value.B.Y - wall.Value.A.Y;
                    var wallLength = Distance(wall.Value.A, wall.Value.B); wallDx /= wallLength; wallDy /= wallLength;
                    if (Distance(start, wall.Value.A) < WidthMeters / 2 || Distance(start, wall.Value.B) < WidthMeters / 2) continue;
                    if (wallDx * -dy + wallDy * dx < 0) { wallDx = -wallDx; wallDy = -wallDy; }
                    var roadDx = (b.X - a.X) / length; var roadDy = (b.Y - a.Y) / length;
                    if (roadDx * -dy + roadDy * dx < 0) { roadDx = -roadDx; roadDy = -roadDy; }
                    GeometryPoint Offset(GeometryPoint p, double x, double y, double sign) => new(p.X + x * WidthMeters / 2 * sign, p.Y + y * WidthMeters / 2 * sign);
                    var polygon = new[] { Offset(start,wallDx,wallDy,1), Offset(start,wallDx,wallDy,-1),
                        Offset(end,roadDx,roadDy,-1), Offset(end,roadDx,roadDy,1), Offset(start,wallDx,wallDy,1) };
                    // Reject narrow wedges and routes that cut through the house, including concave footprints.
                    if (Area(polygon) < gap * WidthMeters * .7 || PolygonsOverlap(polygon, house.Geometry, allowBoundaryTouch:true)) continue;
                    candidates.Add((road, polygon, gap));
                }
                var placed = false;
                foreach (var candidate in candidates.OrderBy(c => c.Length).ThenBy(c => c.Road.Id, StringComparer.Ordinal))
                {
                    if (obstacles.Near(Box.Of(candidate.Polygon).Expand(3)).Any(e => e.Id != house.Id && e.Position.Region == house.Position.Region && Blocks(e, candidate.Polygon))) continue;
                    var driveway = new CanonicalEntity($"generated:driveway:{house.Id}", EntityKind.Terrain,
                        house.Position with { X = candidate.Polygon.Take(4).Average(p => p.X), Y = candidate.Polygon.Take(4).Average(p => p.Y) },
                        candidate.Polygon, new Dictionary<string, string> { ["terrain"] = "pavement", ["subtype"] = "driveway",
                            ["buildingId"] = house.Id, ["doorId"] = door.Id, ["roadId"] = candidate.Road.Id, ["widthMeters"] = "3" });
                    result.Add(driveway); obstacles.Add(driveway); alreadyPaved.Add(house.Id); placed = true; break;
                }
                if (placed) break;
            }
        }
        return result;
    }

    private static bool IsDriveway(CanonicalEntity e) => e.Properties.GetValueOrDefault("subtype") == "driveway";
    private static bool IsAccessibleRoad(CanonicalEntity e) => e.Kind == EntityKind.Road && e.Geometry.Count >= 2 &&
        e.Properties.GetValueOrDefault("highway") is "residential" or "living_street" or "unclassified" or "service" or "tertiary" or "secondary" or "primary" &&
        e.Properties.GetValueOrDefault("bridge") != "yes" && e.Properties.GetValueOrDefault("tunnel") != "yes" &&
        e.Properties.GetValueOrDefault("motorroad") != "yes" && e.Properties.GetValueOrDefault("motor_vehicle") != "no" && e.Properties.GetValueOrDefault("access") != "no";
    private static bool IsHouse(CanonicalEntity e)
    {
        if (e.Kind != EntityKind.Building || e.Geometry.Count < 3 || e.Properties.ContainsKey("shop") || e.Properties.ContainsKey("amenity") ||
            e.Properties.ContainsKey("merchantCategory") || e.Properties.ContainsKey("office")) return false;
        if (e.Properties.GetValueOrDefault("building:use") is "commercial" or "industrial" or "retail" or "office") return false;
        var building = e.Properties.GetValueOrDefault("building");
        return building is "house" or "detached" or "semidetached_house" or "terrace" or "bungalow" or "cabin" or "residential" ||
            building == "yes" && Area(e.Geometry) <= 400 && Number(e,"building:levels",1) <= 3;
    }
    private static (GeometryPoint A, GeometryPoint B)? WallAtDoor(IReadOnlyList<GeometryPoint> polygon, GeometryPoint door)
    {
        foreach (var (a,b) in Edges(polygon,true)) if (Distance(a,b) >= WidthMeters && Distance(door,Closest(door,a,b)) < .2) return (a,b);
        return null;
    }
    private static bool Blocks(CanonicalEntity e, GeometryPoint[] driveway)
    {
        if (e.Kind == EntityKind.Vehicle)
        {
            var angle=Number(e,"rotationDegrees",0)*Math.PI/180;var dx=Math.Cos(angle);var dy=Math.Sin(angle);
            var l=Number(e,"lengthMeters",4.5)/2;var w=Number(e,"widthMeters",1.9)/2;
            var polygon=new[]{(-1,-1),(1,-1),(1,1),(-1,1)}.Select(p=>new GeometryPoint(e.Position.X+dx*p.Item1*l-dy*p.Item2*w,e.Position.Y+dy*p.Item1*l+dx*p.Item2*w)).ToArray();
            return PolygonsOverlap(driveway,polygon);
        }
        if (e.Kind is EntityKind.Building or EntityKind.Water || IsDriveway(e))
            if (e.Geometry.Count >= 3 && PolygonsOverlap(driveway,e.Geometry)) return true;
        if (e.Kind is EntityKind.Tree or EntityKind.Bush or EntityKind.StreetLight)
        {
            var point=new GeometryPoint(e.Position.X,e.Position.Y);
            return Inside(point,driveway)||EdgeDistance(point,driveway,true)<=Number(e,"collisionRadius",e.Kind==EntityKind.Tree?1.2:.5);
        }
        if (e.Kind is EntityKind.Fence or EntityKind.Water)
        {
            var radius=e.Kind==EntityKind.Water?Number(e,"widthMeters",3)/2:.2;
            foreach(var (a,b) in Edges(e.Geometry,false))
            {
                if(Inside(a,driveway)||Inside(b,driveway))return true;
                foreach(var (c,d) in Edges(driveway,true))
                    if(Intersects(a,b,c,d)||new[]{Distance(a,Closest(a,c,d)),Distance(b,Closest(b,c,d)),Distance(c,Closest(c,a,b)),Distance(d,Closest(d,a,b))}.Min()<=radius)return true;
            }
        }
        return false;
    }
    private static bool PolygonsOverlap(IReadOnlyList<GeometryPoint> a,IReadOnlyList<GeometryPoint> b,bool allowBoundaryTouch=false)
    {
        foreach(var p in a)if(Inside(p,b)&&(!allowBoundaryTouch||EdgeDistance(p,b,true)>1e-6))return true;
        foreach(var p in b)if(Inside(p,a)&&(!allowBoundaryTouch||EdgeDistance(p,a,true)>1e-6))return true;
        foreach(var (p,q) in Edges(a,true))foreach(var (r,s) in Edges(b,true))if(Intersects(p,q,r,s,allowBoundaryTouch))return true;
        return false;
    }
    private static IEnumerable<(GeometryPoint A,GeometryPoint B)> Edges(IReadOnlyList<GeometryPoint> points,bool closed)
    {for(var i=1;i<points.Count;i++)yield return(points[i-1],points[i]);if(closed&&points.Count>2&&points[^1]!=points[0])yield return(points[^1],points[0]);}
    private static double Cross(GeometryPoint a,GeometryPoint b,GeometryPoint p)=>(b.X-a.X)*(p.Y-a.Y)-(b.Y-a.Y)*(p.X-a.X);
    private static bool Intersects(GeometryPoint a,GeometryPoint b,GeometryPoint c,GeometryPoint d,bool strict=false)
    {
        var x=Cross(a,b,c)*Cross(a,b,d);var y=Cross(c,d,a)*Cross(c,d,b);
        if(strict)return x< -1e-10&&y< -1e-10;
        return x<=1e-10&&y<=1e-10&&Math.Max(a.X,b.X)>=Math.Min(c.X,d.X)-1e-6&&Math.Max(c.X,d.X)>=Math.Min(a.X,b.X)-1e-6&&
            Math.Max(a.Y,b.Y)>=Math.Min(c.Y,d.Y)-1e-6&&Math.Max(c.Y,d.Y)>=Math.Min(a.Y,b.Y)-1e-6;
    }
    private static bool Inside(GeometryPoint p,IReadOnlyList<GeometryPoint> polygon)
    {var inside=false;foreach(var(a,b)in Edges(polygon,true))if((a.Y>p.Y)!=(b.Y>p.Y)&&p.X<(b.X-a.X)*(p.Y-a.Y)/(b.Y-a.Y)+a.X)inside=!inside;return inside;}
    private static double Area(IReadOnlyList<GeometryPoint> polygon)=>Math.Abs(Edges(polygon,true).Sum(e=>e.A.X*e.B.Y-e.B.X*e.A.Y))*.5;
    private static GeometryPoint Closest(GeometryPoint p,GeometryPoint a,GeometryPoint b)
    {var dx=b.X-a.X;var dy=b.Y-a.Y;var square=dx*dx+dy*dy;var t=square<1e-12?0:Math.Clamp(((p.X-a.X)*dx+(p.Y-a.Y)*dy)/square,0,1);return new(a.X+dx*t,a.Y+dy*t);}
    private static double Distance(GeometryPoint a,GeometryPoint b)=>Math.Sqrt((a.X-b.X)*(a.X-b.X)+(a.Y-b.Y)*(a.Y-b.Y));
    private static double EdgeDistance(GeometryPoint p,IReadOnlyList<GeometryPoint> points,bool closed)=>Edges(points,closed).Select(e=>Distance(p,Closest(p,e.A,e.B))).DefaultIfEmpty(double.MaxValue).Min();
    private static double Number(CanonicalEntity e,string key,double fallback)=>double.TryParse(e.Properties.GetValueOrDefault(key),NumberStyles.Float,CultureInfo.InvariantCulture,out var value)&&double.IsFinite(value)?value:fallback;

    private readonly record struct Box(double MinX,double MinY,double MaxX,double MaxY)
    {
        public static Box Around(GeometryPoint p,double r)=>new(p.X-r,p.Y-r,p.X+r,p.Y+r);
        public static Box Of(IReadOnlyList<GeometryPoint> p)=>new(p.Min(v=>v.X),p.Min(v=>v.Y),p.Max(v=>v.X),p.Max(v=>v.Y));
        public Box Expand(double r)=>new(MinX-r,MinY-r,MaxX+r,MaxY+r);
        public bool Overlaps(Box b)=>MinX<=b.MaxX&&MaxX>=b.MinX&&MinY<=b.MaxY&&MaxY>=b.MinY;
    }
    private sealed class FeatureIndex
    {
        private readonly Dictionary<(int,int),List<(CanonicalEntity Feature,Box Bounds)>> _cells=new();
        private readonly List<(CanonicalEntity Feature,Box Bounds)> _large=new();
        public FeatureIndex(IEnumerable<CanonicalEntity> features){foreach(var e in features)Add(e);}
        private static int Cell(double value)=>(int)Math.Floor(value/64);
        public void Add(CanonicalEntity e)
        {
            var box=(e.Geometry.Count>0?Box.Of(e.Geometry):Box.Around(new(e.Position.X,e.Position.Y),4)).Expand(3);
            var minX=Cell(box.MinX);var minY=Cell(box.MinY);var maxX=Cell(box.MaxX);var maxY=Cell(box.MaxY);
            if((long)(maxX-minX+1)*(maxY-minY+1)>4096){_large.Add((e,box));return;}
            for(var x=minX;x<=maxX;x++)for(var y=minY;y<=maxY;y++){if(!_cells.TryGetValue((x,y),out var bucket))_cells[(x,y)]=bucket=[];bucket.Add((e,box));}
        }
        public IEnumerable<CanonicalEntity> Near(Box box)
        {
            var seen=new HashSet<string>();
            for(var x=Cell(box.MinX);x<=Cell(box.MaxX);x++)for(var y=Cell(box.MinY);y<=Cell(box.MaxY);y++)
                foreach(var entry in _cells.GetValueOrDefault((x,y),[]))if(entry.Bounds.Overlaps(box)&&seen.Add(entry.Feature.Id))yield return entry.Feature;
            foreach(var entry in _large)if(entry.Bounds.Overlaps(box)&&seen.Add(entry.Feature.Id))yield return entry.Feature;
        }
    }
}
