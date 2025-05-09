namespace BeGeneric.Backend.Common;

public record SelectPropertyData
{
    public string? JoinTableName { get; set; }
    public string? JoinPropertyName { get; set; }
    public string? OriginalTableName { get; set; }
    public string? TableName { get; set; }
    public string? IdPropertyName { get; set; }
    public string? TableDTOName { get; set; }
    public List<Tuple<string, string, string>>? Properties { get; set; }
}
