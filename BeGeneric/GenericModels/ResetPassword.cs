using System;
using System.ComponentModel.DataAnnotations;

namespace BeGeneric.Models
{
    public class ResetPassword
    {
        [Key]
        [Required]
        public Guid Id { get; set; }

        [Required]
        public string Username { get; set; }

        [Required]
        public DateTime Expires { get; set; }

        public byte[] CodeHash { get; set; }
    }
}