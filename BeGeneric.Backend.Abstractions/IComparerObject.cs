using BeGeneric.Backend.Common.Models;

namespace BeGeneric.Backend.Common;

public interface IComparerObject: IComparerObjectGroup
{
    string? Operator { get; set; }
    string? Property { get; set; }
    object? Filter { get; set; }

    string ResolvePropertyName(Entity entity, string dbSchema, string originTableAlias, string includedFilter = null, Dictionary<string, SelectPropertyData> joinData = null);
}