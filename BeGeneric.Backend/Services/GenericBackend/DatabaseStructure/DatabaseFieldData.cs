namespace BeGeneric.Backend.Services.GenericBackend.DatabaseStructure
{
    public class DatabaseFieldData
    {
        public string FieldType { get; set; }
        public bool IsNullable { get; set; }

        public int? MaxLength { get; set; }

        public int? MinLength { get; set; }

        public string[] AllowedValues { get; set; }

        public string Regex { get; set; }
    }
}
