using BeGeneric.Backend.Common.Models;
using System.Security.Claims;

namespace BeGeneric.Backend.Common;

public interface IComparerObjectGroup
{
    string? Conjunction { get; set; }

    IComparerObject[]? Comparisons { get; set; }

    string? Operator { get; set; }

    Tuple<string, int, List<Tuple<string, object>>> ToSQLQuery(ClaimsPrincipal user, Entity entity, string dbSchema, int counter, string originTableAlias, Dictionary<string, SelectPropertyData> joinData);

    Tuple<string, int, List<Tuple<string, object>>> ToSQLQuery(string userName, Entity entity, string dbSchema, int counter, string originTableAlias, Dictionary<string, SelectPropertyData> joinData);
}