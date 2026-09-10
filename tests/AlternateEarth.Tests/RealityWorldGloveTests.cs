using AlternateEarth.Geo;
using AlternateEarth.Server;
using AlternateEarth.Shared;

namespace AlternateEarth.Tests;

public sealed partial class RealityWorldTests
{
    [Fact]
    public async Task GlovesRequireOwnershipPersistAndUnequipWhenDropped()
    {
        var (world,store,player,_) = await CreateVehicleStorageWorld();
        await Assert.ThrowsAsync<InvalidOperationException>(()=>world.SetEquipmentAsync(player.Id,"gloves","quickdrawGloves"));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>world.SetEquipmentAsync(player.Id,"gloves","food"));
        var inventory=world.GetPrivateState(player.Id).Inventory;
        await store.SaveInventoryAsync(inventory with { Items=[..inventory.Items,new ItemStack("quickdrawGloves",1)] });
        var restarted = new RealityWorld(world.Configuration,new DeterministicWorldGenerator(new FixedGeographicProvider(Building("garage",world.Configuration.Area.Region,20,20))),new FixedWeatherProvider(),store);
        await restarted.InitializeAsync(); await restarted.JoinAsync(player.Id,player.Name,"garage-account");
        var equipped=await restarted.SetEquipmentAsync(player.Id,"gloves","quickdrawGloves");
        Assert.Equal("quickdrawGloves",equipped.EquippedGloves);
        Assert.Equal(.8,equipped.GloveShootingIntervalMultiplier,8);
        Assert.Equal("quickdrawGloves",(await store.LoadCharacterAsync(world.Configuration.Id,player.Id))!.EquippedGloves);
        var dropped=await restarted.DropInventoryItemAsync(player.Id,new("quickdrawGloves",1));
        Assert.Equal("none",dropped.Player.EquippedGloves);
        Assert.Equal(1,dropped.Player.GloveShootingIntervalMultiplier);
        await restarted.CollectLootAsync(player.Id,dropped.Drop.Id);
        Assert.Equal("quickdrawGloves",(await restarted.SetEquipmentAsync(player.Id,"gloves","quickdrawGloves")).EquippedGloves);
    }

    [Fact]
    public async Task RemovingGodModeClearsUnownedGlovesWithoutRemovingOffhand()
    {
        var (world,store,player,_) = await CreateVehicleStorageWorld();
        await world.SetGodModeAsync(player.Id,true);
        await world.SetEquipmentAsync(player.Id,"offhand","shield");
        var equipped=await world.SetEquipmentAsync(player.Id,"gloves","hazmatGloves");
        Assert.True(equipped.ShieldOn);
        var removed=await world.SetEquipmentAsync(player.Id,"gloves",null);
        Assert.True(removed.ShieldOn); Assert.Equal("none",removed.EquippedGloves);
        await world.SetEquipmentAsync(player.Id,"gloves","hazmatGloves");
        Assert.Equal("none",(await world.SetGodModeAsync(player.Id,false)).EquippedGloves);
    }
}

public sealed class GloveBenefitsTests
{
    private static PlayerState Wearing(string gloves) => new("p","Player",new WorldPosition(new GeographicArea(new GeoCoordinate(45,-123),500).Region,0,0),EquippedGloves:gloves);

    [Theory]
    [InlineData("fireproofGloves","fire",4)]
    [InlineData("fireproofGloves","flamethrower",4)]
    [InlineData("fireproofGloves","napalm",4)]
    [InlineData("acidproofGloves","acidGas",2.5)]
    [InlineData("acidproofGloves","fire",10)]
    [InlineData("hazmatGloves","acidGas",5)]
    [InlineData("hazmatGloves","physical",10)]
    public void GlovesReduceOnlyTheirMatchingDamage(string gloves,string effect,double expected) =>
        Assert.Equal(expected,GloveCatalog.ReduceDamage(Wearing(gloves),10,effect),8);

    [Fact]
    public void RandomGlovesHaveStableDistinctInventoryIdsAndBoundedPerks()
    {
        Assert.Equal(24,GloveCatalog.All.Count);
        Assert.All(GloveCatalog.All.Values,g=>{
            Assert.InRange(g.ShootingSpeedBonus,0,.4);Assert.InRange(g.FireResistance,0,.75);Assert.InRange(g.AcidResistance,0,.75);
            Assert.False(string.IsNullOrWhiteSpace(g.Description));Assert.True(g.ShootingSpeedBonus+g.FireResistance+g.AcidResistance>0);
        });
        Assert.All(GloveCatalog.Items,i=>Assert.Equal("gloves",InventorySections.Section(i)));
        Assert.Equal(1,GloveCatalog.ShootingInterval(Wearing("none")));
    }

    [Theory]
    [InlineData("water","food")]
    [InlineData("dirtyWater","food")]
    [InlineData("apple","food")]
    [InlineData("wood","crafting")]
    [InlineData("metal","crafting")]
    [InlineData("recipe:gunpowder","crafting")]
    [InlineData("quickdrawGloves","gloves")]
    [InlineData("calculator","misc")]
    public void StorageSectionsSeparateFoodGlovesCraftingAndOther(string item,string section) =>
        Assert.Equal(section,InventorySections.Section(new ItemConfiguration(item,item,"",0,0,0,0)));
}
