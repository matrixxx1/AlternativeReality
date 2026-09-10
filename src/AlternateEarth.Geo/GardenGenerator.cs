using AlternateEarth.Shared;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace AlternateEarth.Geo;

public static class GardenGenerator
{
    public static bool IsHouse(CanonicalEntity e) => e.Kind==EntityKind.Building && e.Geometry.Count>=3 && !e.Properties.ContainsKey("merchantCategory") && !e.Properties.ContainsKey("shop") && !e.Properties.ContainsKey("amenity") && !e.Properties.ContainsKey("office") && e.Properties.GetValueOrDefault("building:use") is not ("commercial" or "industrial" or "retail" or "office") && (e.Properties.GetValueOrDefault("building") is "house" or "detached" or "semidetached_house" or "terrace" or "bungalow" or "cabin" or "residential" || e.Properties.GetValueOrDefault("building")=="yes" && (e.Geometry.Max(p=>p.X)-e.Geometry.Min(p=>p.X))*(e.Geometry.Max(p=>p.Y)-e.Geometry.Min(p=>p.Y))<=400);
    // Conservative full-footprint clearance, including the sign. Bounding boxes deliberately
    // reject uncertain corners rather than allowing tiny overlaps between sampling points.
    public static bool Clear(WorldPosition p, WorldBounds bounds, IEnumerable<CanonicalEntity> features, double width=4, double height=3)
    {
        var halfWidth=width/2+.1;var halfHeight=height/2+.1;
        if (!double.IsFinite(p.X) || !double.IsFinite(p.Y) || !bounds.Contains(p.X-halfWidth,p.Y-halfHeight) || !bounds.Contains(p.X+halfWidth,p.Y+halfHeight)) return false;
        foreach(var e in features)
        {
            if(e.Position.Region != p.Region || e.Kind is EntityKind.PropertyBoundary or EntityKind.StateBoundary or EntityKind.Airport) continue;
            if(e.Kind == EntityKind.Terrain && string.Equals(e.Properties.GetValueOrDefault("terrain"),"grass",StringComparison.OrdinalIgnoreCase)) continue;
            var points=e.Geometry.Count>0?e.Geometry:[new GeometryPoint(e.Position.X,e.Position.Y)];
            var pad=e.Kind switch {EntityKind.Road=>Number(e,"widthMeters",5)/2,EntityKind.Sidewalk=>Number(e,"widthMeters",3)/2,EntityKind.Water=>3+(WaterGeometry.IsPolygon(e)?0:WaterGeometry.Width(e)/2),EntityKind.Tree=>1.5,EntityKind.Bush=>1,EntityKind.Vehicle=>2.5,_=>.3};
            if(points.Min(v=>v.X)-pad <= p.X+halfWidth && points.Max(v=>v.X)+pad >= p.X-halfWidth && points.Min(v=>v.Y)-pad <= p.Y+halfHeight && points.Max(v=>v.Y)+pad >= p.Y-halfHeight) return false;
        }
        return true;
    }
    private static bool Owned(CanonicalEntity h,WorldBounds b)=>h.Position.X>=b.MinimumX&&h.Position.X<b.MaximumX&&h.Position.Y>=b.MinimumY&&h.Position.Y<b.MaximumY;
    private static double Number(CanonicalEntity e,string key,double fallback) => double.TryParse(e.Properties.GetValueOrDefault(key),NumberStyles.Float,CultureInfo.InvariantCulture,out var value)?Math.Max(0,value):fallback;
    public static IReadOnlyList<CanonicalEntity> Generate(IReadOnlyList<CanonicalEntity> features, WorldBounds bounds, int seed)
    {
        var houses=features.Where(IsHouse).DistinctBy(e=>e.Id).Where(h=>Owned(h,bounds)).OrderBy(e=>e.Id,StringComparer.Ordinal).ToArray();
        var random=new Random(seed); var ordered=houses.OrderBy(_=>random.Next()).ToArray();
        var result=new List<CanonicalEntity>();var farmHouses=new HashSet<string>();
        var doors=features.Where(e=>e.Kind==EntityKind.Door).GroupBy(e=>e.Properties.GetValueOrDefault("buildingId")??"").ToDictionary(g=>g.Key,g=>g.OrderBy(e=>e.Id,StringComparer.Ordinal).First());
        var maxFarms=(int)Math.Floor(houses.Length*.01);
        foreach(var house in ordered)
        {
            if(farmHouses.Count>=maxFarms) break;
            // Only certify isolation where the entire 1/3-mile circle was imported.
            var p=house.Position;var radius=GardenRules.FarmIsolation;
            if(!bounds.Contains(p.X-radius,p.Y-radius)||!bounds.Contains(p.X+radius,p.Y+radius)||features.Any(h=>IsHouse(h)&&h.Id!=house.Id&&DistanceToHouse(p,h)<=radius))continue;
            var plots=new List<CanonicalEntity>();var crops=GardenRules.Crops.OrderBy(_=>random.Next()).Take(10).ToArray();
            for(var ring=10;ring<=38&&plots.Count<10;ring+=6)for(var i=0;i<24&&plots.Count<10;i++)
            {
                var angle=i*Math.PI/12;var spot=p with {X=p.X+Math.Cos(angle)*ring,Y=p.Y+Math.Sin(angle)*ring};
                if(Clear(spot,bounds,features.Concat(result).Concat(plots)))plots.Add(GardenRules.Create($"garden:farm:{house.Id}:{plots.Count}",spot,crops[plots.Count],house.Id,true));
            }
            if(plots.Count==10){result.AddRange(plots);farmHouses.Add(house.Id);}
        }
        var maxGardens=(int)Math.Floor(houses.Length*.05);var count=0;
        foreach(var house in ordered)
        {
            if(count>=maxGardens)break;
            if(farmHouses.Contains(house.Id)||!doors.TryGetValue(house.Id,out var door)||house.Geometry.Count<3)continue;
            // The door's wall sets the front. Its inward normal points toward the opposite wall.
            var edges=Enumerable.Range(0,house.Geometry.Count-1).Select(i=>(A:house.Geometry[i],B:house.Geometry[i+1]));
            var wall=edges.MinBy(edge=>DistanceToSegment(door.Position,edge.A,edge.B));
            var dx=wall.B.Y-wall.A.Y;var dy=wall.A.X-wall.B.X;var length=Math.Sqrt(dx*dx+dy*dy);if(length<.01)continue;dx/=length;dy/=length;
            if(dx*(house.Position.X-door.Position.X)+dy*(house.Position.Y-door.Position.Y)<0){dx=-dx;dy=-dy;}
            var rear=house.Geometry.Max(v=>(v.X-door.Position.X)*dx+(v.Y-door.Position.Y)*dy);
            var placed=false;
            for(var back=4d;back<=16&&!placed;back+=3)for(var side=-8;side<=8&&!placed;side+=4)
            {
                var spot=house.Position with {X=door.Position.X+dx*(rear+back)-dy*side,Y=door.Position.Y+dy*(rear+back)+dx*side};
                if(!Clear(spot,bounds,features.Concat(result)))continue;
                result.Add(GardenRules.Create($"garden:yard:{house.Id}",spot,GardenRules.Crops[random.Next(GardenRules.Crops.Length)],house.Id));count++;placed=true;
            }
        }
        return result;
    }
    private static double DistanceToHouse(WorldPosition p,CanonicalEntity h){var minX=h.Geometry.Min(v=>v.X);var maxX=h.Geometry.Max(v=>v.X);var minY=h.Geometry.Min(v=>v.Y);var maxY=h.Geometry.Max(v=>v.Y);return Math.Sqrt(Math.Pow(Math.Max(0,Math.Max(minX-p.X,p.X-maxX)),2)+Math.Pow(Math.Max(0,Math.Max(minY-p.Y,p.Y-maxY)),2));}
    public static IReadOnlyList<CanonicalEntity> GenerateHoses(IReadOnlyList<CanonicalEntity> features,WorldBounds bounds,int seed)
    {
        var random=new Random(seed^73129);var houses=features.Where(IsHouse).DistinctBy(e=>e.Id).Where(h=>Owned(h,bounds)).OrderBy(e=>e.Id,StringComparer.Ordinal).ToArray();var result=new List<CanonicalEntity>();var max=(int)Math.Floor(houses.Length*.05);
        var doors=features.Where(e=>e.Kind==EntityKind.Door).GroupBy(e=>e.Properties.GetValueOrDefault("buildingId")??"").ToDictionary(g=>g.Key,g=>g.First());
        foreach(var h in houses.OrderBy(_=>random.Next()))
        {
            if(result.Count>=max)break;if(!doors.TryGetValue(h.Id,out var door))continue;
            var angle=Number(door,"facingDegrees",0)*Math.PI/180;var dx=Math.Cos(angle);var dy=Math.Sin(angle);var placed=false;
            for(var side=2d;side<=6&&!placed;side+=1)foreach(var sign in new[]{-1,1})
            {
                var p=h.Position with {X=door.Position.X+dx*1.5-dy*side*sign,Y=door.Position.Y+dy*1.5+dx*side*sign};
                if(!Clear(p,bounds,features.Concat(result),1,1.2))continue;
                result.Add(new("hose:"+h.Id,EntityKind.ResourceNode,p,[new(p.X-.5,p.Y-.6),new(p.X+.5,p.Y-.6),new(p.X+.5,p.Y+.6),new(p.X-.5,p.Y+.6)],new Dictionary<string,string>{["subtype"]="gardenHose",["buildingId"]=h.Id,["doorId"]=door.Id,["displayName"]="Garden hose",["facingDegrees"]=door.Properties.GetValueOrDefault("facingDegrees")??"0"}));placed=true;break;
            }
        }
        return result;
    }
    private static double DistanceToSegment(WorldPosition p,GeometryPoint a,GeometryPoint b){var dx=b.X-a.X;var dy=b.Y-a.Y;var t=Math.Clamp(((p.X-a.X)*dx+(p.Y-a.Y)*dy)/(dx*dx+dy*dy+.000001),0,1);return Math.Sqrt(Math.Pow(p.X-a.X-t*dx,2)+Math.Pow(p.Y-a.Y-t*dy,2));}
}
