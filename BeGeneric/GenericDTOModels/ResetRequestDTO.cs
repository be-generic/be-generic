using System;

namespace BeGeneric.DTOModels
{
    public class ResetRequestDTO
    {
        public Guid Id { get; set; }
        public string Code { get; set; }
        public string Password { get; set; }
        public string Username { get; set; }
    }
}
