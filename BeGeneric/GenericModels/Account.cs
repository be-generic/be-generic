using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BeGeneric.Models
{
    public class Account
    {
        [Key]
        [MaxLength(100)]
        public string Username { get; set; }

        [MaxLength(500)]
        public string EmailAddress { get; set; }

        [MaxLength(500)]
        [NotMapped]
        public string Password { get; set; }

        [MaxLength(500)]
        public byte[] PasswordHash { get; set; }

        public Guid RoleId { get; set; }
        public virtual Role Role { get; set; }
    }
}
