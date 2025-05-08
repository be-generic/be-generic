namespace BeGeneric.Backend.Services;

public class GenerateSelectQueryModel
{
    public string JoinQueryPart { get; set; } = string.Empty;
    public List<string> PropertyValues { get; set; } = new List<string>();
    public List<string> PropertyNames { get; set; } = new List<string>();
    public List<string> ColumnPaths { get; set; } = new List<string>();
    public List<string> OutputPaths { get; set; } = new List<string>();
}
