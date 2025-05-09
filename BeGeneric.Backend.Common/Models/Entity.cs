using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BeGeneric.Backend.Common.Models;

public class Entity
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Key]
    public Guid EntityId { get; set; }
    public string TableName { get; set; }
    public string ObjectName { get; set; }
    public string ControllerName { get; set; }
    public string SoftDeleteColumn { get; set; }

    public virtual List<Property> Properties { get; set; }
    public virtual List<Property> ReferencingProperties { get; set; }

    public virtual List<EntityRole> EntityRoles { get; set; }

    public virtual List<EntityRelation> EntityRelations1 { get; set; }
    public virtual List<EntityRelation> EntityRelations2 { get; set; }
}
