using BeGeneric.Backend.Common;
using BeGeneric.Backend.Database;
using GraphQLParser.AST;
using GraphQLParser;

namespace BeGeneric.Backend.Utility;

public class GraphQLToGenericQueryConverter
{
    public static (string controllerName, int? page, int? pageSize, string? sortProperty, string? sortOrder, IComparerObject? filter, string[] properties) ConvertGraphQLToGetParameters(
        string graphqlQuery)
    {
        var document = Parser.Parse(graphqlQuery);
        var operation = document.Definitions.OfType<GraphQLOperationDefinition>().FirstOrDefault();
        
        var field = (operation?.SelectionSet.Selections.OfType<GraphQLField>().FirstOrDefault()) ?? throw new Exception("No valid root field found");

        string controllerName = field.Name.StringValue;
        var args = field.Arguments?.ToDictionary(a => a.Name.StringValue, a => a.Value) ?? new();

        int? page = GetInt(args, "page");
        int pageSize = GetInt(args, "pageSize") ?? 10;
        string? sortProperty = GetString(args, "sortProperty");
        string sortOrder = GetString(args, "sortOrder") ?? "ASC";

        IComparerObject? filter = null;
        if (args.TryGetValue("where", out var whereValue) && whereValue is GraphQLObjectValue whereObj)
        {
            filter = ConvertGraphQLWhereObject(whereObj);
        }
        else if (args.TryGetValue("filter", out whereValue) && whereValue is GraphQLObjectValue filterObj)
        {
            filter = ConvertGraphQLWhereObject(filterObj);
        }

        string[] properties = ExtractProperties(field.SelectionSet).ToArray();

        return new (controllerName, page, pageSize, sortProperty, sortOrder, filter, properties);
    }

    private static int? GetInt(Dictionary<string, GraphQLValue> args, string key)
    {
        return args.TryGetValue(key, out var val) && val is GraphQLIntValue iv
            ? int.Parse(iv.Value)
            : (int?)null;
    }

    private static string? GetString(Dictionary<string, GraphQLValue> args, string key)
    {
        return args.TryGetValue(key, out var val) && val is GraphQLStringValue sv
            ? sv.Value.ToString()
            : null;
    }

    private static ComparerObject ConvertGraphQLWhereObject(GraphQLObjectValue obj, string prefix = "")
    {
        var comparisons = new List<ComparerObject>();

        foreach (var field in obj.Fields)
        {
            var name = field.Name.StringValue;
            var fullPath = string.IsNullOrEmpty(prefix) ? name : $"{prefix}.{name}";

            if (name.Equals("or", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("and", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("not", StringComparison.OrdinalIgnoreCase))
            {
                if (field.Value is GraphQLListValue list)
                {
                    var nested = new List<ComparerObject>();
                    foreach (var item in list.Values.OfType<GraphQLObjectValue>())
                    {
                        nested.Add(ConvertGraphQLWhereObject(item));
                    }

                    comparisons.Add(new ComparerObject
                    {
                        Conjunction = name.ToLowerInvariant(),
                        Comparisons = nested.ToArray()
                    });
                }
            }
            else if (field.Value is GraphQLObjectValue opObj)
            {
                // Check for nested operator style (existing behavior)
                bool isOperatorSet = opObj.Fields.All(f =>
                    f.Name.StringValue is "eq" or "neq" or "contains" or "gte" or "lte" or "gt" or "lt" or "startswith" or "endswith" or "null" or "not null");

                if (isOperatorSet)
                {
                    foreach (var opField in opObj.Fields)
                    {
                        var op = opField.Name.StringValue;
                        var val = ExtractValue(opField.Value);

                        comparisons.Add(new ComparerObject
                        {
                            Property = fullPath,
                            Operator = op,
                            Filter = val
                        });
                    }
                }
                else
                {
                    // Nested object: recurse
                    var nestedGroup = ConvertGraphQLWhereObject(opObj, fullPath);
                    comparisons.AddRange(nestedGroup.Comparisons);
                }
            }
            else
            {
                // New: Flattened syntax, e.g. title_contains: "abc"
                var supportedSuffixes = new[] {
                    "_eq", "_neq", "_contains", "_startswith", "_endswith",
                    "_gt", "_lt", "_gte", "_lte", "_null", "_notnull"
                };

                var matchingSuffix = supportedSuffixes.FirstOrDefault(s => name.EndsWith(s, StringComparison.OrdinalIgnoreCase));
                if (matchingSuffix != null)
                {
                    var property = name[..^matchingSuffix.Length];
                    var op = matchingSuffix.TrimStart('_').ToLowerInvariant();
                    var val = ExtractValue(field.Value);

                    comparisons.Add(new ComparerObject
                    {
                        Property = string.IsNullOrEmpty(prefix) ? property : $"{prefix}.{property}",
                        Operator = op switch
                        {
                            "notnull" => "not null",
                            _ => op
                        },
                        Filter = val
                    });
                }
                else
                {
                    // New: Shorthand form, e.g. title: "report" becomes title eq "report"
                    var val = ExtractValue(field.Value);
                    comparisons.Add(new ComparerObject
                    {
                        Property = fullPath,
                        Operator = "eq",
                        Filter = val
                    });
                }
            }

        }

        return new ComparerObject
        {
            Conjunction = "and",
            Comparisons = comparisons.ToArray()
        };
    }

    private static List<string> ExtractProperties(GraphQLSelectionSet? selectionSet, string? prefix = null)
    {
        var props = new List<string>();

        if (selectionSet == null) return props;

        foreach (var sel in selectionSet.Selections.OfType<GraphQLField>())
        {
            var name = sel.Name.StringValue;
            var fullName = string.IsNullOrEmpty(prefix) ? name : $"{prefix}.{name}";

            if (sel.SelectionSet != null)
            {
                props.AddRange(ExtractProperties(sel.SelectionSet, fullName));
            }
            else
            {
                props.Add(fullName);
            }
        }

        return props;
    }

    private static object? ExtractValue(GraphQLValue value)
    {
        return value switch
        {
            GraphQLBooleanValue b => b.Value,
            GraphQLIntValue i => int.Parse(i.Value),
            GraphQLFloatValue f => double.Parse(f.Value),
            GraphQLStringValue s => s.Value.ToString(),
            _ => value.ToString()
        };
    }
}