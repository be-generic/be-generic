namespace BeGeneric.Backend.Models
{
    public class ColumnMetadata
    {
        public string TableName { get; set; }
        public string ColumnName { get; set; }

        public string AllowedValues { get; set; }
        public string Regex { get; set; }
    }

}
