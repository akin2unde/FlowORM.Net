using System.Linq.Expressions;

namespace FlowORM.Net.Services;

/// <summary>Resolves CLR member/dictionary expressions to provider-neutral dotted field paths.</summary>
internal static class ExpressionFieldResolver
{
    public static string Resolve(Expression expression)
    {
        expression = StripConvert(expression);
        var parts = new List<string>();
        ResolveCore(expression, parts);
        return string.Join('.', parts);
    }

    private static void ResolveCore(Expression expression, List<string> parts)
    {
        expression = StripConvert(expression);
        switch (expression)
        {
            case ParameterExpression:
                return;
            case MemberExpression member:
                ResolveCore(member.Expression ?? throw new NotSupportedException("Static members are not query fields."), parts);
                parts.Add(member.Member.Name);
                return;
            case MethodCallExpression call when call.Method.Name == "get_Item" && call.Object is not null && call.Arguments.Count == 1:
                ResolveCore(call.Object, parts);
                var key = EvaluateConstant(call.Arguments[0]);
                if (key is not string text || string.IsNullOrWhiteSpace(text))
                    throw new NotSupportedException("Dictionary query keys must be non-empty constant strings.");
                parts.Add(text);
                return;
            case IndexExpression index when index.Object is not null && index.Arguments.Count == 1:
                ResolveCore(index.Object, parts);
                var indexKey = EvaluateConstant(index.Arguments[0]);
                if (indexKey is not string indexText || string.IsNullOrWhiteSpace(indexText))
                    throw new NotSupportedException("Dictionary query keys must be non-empty constant strings.");
                parts.Add(indexText);
                return;
            default:
                throw new NotSupportedException($"Expression '{expression}' is not a supported field path.");
        }
    }

    private static Expression StripConvert(Expression expression)
    {
        while (expression is UnaryExpression unary &&
               unary.NodeType is ExpressionType.Convert or ExpressionType.ConvertChecked)
            expression = unary.Operand;
        return expression;
    }

    private static object? EvaluateConstant(Expression expression)
    {
        expression = StripConvert(expression);
        return expression is ConstantExpression constant
            ? constant.Value
            : Expression.Lambda(expression).Compile().DynamicInvoke();
    }
}
