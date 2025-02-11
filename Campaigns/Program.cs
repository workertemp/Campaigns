using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using System.Linq.Dynamic.Core;
using Campaigns;

#region Model Definitions

public class CarModel
{
    public string Make { get; set; }
    public int Year { get; set; }
    public string Type { get; set; }
    public string VIN { get; set; }
    public decimal Price { get; set; }
}

public class Campaign
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string ExpressionString { get; set; }
    public DateTime ValidFrom { get; set; }
    public DateTime ValidTo { get; set; }
    public bool IsActive { get; set; }
}

public class CriteriaMatchResult
{
    public bool IsMatch { get; set; }
    public List<Campaign> MatchedCampaigns { get; set; } = new List<Campaign>();
    public CarModel Model { get; set; }
    public string ErrorMessage { get; set; }
}

#endregion

public class ExpressionParsingException : Exception
{
    public ExpressionParsingException(string message, Exception innerException)
        : base(message, innerException) { }
}




#region Criteria Specification

public interface ICriteriaSpecification
{
    Task<IEnumerable<Campaign>> GetCriteriaAsync();
}

public class DatabaseCriteriaSpecification : ICriteriaSpecification
{
    public async Task<IEnumerable<Campaign>> GetCriteriaAsync()
    {
        await Task.Delay(100); // Simulating DB access
        return new List<Campaign>
        {
            new Campaign
            {
                Id = "1",
                Name = "Toyota SUV Campaign",
                ExpressionString = "Make == \"Toyota\" && Type == \"SUV\" && Year >= 2020",
                ValidFrom = DateTime.UtcNow.AddDays(-30),
                ValidTo = DateTime.UtcNow.AddDays(30),
                IsActive = true
            },
            new Campaign
            {
                Id = "2",
                Name = "Honda Sedan Campaign",
                ExpressionString = "Make == \"Honda\" && Type == \"Sedan\" && Price <= 30000",
                ValidFrom = DateTime.UtcNow.AddDays(-30),
                ValidTo = DateTime.UtcNow.AddDays(30),
                IsActive = true
            }
        };
    }
}

#endregion

#region Criteria Cache

public interface ICriteriaCache
{
    Task<IEnumerable<Campaign>> GetOrLoadAsync(Func<Task<IEnumerable<Campaign>>> loadFunc);
}

public class InMemoryCriteriaCache : ICriteriaCache
{
    private readonly Dictionary<string, IEnumerable<Campaign>> _db = new Dictionary<string, IEnumerable<Campaign>>();
    public async Task<IEnumerable<Campaign>> GetOrLoadAsync(Func<Task<IEnumerable<Campaign>>> loadFunc)
    {
        string cacheKey = "campaigns";
        if (!_db.ContainsKey(cacheKey))
        {
            _db[cacheKey] = await loadFunc();
        }
        return _db[cacheKey];
    }
}

#endregion


#region Rule Engine

public interface IRuleEngine<T>
{
    Task<CriteriaMatchResult> ExecuteAsync(T model);
}

public class RuleEngine : IRuleEngine<CarModel>
{
    private readonly ICriteriaMatcher<CarModel> _matcher;

    public RuleEngine(ICriteriaMatcher<CarModel> matcher)
    {
        _matcher = matcher;
    }

    public async Task<CriteriaMatchResult> ExecuteAsync(CarModel model)
    {
        return await _matcher.MatchAsync(model);
    }
}

#endregion

#region Main Program

class Program
{
    static async Task Main(string[] args)
    {
        var criteriaSpecification = new DatabaseCriteriaSpecification();

        var expressionParser = new CarExpressionParser();

        var criteriaCache = new InMemoryCriteriaCache();
        var matcher = new CriteriaMatcher(criteriaSpecification, expressionParser, criteriaCache);

        var ruleEngine = new RuleEngine(matcher);

        var carModel = new CarModel { Make = "Toyota", Year = 2021, Type = "SUV", VIN = "123ABC", Price = 35000 };

        var result = await ruleEngine.ExecuteAsync(carModel);

        Console.WriteLine("Match Found: " + result.IsMatch);
        Console.WriteLine("Matched Campaigns:");
        foreach (var campaign in result.MatchedCampaigns)
        {
            Console.WriteLine($"{campaign.Name} applied Campaign");
        }

        Console.ReadKey();
    }
}

#endregion
