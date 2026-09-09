using AlternateEarth.Shared;

namespace AlternateEarth.Server;

internal static class NutritionCatalog
{
    public static readonly string[] Produce = ["apple", "pear", "peach", "orange", "banana", "blackberry", "raspberry", "blueberry", "strawberry", "cranberry", "watermelon", "carrot", "potato", "corn", "beans", "spinach", "lettuce", "garlic", "onion", "peppers", "tomato", "pumpkin"];
    public static readonly string[] Livestock = ["chicken", "pig", "cow", "sheep", "goat"];
    public static readonly string[] Shellfish = ["crab", "clam", "oyster", "crayfish", "lobster", "shrimp"];
    public static bool Aquatic(string subtype) => subtype is "fish" or "waterMonster" || Shellfish.Contains(subtype);
    public static string? Meat(string subtype) => subtype switch
    {
        "fish" => "fish", "largeShark" => "rawShark", "largeOctopus" => "rawOctopus",
        "eventBear" => "rawBear",
        _ => Animals.Contains(subtype) ? "raw" + char.ToUpperInvariant(subtype[0]) + subtype[1..] : null
    };
    private static readonly string[] Animals = [.. Livestock, .. Shellfish, "shark", "octopus", "barracuda", "morayEel", "rabbit", "deer", "bear", "cougar", "bird", "dog", "cat"];
    public static readonly CraftingRecipe[] Recipes =
    [
        new("fishStew", "Fish stew", "fishStew", 1, [new("fish",2),new("water",1),new("salt",1)]),
        new("friedFish", "Fried fish", "friedFish", 1, [new("fish",1),new("cookingOil",1),new("flour",1)]),
        new("cheeseburger", "Cheeseburger", "cheeseburger", 1, [new("rawCow",1),new("cheese",1),new("flour",1),new("lettuce",1),new("tomato",1)]),
        new("gardenSalad", "Garden salad", "gardenSalad", 1, [new("lettuce",1),new("spinach",1),new("carrot",1),new("tomato",1)]),
        new("berryCompote", "Berry compote", "berryCompote", 1, [new("blackberry",1),new("raspberry",1),new("blueberry",1),new("sugar",1)]),
        new("roastChicken", "Roast chicken dinner", "roastChicken", 1, [new("rawChicken",1),new("potato",1),new("garlic",1),new("onion",1)]),
        new("porkAndBeans", "Pork and beans", "porkAndBeans", 1, [new("rawPig",1),new("beans",2),new("peppers",1)]),
        new("seafoodChowder", "Seafood chowder", "seafoodChowder", 1, [new("rawClam",1),new("rawCrab",1),new("milk",1),new("potato",1)]),
        new("cornBread", "Corn bread", "cornBread", 1, [new("corn",2),new("flour",1),new("egg",1)]),
        new("cranberryRelish", "Cranberry relish", "cranberryRelish", 1, [new("cranberry",2),new("orange",1),new("sugar",1)]),
        new("fruitSalad", "Fruit salad", "fruitSalad", 1, [new("watermelon",1),new("apple",1),new("banana",1)]),
        new("antibiotics", "Antibiotics", "antibiotics", 1, [new("medicinalCulture",1),new("medicalBinder",1),new("water",1),new("emptyGlassBottle",1)]),
        .. Animals.Select(a => new CraftingRecipe("cook" + a, "Cooked " + a, "cooked" + a, 1, [new(Meat(a)!,1),new("salt",1)]))
    ];
    public static readonly IReadOnlyDictionary<string, Nutrition> Foods = BuildFoods();
    private static Dictionary<string, Nutrition> BuildFoods()
    {
        var foods = new Dictionary<string,Nutrition>(StringComparer.OrdinalIgnoreCase)
        {
            ["food"] = new(70,2.12,10,10,Bonuses:new Dictionary<string,int>{{"endurance",1}}), ["fish"] = new(12,.35,1.4,Raw:true),
            ["flour"] = new(12,.05,.8), ["cookingOil"] = new(10,.02,.5), ["sugar"] = new(8,0,1.5),
            ["salt"] = new(0,0,.1), ["pepper"] = new(0,.05,.15), ["water"] = new(0,2,0,10),
            ["cheese"] = new(18,.8,1.6), ["milk"] = new(12,.6,1.2,3), ["egg"] = new(10,.25,.9,Raw:true)
        };
        for(var i=0;i<Produce.Length;i++)
        {
            var id=Produce[i];var hunger=id=="carrot"?7:id=="watermelon"?25:5+i%7*2;
            foods[id]=new(hunger,.1+i*.025,.4+i%6*.2,id=="watermelon"?5:id.EndsWith("berry")?1.2:.2+i%4*.2);
        }
        for(var i=0;i<Animals.Length;i++) foods[Meat(Animals[i])!]=new(10+i%6*2,.2+i*.02,.6+i*.07,Raw:true);
        string[] stats=["endurance","perception","strength","agility","intelligence","luck","nutUp","opportunistic","timing","charisma"];
        foreach(var (recipe,index) in Recipes.Where(r=>r.Id!="antibiotics").Select((r,i)=>(r,i)))
        {
            var benefits=recipe.Ingredients.Select(i=>(Food:foods.GetValueOrDefault(i.ItemType),i.Quantity)).Where(i=>i.Food is not null).ToArray();
            var bonuses=new Dictionary<string,int>();
            foreach(var i in benefits)foreach(var b in i.Food!.Bonuses??new Dictionary<string,int>())bonuses[b.Key]=bonuses.GetValueOrDefault(b.Key)+b.Value*i.Quantity;
            bonuses[stats[index%stats.Length]]=bonuses.GetValueOrDefault(stats[index%stats.Length])+1;
            foods[recipe.OutputItemType]=new(recipe.Id=="cheeseburger"?100:benefits.Sum(i=>i.Food!.Hunger*i.Quantity)+5,
                benefits.Sum(i=>i.Food!.Health*i.Quantity)+.5,benefits.Sum(i=>i.Food!.Stamina*i.Quantity)+1,
                benefits.Sum(i=>i.Food!.Water*i.Quantity),Bonuses:bonuses);
        }
        return foods;
    }
    public static string Description(Nutrition n) => $"Hunger −{n.Hunger:0.#}; hearts +{n.Health:0.##}; stamina +{n.Stamina:0.##}; water +{n.Water:0.#}." +
        (n.Raw?" Raw: 25% parasite risk (1 minute–3 hours).":"") +
        (n.Bonuses is null?"":" " + string.Join(", ",n.Bonuses.Select(b=>$"+{b.Value} {b.Key} for 5 minutes")));
    public static IEnumerable<ItemConfiguration> Items => Foods.Where(f=>f.Key is not ("food" or "fish" or "water" or "flour" or "cookingOil" or "sugar" or "salt" or "pepper")).Select(f=>new ItemConfiguration(f.Key,
        f.Key=="rawCow"?"Raw beef":f.Key=="rawPig"?"Raw pork":System.Text.RegularExpressions.Regex.Replace(f.Key,"([a-z])([A-Z])","$1 $2"),Description(f.Value),0,0,50,800,WeightPounds:.4,Nutrition:f.Value)).Concat([
        new("antibiotics","Antibiotics","Cures all game diseases and illnesses, including parasites",0,0,1200,2400,WeightPounds:.05),
        new("medicinalCulture","Medicinal culture","Fictional antibiotic crafting ingredient",0,0,250,500,WeightPounds:.1),
        new("medicalBinder","Medical binder","Fictional antibiotic crafting ingredient",0,0,150,350,WeightPounds:.1),
        new("ring","Buried ring","A ring recovered from sand; sell to a merchant",0,0,1500,15000,WeightPounds:.02)
    ]);
}
