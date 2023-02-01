using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace BeGeneric.Models
{
    public class Role
    {
        public Guid Id { get; set; }

        [MaxLength(100)]
        public string RoleName { get; set; }

        public string RoleDescription { get; set; }

        public virtual List<Account> Accounts { get; set; }

        public virtual List<EntityRole> EntityRoles { get; set; }
    }
}
