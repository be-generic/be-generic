namespace BeGeneric.Backend.Settings
{
    public class EntityRelationDefinition
    {
        public string CrossTableName { get; set; }

        public string EntityReferencingColumnName { get; set; }
        public string EntityPropertyName { get; set; }

        public string RelatedEntityReferencingColumnName { get; set; }
        public string RelatedEntityPropertyName { get; set; }

        public string? ValidFromColumnName { get; set; }
        public string? ValidToColumnName { get; set; }
        public string? ActiveColumnName { get; set; }

        public bool ShowInEntity { get; set; } = false;
        public bool ShowInRelatedEntity { get; set; } = false;

        public string RelatedEntityKey { get; set; }
    }
}
