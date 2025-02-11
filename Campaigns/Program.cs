using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using System.Linq.Dynamic.Core;

#region Model Definitions

public class CarModel
{
    public string Make { get; set; }
    public int Year { get; set; }
    public string Type { get; set; }
    public string VIN { get; set; }
    public decimal Price { get; set; }
    public Owner Owner { get; set; }
}


public class Owner
{
    public int ID { get; set; }
    public string Name { get; set; }
    public int Age { get; set; }
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

#region Expression Parser

public interface IExpressionParser
{
    Expression<Func<T, bool>> ParseExpression<T>(string expressionString);
}

public class ExpressionParsingException : Exception
{
    public ExpressionParsingException(string message, Exception innerException)
        : base(message, innerException) { }
}

public class CarExpressionParser : IExpressionParser
{
    public Expression<Func<T, bool>> ParseExpression<T>(string expressionString)
    {
        if (string.IsNullOrWhiteSpace(expressionString))
        {
            throw new ExpressionParsingException("Expression string cannot be empty", null);
        }

        return DynamicExpressionParser.ParseLambda<T, bool>(
            new ParsingConfig { ResolveTypesBySimpleName = true },
            false,
            expressionString);
    }
}

#endregion

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
                ExpressionString = "Make == \"Toyota\" && Type == \"SUV\" && Year >= 2020 && Owner.ID == 15",
                ValidFrom = DateTime.UtcNow.AddDays(-30),
                ValidTo = DateTime.UtcNow.AddDays(30),
                IsActive = true,
            },
            new Campaign
            {
                Id = "2",
                Name = "Honda Sedan Campaign",
                ExpressionString = "Make == \"Honda\" && Type == \"Sedan\" && Price <= 30000 && Owner.ID == 15",
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

#region Criteria Matcher

public interface ICriteriaMatcher<T>
{
    Task<CriteriaMatchResult> MatchAsync(T model);
}

public class CriteriaMatcher : ICriteriaMatcher<CarModel>
{
    private readonly ICriteriaSpecification _criteriaSpecification;
    private readonly IExpressionParser _expressionParser;
    private readonly ICriteriaCache _criteriaCache;

    public CriteriaMatcher(
        ICriteriaSpecification criteriaSpecification,
        IExpressionParser expressionParser,
        ICriteriaCache criteriaCache)
    {
        _criteriaSpecification = criteriaSpecification;
        _expressionParser = expressionParser;
        _criteriaCache = criteriaCache;
    }

    public async Task<CriteriaMatchResult> MatchAsync(CarModel model)
    {
        var result = new CriteriaMatchResult { Model = model };
        var now = DateTime.UtcNow;
        var campaigns = await _criteriaCache.GetOrLoadAsync(() => _criteriaSpecification.GetCriteriaAsync());
        var activeCampaigns = campaigns.Where(c => c.IsActive && c.ValidFrom <= now && c.ValidTo >= now);

        foreach (var campaign in activeCampaigns)
        {
            var expression = _expressionParser.ParseExpression<CarModel>(campaign.ExpressionString);
            var isMatch = expression.Compile()(model);
            if (isMatch)
            {
                result.MatchedCampaigns.Add(campaign);
            }
        }

        result.IsMatch = result.MatchedCampaigns.Any();
        return result;
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

        var carModel = new CarModel
        {
            Make = "Toyota",
            Year = 2021,
            Type = "SUV",
            VIN = "123ABC",
            Price = 35000,
            Owner = new Owner() { ID = 15, Age = 25, Name = "Linux Tovard" }
        };

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
