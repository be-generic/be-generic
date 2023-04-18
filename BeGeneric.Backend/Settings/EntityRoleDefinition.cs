namespace BeGeneric.Backend.Settings
{
    public class EntityRoleDefinition
    {
        public bool GetOne { get; set; }
        public bool GetAll { get; set; }
        public bool Post { get; set; }
        public bool Put { get; set; }
        public bool Delete { get; set; }

        public string ViewFilter { get; set; }
        public string EditFilter { get; set; }

        public string RoleKey { get; set; }
    }
}
