using AlternateEarth.Server;
using AlternateEarth.Shared;
using System.Collections.Concurrent;

namespace AlternateEarth.Tests;

public sealed partial class RealityWorldTests
{
    [Fact]
    public void HungerRisesSlowlyThenExhaustsStaminaBeforeDamagingHealth()
    {
        var now=DateTimeOffset.UtcNow;var p=new PlayerState("h","Hungry",new(default,0,0));
        Assert.Equal(.1,RealityWorld.StepHunger(p,2,now).Survival!.Hunger,6);
        p=p with{Stamina=1,Survival=new(100)};
        var hungry=RealityWorld.StepHunger(p,2,now);Assert.Equal(0,hungry.Stamina);Assert.Equal(9.85,hungry.HealthHearts,6);
        Assert.Equal(0,RealityWorld.StepHunger(hungry with{GodMode=true},2,now).Survival!.Hunger);
        var carrot=RealityWorld.EatNutrition(p,NutritionCatalog.Foods["carrot"],now,1,0);
        Assert.Equal(93,carrot.Survival!.Hunger);
        Assert.Equal(0,RealityWorld.EatNutrition(p,NutritionCatalog.Foods["cheeseburger"],now,1,0).Survival!.Hunger);
    }

    [Fact]
    public void RawMeatCanCauseShortOrLongParasitesCookedMealsCannotAndWaterReducesIllnessDamage()
    {
        var now=DateTimeOffset.UtcNow;var p=new PlayerState("h","Hungry",new(default,0,0),Stamina:0,HealthHearts:5);
        var raw=NutritionCatalog.Foods["rawChicken"];
        var shortIllness=RealityWorld.EatNutrition(p,raw,now,0,0);
        Assert.Equal(now.AddMinutes(1),Assert.Single(shortIllness.Survival!.Illnesses!).EndsAtUtc);
        var longIllness=RealityWorld.EatNutrition(p,raw,now,.24,1);
        Assert.Equal(now.AddHours(3),Assert.Single(longIllness.Survival!.Illnesses!).EndsAtUtc);
        Assert.Empty(RealityWorld.EatNutrition(p,raw,now,.25,1).Survival!.Illnesses!);
        Assert.Empty(RealityWorld.EatNutrition(p,NutritionCatalog.Foods["roastChicken"],now,0,1).Survival!.Illnesses!);
        var sick=RealityWorld.StepHunger(longIllness,2,now);
        Assert.True(sick.Survival!.Hunger>.1);Assert.True(sick.HealthHearts<longIllness.HealthHearts);
        Assert.True(RealityWorld.StepHunger(longIllness with{Water=0},2,now).HealthHearts<sick.HealthHearts);
        Assert.Empty(RealityWorld.StepHunger(shortIllness,2,now.AddMinutes(1)).Survival!.Illnesses!);
    }

    [Fact]
    public void EveryFoodRecipeRetainsIngredientsAndAddsAnExpiringNotSpecialBonus()
    {
        foreach(var recipe in NutritionCatalog.Recipes.Where(r=>r.Id!="antibiotics"))
        {
            var food=NutritionCatalog.Foods[recipe.OutputItemType];Assert.False(food.Raw);Assert.NotEmpty(food.Bonuses!);
            Assert.True(food.Health>=recipe.Ingredients.Sum(i=>NutritionCatalog.Foods.GetValueOrDefault(i.ItemType)?.Health*i.Quantity??0));
            Assert.True(food.Stamina>=recipe.Ingredients.Sum(i=>NutritionCatalog.Foods.GetValueOrDefault(i.ItemType)?.Stamina*i.Quantity??0));
            Assert.True(food.Water>=recipe.Ingredients.Sum(i=>NutritionCatalog.Foods.GetValueOrDefault(i.ItemType)?.Water*i.Quantity??0));
        }
        var now=DateTimeOffset.UtcNow;var p=new PlayerState("h","Hungry",new(default,0,0));
        var fed=RealityWorld.EatNutrition(p,NutritionCatalog.Foods["cheeseburger"],now,0,0);
        Assert.NotEmpty(fed.Survival!.Buffs!);
        Assert.Empty(RealityWorld.StepHunger(fed,1,now.AddMinutes(5)).Survival!.Buffs!);
        var foods=NutritionCatalog.Produce.Select(p=>NutritionCatalog.Foods[p]).ToArray();
        Assert.Equal(foods.Length,foods.Distinct().Count());
    }

