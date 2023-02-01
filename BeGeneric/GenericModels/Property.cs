using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BeGeneric.Models
{
    public class Property
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Key]
        public Guid PropertyId { get; set; }
        public string PropertyName { get; set; }
        public string ModelPropertyName { get; set; }
        public Guid EntityId { get; set; }
        public Guid? ReferencingEntityId { get; set; }
        public bool IsKey { get; set; }
        public bool UseInBaseModel { get; set; }
        public bool IsReadOnly { get; set; }

        public string RelatedModelPropertyName { get; set; }
        public bool DisplayInRelatedEntityBaseModel { get; set; }


        public virtual Entity Entity { get; set; }

        public virtual Entity ReferencingEntity { get; set; }
    }
}
