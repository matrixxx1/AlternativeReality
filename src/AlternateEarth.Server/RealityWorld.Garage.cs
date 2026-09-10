using AlternateEarth.Shared;

namespace AlternateEarth.Server;

public sealed partial class RealityWorld
{
    private static bool InsideGarage(HomeGarageState? garage, double x, double y) => garage is not null &&
        x >= garage.Room.X && x <= garage.Room.X + garage.Room.Width && y >= garage.Room.Y && y <= garage.Room.Y + garage.Room.Height;

    private static bool HomeFloorContains(DungeonState home, GeometryPoint point) => PointInsideFootprint(point, home.Footprint) ||
        InsideGarage(home.Garage, point.X, point.Y) || home.Garage is { } garage && PointInsideFootprint(point, garage.Passage);

    private static HomeGarageState AddGarage(double width, double height, List<DungeonWall> walls)
    {
        var exterior = walls.ToArray();
        var centerY = exterior.Where(w => Math.Abs(w.Y2-w.Y1) >= 2.8)
            .OrderByDescending(w => (w.X1+w.X2)/2).Select(w => (double?)((w.Y1+w.Y2)/2)).FirstOrDefault() ?? height/2;
        var lowY = centerY-1.2; var highY = centerY+1.2;
        double EdgeX(DungeonWall wall, double y) => wall.X1+(wall.X2-wall.X1)*(y-wall.Y1)/(wall.Y2-wall.Y1);
        double EastX(double y) => exterior.Where(w => Math.Abs(w.Y2-w.Y1)>.0001 && y>=Math.Min(w.Y1,w.Y2) && y<=Math.Max(w.Y1,w.Y2)).Max(w => EdgeX(w,y));
        var lower = new GeometryPoint(EastX(lowY),lowY); var upper = new GeometryPoint(EastX(highY),highY);
        var minimumX = Math.Min(lower.X,upper.X);
        // Remove the east-facing wall pieces across the whole opening, including
        // footprints whose edge is made from several short or diagonal segments.
        foreach (var wall in exterior)
        {
            if (Math.Abs(wall.Y2-wall.Y1)<.0001) continue;
            var low=Math.Max(lowY,Math.Min(wall.Y1,wall.Y2)); var high=Math.Min(highY,Math.Max(wall.Y1,wall.Y2));
            if (high<=low || EdgeX(wall,(low+high)/2)<EastX((low+high)/2)-.001) continue;
            var firstY=wall.Y1<wall.Y2?low:high; var lastY=wall.Y1<wall.Y2?high:low;
            var firstX=EdgeX(wall,firstY); var lastX=EdgeX(wall,lastY);
            minimumX=Math.Min(minimumX,Math.Min(firstX,lastX)); walls.Remove(wall);
            if (Math.Abs(firstY-wall.Y1)>.001) walls.Add(new(wall.X1,wall.Y1,firstX,firstY));
            if (Math.Abs(lastY-wall.Y2)>.001) walls.Add(new(lastX,lastY,wall.X2,wall.Y2));
        }
        // Keep the original corner so already placed furniture stays in place.
        var room=new DungeonRoom(width+2,Math.Max(0,centerY-6),28,18);
        var right=room.X+room.Width;var top=room.Y+room.Height;
        walls.AddRange([new(room.X,room.Y,right,room.Y),new(right,room.Y,right,top),new(right,top,room.X,top),
            new(room.X,room.Y,room.X,centerY-1.2),new(room.X,centerY+1.2,room.X,top),
            new(lower.X,lower.Y,room.X,centerY-1.2),new(upper.X,upper.Y,room.X,centerY+1.2)]);
        return new(room,[new(minimumX-.15,lower.Y),new(room.X+.15,centerY-1.2),new(room.X+.15,centerY+1.2),new(minimumX-.15,upper.Y)]);
    }

    private void AddHomeStations(string accountId, CanonicalEntity building, List<CanonicalEntity> furniture)
    {
        var home=EmptyHome($"home:{accountId}:{building.Id}",building,[]);
        foreach(var type in new[]{"kitchenSink","stove","sewingTable","garageWorkbench","weaponsBench"})
        {
            if(furniture.Any(item=>item.Properties.GetValueOrDefault("objectType")==type))continue;
            var definition=type=="kitchenSink"?FurnitureCatalog.KitchenSink:FurnitureCatalog.All.Single(item=>item.Type==type);
            var item=CreateFurnitureEntity(accountId,definition,"black","solid",true,1000+(type=="kitchenSink"?4:type=="stove"?0:type=="sewingTable"?1:type=="garageWorkbench"?2:3),building.Position.Region);
            if(TryFindOpenFurniturePosition(home,item,furniture,out var position))item=SetFurniturePlacement(item,position.X,position.Y,0,false);
            furniture.Add(item);
        }
    }
}
