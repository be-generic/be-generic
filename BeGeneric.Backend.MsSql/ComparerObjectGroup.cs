using BeGeneric.Backend.Common;
using BeGeneric.Backend.Common.Models;
using System.Security.Claims;

namespace BeGeneric.Backend.MsSql;

public class ComparerObjectGroup: IComparerObjectGroup
{
    public string? Conjunction { get; set; }
    public IComparerObjectGroup[]? Comparisons { get; set; }

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
        if (Comparisons == null || Comparisons.Length == 0)
        {
            return new Tuple<string, int, List<Tuple<string, object>>>(string.Empty, counter, new List<Tuple<string, object>>());
        }

        bool isOr = !string.IsNullOrEmpty(Conjunction) && string.Equals(Conjunction, "or", StringComparison.OrdinalIgnoreCase);
        bool isNot = !string.IsNullOrEmpty(Conjunction) && string.Equals(Conjunction, "not", StringComparison.OrdinalIgnoreCase);

        List<Tuple<string, int, List<Tuple<string, object>>>> comps = new();
        int internalCounter = counter;
        foreach (var ci in Comparisons.Where(x => !string.Equals(x.Operator, "contains-any", StringComparison.OrdinalIgnoreCase)))
        {
            var tmp1 = ci.ToSQLQuery(userName, entity, dbSchema, internalCounter, originTableAlias, joinData);
            internalCounter = tmp1.Item2;
            comps.Add(tmp1);
        }

        var comparers = Comparisons.Where(x => string.Equals(x.Operator, "contains-any", StringComparison.OrdinalIgnoreCase)).ToList();
        if (comparers.Count > 0)
        {
            var tmp1 = ComparerObject.ToGroupSQLQuery(comparers, entity, dbSchema, internalCounter, originTableAlias);
            internalCounter = tmp1.Item2;
            comps.Add(tmp1);
        }

        return new Tuple<string, int, List<Tuple<string, object>>>(
            (isNot ? "NOT " : string.Empty) +
            "(" + string.Join(
            !isOr ? " AND " : " OR ",
            comps.Select(x => x.Item1)) + ")",
            internalCounter,
            comps.SelectMany(x => x.Item3).ToList());
    }
}
