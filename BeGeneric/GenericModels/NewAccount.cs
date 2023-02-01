using System;

namespace BeGeneric.Models
{
    public class NewAccount
    {
        public string Username { get; set; }
        public string EmailAddress { get; set; }
        public Guid? Role { get; set; }
        public bool IsAdmin { get; set; }
        public string Password { get; set; }
    }
}
