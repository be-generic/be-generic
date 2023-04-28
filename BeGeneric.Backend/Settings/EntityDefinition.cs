namespace BeGeneric.Backend.Settings
{
    public class EntityDefinition
    {
        public string EntityKey { get; set; }
        public string TableName { get; set; }
        public string? ObjectName { get; set; }
        public string? ControllerName { get; set; }
        public string? SoftDeleteColumn { get; set; }

        public virtual List<PropertyDefinition>? Properties { get; set; }

        public virtual List<EntityRoleDefinition>? EntityRoles { get; set; }

        public virtual List<EntityRelationDefinition>? EntityRelations { get; set; }
    }
}
