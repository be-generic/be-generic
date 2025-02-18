using BeGeneric.Backend.GenericModels;
using System.Security.Claims;

namespace BeGeneric.Backend.Services.GenericBackend;

public class ComparerObjectGroup
{
    public string? Conjunction { get; set; }
    public ComparerObject[]? Comparisons { get; set; }

    public virtual Tuple<string, int, List<Tuple<string, object>>> ToSQLQuery(ClaimsPrincipal user, Entity entity, string dbSchema, int counter, string originTableAlias, Dictionary<string, SelectPropertyData> joinData)
    {
        if (user.Identity.IsAuthenticated)
        {
            ClaimsIdentity userData = user.Identity as ClaimsIdentity;
            return ToSQLQuery(userData.FindFirst("id").Value, entity, dbSchema, counter, originTableAlias, joinData);
        }
        else
        {
            return ToSQLQuery((string)null, entity, dbSchema, counter, originTableAlias, joinData);
        }
    }

    public virtual Tuple<string, int, List<Tuple<string, object>>> ToSQLQuery(string userName, Entity entity, string dbSchema, int counter, string originTableAlias, Dictionary<string, SelectPropertyData> joinData)
    {
        bool isOr = !string.IsNullOrEmpty(Conjunction) && string.Equals(Conjunction, "or", StringComparison.OrdinalIgnoreCase);
        bool isNot = !string.IsNullOrEmpty(Conjunction) && string.Equals(Conjunction, "not", StringComparison.OrdinalIgnoreCase);

        List<Tuple<string, int, List<Tuple<string, object>>>> comps = new();
        foreach (var ci in Comparisons.Where(x => !string.Equals(x.Operator, "contains-any", StringComparison.OrdinalIgnoreCase)))
        {
            var tmp1 = ci.ToSQLQuery(userName, entity, dbSchema, counter + 1, originTableAlias, joinData);
            counter = tmp1.Item2;
            comps.Add(tmp1);
        }

        var comparers = Comparisons.Where(x => string.Equals(x.Operator, "contains-any", StringComparison.OrdinalIgnoreCase)).ToList();
        if (comparers.Count > 0)
        {
            comps.Add(ComparerObject.ToGroupSQLQuery(comparers, entity, dbSchema, ++counter, originTableAlias));
        }

        return new Tuple<string, int, List<Tuple<string, object>>>(
            (isNot ? "NOT " : string.Empty) +
            "(" + string.Join(
            !isOr ? " AND " : " OR ",
            comps.Select(x => x.Item1)) + ")",
            counter + 1,
            comps.SelectMany(x => x.Item3).ToList());
    }
}
