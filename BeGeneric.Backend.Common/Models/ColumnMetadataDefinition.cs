namespace BeGeneric.Backend.Common.Models;

public class ColumnMetadataDefinition
{
    public string TableName { get; set; }
    public string ColumnName { get; set; }

    public string? AllowedValues { get; set; }
    public string? Regex { get; set; }
    public bool? IsRequired { get; set; }
}
