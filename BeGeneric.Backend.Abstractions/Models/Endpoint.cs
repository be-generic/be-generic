using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BeGeneric.Backend.Common.Models;

public class Endpoint
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Key]
    public Guid EndpointId { get; set; }
    public string EndpointPath { get; set; }
    public Guid StartingEntityId { get; set; }
    public Guid? RoleId { get; set; }
    public string Filter { get; set; }

    public int? DefaultPageNumber { get; set; }
    public int? DefaultPageSize { get; set; }
    public string DefaultSortOrderProperty { get; set; }
    public string DefaultSortOrder { get; set; }


    public virtual Entity StartingEntity { get; set; }
    public virtual Role Role { get; set; }

    public virtual List<EndpointProperty> EndpointProperties { get; set; }
}