    [Fact]
    public async Task HungerIllnessAndBonusesPersistAndAntibioticsCureAllIllnesses()
    {
        var (world,p,clock)=await ScubaFixture();
        var inventory=PhotoField<ConcurrentDictionary<string,Dictionary<string,int>>>(world,"_inventories")[p.Id];
        inventory["antibiotics"]=1;inventory["rawPig"]=1;inventory["cheeseburger"]=1;world.ProgressionRoll=()=>0;
        ScubaPlayer(world,p with{Survival=new(81,[new("Fever",clock.GetUtcNow().AddHours(2))])});
        var infected=await world.ConsumeItemAsync(p.Id,"rawPig");Assert.Equal(2,infected.Survival!.Illnesses!.Count);
        var joined=await world.JoinAsync(p.Id,p.Name);Assert.Equal(infected.Survival.Hunger,joined.Survival!.Hunger);Assert.Equal(2,joined.Survival.Illnesses!.Count);
        var fed=await world.ConsumeItemAsync(p.Id,"cheeseburger");Assert.Equal(0,fed.Survival!.Hunger);Assert.NotEmpty(fed.Survival.Buffs!);
        var cured=await world.ConsumeItemAsync(p.Id,"antibiotics");Assert.Empty(cured.Survival!.Illnesses!);Assert.NotEmpty(cured.Survival.Buffs!);
        joined=await world.JoinAsync(p.Id,p.Name);Assert.Empty(joined.Survival!.Illnesses!);Assert.NotEmpty(joined.Survival.Buffs!);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>world.ConsumeItemAsync(p.Id,"antibiotics"));
    }

    [Fact]
    public async Task WildFoodsLivestockAndShoreResourcesRespectHabitatsAndGatherOnlyOnce()
    {
        var (world,p,_)=await ScubaFixture();var snapshot=world.CreateSnapshot();
        var crops=snapshot.BaseEntities.Where(e=>e.Properties.GetValueOrDefault("subtype") is "wildCrop" or "fruitTree").ToArray();Assert.NotEmpty(crops);
        foreach(var crop in crops)
        {
            var terrain=PhotoField<WorldNavigation>(world,"_navigation").TerrainAt(crop.Position.X,crop.Position.Y);
            if(crop.Properties["itemType"]=="cranberry")Assert.Equal(TerrainType.ShallowWater,terrain);
            else
            {
                Assert.Contains(terrain,new[]{TerrainType.Grass,TerrainType.Forest});
                Assert.DoesNotContain(snapshot.BaseEntities,b=>b.Kind==EntityKind.Building&&b.Position.Distance2D(crop.Position)<30);
            }
        }
        foreach(var animal in NutritionCatalog.Livestock)Assert.Contains(snapshot.Actors!,a=>a.Subtype==animal);
        var cropToGather=crops[0];ScubaPlayer(world,p with{GodMode=true,Position=cropToGather.Position});
        var before=world.GetPrivateState(p.Id).Inventory.Items.Where(i=>i.ItemType==cropToGather.Properties["itemType"]).Sum(i=>i.Quantity);
        await world.GatherWildAsync(p.Id,cropToGather.Id);
        Assert.Equal(before+1,world.GetPrivateState(p.Id).Inventory.Items.Single(i=>i.ItemType==cropToGather.Properties["itemType"]).Quantity);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>world.GatherWildAsync(p.Id,cropToGather.Id));
    }

    [Fact]
    public async Task SandSearchReportsBothOutcomesAndOnlySuccessRevealsLoot()
    {
        var (world,p,_)=await ScubaFixture();var lumps=world.CreateSnapshot().BaseEntities.Where(e=>e.Properties.GetValueOrDefault("subtype")=="sandLump").ToArray();Assert.True(lumps.Length>=2);
        Assert.All(lumps,l=>Assert.Equal(TerrainType.Sand,PhotoField<WorldNavigation>(world,"_navigation").TerrainAt(l.Position.X,l.Position.Y)));
        var before=world.GetPrivateState(p.Id).Chests!.Count;
        ScubaPlayer(world,p with{Position=lumps[0].Position});world.ProgressionRoll=()=>1;
        Assert.Contains("unsuccessful",(await world.GatherWildAsync(p.Id,lumps[0].Id)).Message);Assert.Equal(before,world.GetPrivateState(p.Id).Chests!.Count);
        ScubaPlayer(world,p with{GodMode=true,Position=lumps[1].Position});world.ProgressionRoll=()=>0;
        Assert.Contains("successful",(await world.GatherWildAsync(p.Id,lumps[1].Id)).Message);Assert.Equal(before+1,world.GetPrivateState(p.Id).Chests!.Count);
        var opened=await world.OpenChestAsync(p.Id,lumps[1].Id+":chest");Assert.True(opened.Player.WalletCents>0);Assert.Contains(opened.Contents.Items,i=>i.ItemType is "ring" or "soiledUnderwear");
        Assert.Equal(0,(await world.OpenChestAsync(p.Id,lumps[1].Id+":chest")).Contents.MoneyCents);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>world.GatherWildAsync(p.Id,lumps[1].Id));
        var baseChance=RealityWorld.SandSearchChance(new());
        Assert.True(RealityWorld.SandSearchChance(new(Luck:5))>baseChance);Assert.True(RealityWorld.SandSearchChance(new(Perception:5))>baseChance);Assert.True(RealityWorld.SandSearchChance(new(Opportunistic:5))>baseChance);
    }

    [Fact]
    public async Task LivestockAndShellfishKillsDropTheirOwnRawMeat()
    {
        var(world,p,_)=await ScubaFixture();var actors=PhotoField<ConcurrentDictionary<string,ActorState>>(world,"_actors");
        var definitions=PhotoField<ConcurrentDictionary<string,ItemConfiguration>>(world,"_itemConfigurations");definitions["sword"]=definitions["sword"] with{Accuracy=1};
        ScubaPlayer(world,p with{GodMode=true,EquippedWeapon="sword"});
        foreach(var type in NutritionCatalog.Livestock.Concat(NutritionCatalog.Shellfish))
        {
            var id="meat-test:"+type;actors[id]=new(id,EntityKind.Animal,type,type,p.Position,HealthHearts:.01);
            PhotoField<ConcurrentDictionary<(string Player,string Weapon),DateTimeOffset>>(world,"_lastPlayerAttack").Clear();
            var killed=await world.AttackAsync(p.Id,new(id,"sword"));Assert.True(killed.Event.TargetDied);
            Assert.Contains(world.GetPrivateState(p.Id).Loot!,l=>l.Items.Any(i=>i.ItemType==NutritionCatalog.Meat(type)&&i.Quantity>0));
        }
    }

    [Fact]
    public async Task PharmacyStocksMedicineFoodStoresStockProduceAndAntibioticsCraftAtTable()
    {
        var (world,store,_)=await CreateCraftingTestWorld();
        var merchant=new ActorState("pharmacy",EntityKind.Npc,"merchant","Pharmacist",new(default,0,0),IsMerchant:true,MerchantCategory:"pharmacy");
        Assert.Contains(world.BaseMerchantOffers(merchant),o=>o.ItemType=="antibiotics");
        var foodOffers=world.BaseMerchantOffers(merchant with{MerchantCategory="food"});
        Assert.All(NutritionCatalog.Produce,p=>Assert.Contains(foodOffers,o=>o.ItemType==p));
        var recipe=CraftingCatalog.Recipes.Single(r=>r.Id=="antibiotics");
        PhotoField<ConcurrentDictionary<(string Player,string Recipe),byte>>(world,"_learnedRecipes")[("crafter",recipe.Id)]=0;
        var supplies=PhotoField<ConcurrentDictionary<string,Dictionary<string,int>>>(world,"_homeItemStorage")["crafter-account"];
        foreach(var i in recipe.Ingredients)supplies[i.ItemType]=i.Quantity;
        var crafted=await world.CraftItemAsync("crafter",new("misc-table","antibiotics"));
        Assert.Contains(crafted.PrivateState.HomeItemStorage!.Items,i=>i.ItemType=="antibiotics"&&i.Quantity==1);
        Assert.DoesNotContain(crafted.PrivateState.HomeItemStorage.Items,i=>i.ItemType=="medicinalCulture"&&i.Quantity>0);
    }
}
