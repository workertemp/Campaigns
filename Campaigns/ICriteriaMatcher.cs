using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Campaigns
{
    #region Criteria Matcher
    public interface ICriteriaMatcher<T>
    {
        Task<CriteriaMatchResult> MatchAsync(T model);
    }

    public class CriteriaMatcher : ICriteriaMatcher<CarModel>
    {
        private readonly ICriteriaSpecification _criteriaSpecification; //Get datas
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
}
