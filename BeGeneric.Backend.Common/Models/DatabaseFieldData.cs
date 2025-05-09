namespace BeGeneric.Backend.Common.Models;

public class DatabaseFieldData
{
    public string FieldType { get; set; }
    public bool IsNullable { get; set; }

    public int? MaxLength { get; set; }

    public int? MinLength { get; set; }

    public string[] AllowedValues { get; set; }

    public string Regex { get; set; }
}
