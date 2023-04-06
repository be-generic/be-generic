using System.ComponentModel.DataAnnotations;

namespace BeGeneric.Backend.Models
{
    public class Role
    {
        public Guid Id { get; set; }

        [MaxLength(100)]
        public string RoleName { get; set; }

        public string RoleDescription { get; set; }

        public virtual List<EntityRole> EntityRoles { get; set; }
    }
}
