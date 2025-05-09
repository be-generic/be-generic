namespace BeGeneric.Backend.Common.Models;

public class EntityRoleDefinition
{
    public bool GetOne { get; set; } = false;
    public bool GetAll { get; set; } = false;
    public bool Post { get; set; } = false;
    public bool Put { get; set; } = false;
    public bool Delete { get; set; } = false;

    public string? ViewFilter { get; set; }
    public string? EditFilter { get; set; }

    public string RoleKey { get; set; } = string.Empty;
}
