using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Campaigns
{
    public interface IExpressionParser
    {
        Expression<Func<T, bool>> ParseExpression<T>(string expressionString);
    }

    public class CarExpressionParser : IExpressionParser
    {
        public Expression<Func<M, bool>> ParseExpression<M>(string expressionString)
        {
            if (string.IsNullOrWhiteSpace(expressionString))
            {
                throw new ExpressionParsingException("Expression string cannot be empty", null);
            }

            return DynamicExpressionParser.ParseLambda<M, bool>(
                new ParsingConfig { ResolveTypesBySimpleName = true },
                false,
                expressionString);
        }
    }
}
