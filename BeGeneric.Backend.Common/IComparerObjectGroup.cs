using BeGeneric.Backend.Common.Models;
using System.Security.Claims;

namespace BeGeneric.Backend.Common;

public interface IComparerObjectGroup
{
    string? Conjunction { get; set; }

    IComparerObject[]? ComparisonsInternal { get; }

    string? Operator { get; set; }

    Tuple<string, int, List<Tuple<string, object>>> ToSQLQuery(
        ISqlDialect sqlDialect,
        ClaimsPrincipal user, 
        Entity entity, 
        string dbSchema, 
        int counter, 
        string originTableAlias, 
        Dictionary<string, SelectPropertyData> joinData);

    Tuple<string, int, List<Tuple<string, object>>> ToSQLQuery(
        ISqlDialect sqlDialect, 
        string userName, 
        Entity entity, 
        string dbSchema, 
        int counter, 
        string originTableAlias, 
        Dictionary<string, SelectPropertyData> joinData);
}