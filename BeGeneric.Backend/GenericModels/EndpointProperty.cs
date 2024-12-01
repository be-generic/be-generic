using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BeGeneric.Backend.GenericModels
{
    public class EndpointProperty
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Key]
        public Guid EndpointPropertyId { get; set; }

        public Guid EndpointId { get; set; }

        public string PropertyName { get; set; }
        public string PropertyPath { get; set; }


        public virtual Endpoint Endpoint { get; set; }
    }

}
