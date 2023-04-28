namespace BeGeneric.Backend.Settings
{
    public class PropertyDefinition
    {
        public string PropertyName { get; set; }
        public string? ModelPropertyName { get; set; }

        public bool? IsKey { get; set; } = false;
        public bool? IsReadOnly { get; set; } = false;
        
        public string? RelatedModelPropertyName { get; set; }

        public string? ReferencingEntityKey { get; set; }
    }
}
