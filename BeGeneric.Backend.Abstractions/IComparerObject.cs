namespace BeGeneric.Backend.Common;

public interface IComparerObject: IComparerObjectGroup
{
    string? Operator { get; set; }
    string? Property { get; set; }
    object? Filter { get; set; }
}