using Microsoft.AspNetCore.OData.Query;
using Microsoft.OData;
using Microsoft.OData.UriParser;
using Synax.EntityFrameworkCore.OData;

namespace Aptison.Booking.Api.OData;

/// <summary>Which members a client's <c>$filter</c> and <c>$orderby</c> read, from the parsed query.</summary>
internal static class ODataPropertyReferences
{
    /// <summary>
    /// False for a query that does not parse: the OData pipeline refuses it with its own 400. A node
    /// this walker does not know counts as a reference, so an unusual construct is refused rather
    /// than let through.
    /// </summary>
    public static bool ReadsAny<T>(HttpRequest request, SynaxODataOptions options, IReadOnlySet<string> edmNames)
    {
        try
        {
            var queryOptions = SynaxQueryODataExtensions.CreateQueryOptions<T>(request, options);

            if (queryOptions.Filter?.FilterClause is { } filter && ReadsAny(filter.Expression, edmNames))
            {
                return true;
            }

            for (var orderBy = queryOptions.OrderBy?.OrderByClause; orderBy is not null; orderBy = orderBy.ThenBy)
            {
                if (ReadsAny(orderBy.Expression, edmNames))
                {
                    return true;
                }
            }

            return false;
        }
        catch (Exception exception) when (exception is ODataException or SynaxODataQueryException)
        {
            return false;
        }
    }

    private static bool ReadsAny(QueryNode node, IReadOnlySet<string> edmNames)
    {
        if (GetPropertyName(node) is { } name && edmNames.Contains(name))
        {
            return true;
        }

        return GetChildren(node) is not { } children || children.Any(child => ReadsAny(child, edmNames));
    }

    private static string? GetPropertyName(QueryNode node) => node switch
    {
        SingleValuePropertyAccessNode access => access.Property.Name,
        CollectionPropertyAccessNode access => access.Property.Name,
        SingleComplexNode complex => complex.Property.Name,
        CollectionComplexNode complex => complex.Property.Name,
        SingleValueOpenPropertyAccessNode open => open.Name,
        CollectionOpenPropertyAccessNode open => open.Name,
        _ => null
    };

    private static IEnumerable<QueryNode>? GetChildren(QueryNode node) => node switch
    {
        ConstantNode or CollectionConstantNode or SearchTermNode or ParameterAliasNode
            or ResourceRangeVariableReferenceNode or NonResourceRangeVariableReferenceNode => [],
        ConvertNode convert => [convert.Source],
        UnaryOperatorNode unary => [unary.Operand],
        BinaryOperatorNode binary => [binary.Left, binary.Right],
        InNode inNode => [inNode.Left, inNode.Right],
        CountNode count => [count.Source],
        SingleValueFunctionCallNode function => function.Parameters,
        SingleResourceFunctionCallNode function => function.Parameters,
        CollectionFunctionCallNode function => function.Parameters,
        CollectionResourceFunctionCallNode function => function.Parameters,
        NamedFunctionParameterNode parameter => [parameter.Value],
        SingleValuePropertyAccessNode access => [access.Source],
        CollectionPropertyAccessNode access => [access.Source],
        SingleComplexNode complex => [complex.Source],
        CollectionComplexNode complex => [complex.Source],
        SingleNavigationNode navigation => [navigation.Source],
        CollectionNavigationNode navigation => [navigation.Source],
        SingleValueOpenPropertyAccessNode open => [open.Source],
        CollectionOpenPropertyAccessNode open => [open.Source],
        SingleValueCastNode cast => [cast.Source],
        SingleResourceCastNode cast => [cast.Source],
        CollectionResourceCastNode cast => [cast.Source],
        AnyNode lambda => [lambda.Source, lambda.Body],
        AllNode lambda => [lambda.Source, lambda.Body],
        _ => null
    };
}
