namespace BeGeneric.Backend.ApiModels;

public class GraphQLRequest
{
    public string Query { get; set; } = string.Empty;
    public string OperationName { get; set; } = string.Empty;
    public Dictionary<string, object> Variables { get; set; } = new();
}