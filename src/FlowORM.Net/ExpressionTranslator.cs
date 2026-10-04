using System.Linq.Expressions;

using System.Security.Cryptography;

using FlowORM.Net.Abstractions;

using FlowORM.Net.Configuration;

using FlowORM.Net.Metadata;

using FlowORM.Net.Models;

using FlowORM.Net.Query;

namespace FlowORM.Net.Services;

internal static class ExpressionTranslator
{

    public static SearchParam Translate<T>(Expression<Func<T,bool>> e,SearchParam? existing) where T:DBModel
    {
        var p=existing?.Clone()??new();

        Visit(e.Body,p);

        return p;

    }

    private static void Visit(Expression e, SearchParam p)
    {
        if (e is BinaryExpression logical &&
            logical.NodeType is ExpressionType.AndAlso or ExpressionType.OrElse)
        {
            p.Condition = logical.NodeType == ExpressionType.OrElse
                ? SearchCondition.Or
                : SearchCondition.And;
            Visit(logical.Left, p);
            Visit(logical.Right, p);
            return;
        }

        if (e is BinaryExpression comparison)
        {
            var field = TryResolveField(comparison.Left);
            var valueExpression = comparison.Right;
            var nodeType = comparison.NodeType;

            if (field is null)
            {
                field = TryResolveField(comparison.Right);
                valueExpression = comparison.Left;
                nodeType = Reverse(nodeType);
            }

            if (field is not null)
            {
                p.Filters.Add(new SearchFilter
                {
                    Field = field,
                    Operator = nodeType switch
                    {
                        ExpressionType.Equal => SearchOperator.EQ,
                        ExpressionType.NotEqual => SearchOperator.NEQ,
                        ExpressionType.GreaterThan => SearchOperator.GT,
                        ExpressionType.GreaterThanOrEqual => SearchOperator.GTE,
                        ExpressionType.LessThan => SearchOperator.LT,
                        ExpressionType.LessThanOrEqual => SearchOperator.LTE,
                        _ => throw new NotSupportedException($"Comparison '{nodeType}' is not supported.")
                    },
                    Value = Expression.Lambda(valueExpression).Compile().DynamicInvoke()
                });
                return;
            }
        }

        if (e is MethodCallExpression call && call.Object is not null && call.Arguments.Count == 1)
        {
            var field = TryResolveField(call.Object);
            if (field is not null)
            {
                p.Filters.Add(new SearchFilter
                {
                    Field = field,
                    Operator = call.Method.Name switch
                    {
                        nameof(string.Contains) => SearchOperator.Contains,
                        nameof(string.StartsWith) => SearchOperator.StartsWith,
                        nameof(string.EndsWith) => SearchOperator.EndsWith,
                        _ => throw new NotSupportedException()
                    },
                    Value = Expression.Lambda(call.Arguments[0]).Compile().DynamicInvoke()
                });
                return;
            }
        }

        throw new NotSupportedException($"Expression '{e}' is outside the supported expression subset.");
    }

    private static string? TryResolveField(Expression expression)
    {
        try { return ExpressionFieldResolver.Resolve(expression); }
        catch (NotSupportedException) { return null; }
    }

    private static ExpressionType Reverse(ExpressionType type) => type switch
    {
        ExpressionType.GreaterThan => ExpressionType.LessThan,
        ExpressionType.GreaterThanOrEqual => ExpressionType.LessThanOrEqual,
        ExpressionType.LessThan => ExpressionType.GreaterThan,
        ExpressionType.LessThanOrEqual => ExpressionType.GreaterThanOrEqual,
        _ => type
    };

}
