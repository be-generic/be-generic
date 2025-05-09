using System.ComponentModel.DataAnnotations.Schema;

namespace BeGeneric.Backend.Common.Models;

public class EntityRole
{
    public Guid EntitiesEntityId { get; set; }
    public Guid RolesId { get; set; }

    public bool GetOne { get; set; }
    public bool GetAll { get; set; }
    public bool Post { get; set; }
    public bool Put { get; set; }
    public bool Delete { get; set; }

    public string ViewFilter { get; set; }
    public string EditFilter { get; set; }

    [ForeignKey(nameof(RolesId))]
    public virtual Role Role { get; set; }

    [ForeignKey(nameof(EntitiesEntityId))]
    public virtual Entity Entity { get; set; }
}
