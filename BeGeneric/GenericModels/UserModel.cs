using System;

namespace BeGeneric.Models
{
    public class UserModel
    {
        public string UserName { get; set; }
        public string EmailAddress { get; set; }
        public string[] Roles { get; set; }
    }
}
