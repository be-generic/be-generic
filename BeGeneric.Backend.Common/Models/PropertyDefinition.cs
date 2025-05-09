namespace BeGeneric.Backend.Common.Models;

public class PropertyDefinition
{
    public string PropertyName { get; set; }
    public string? ModelPropertyName { get; set; }

    public bool? IsKey { get; set; } = false;
    public bool? IsReadOnly { get; set; } = false;
    public bool? IsHidden { get; set; } = false;

    /// <summary>
    /// Default value can have one of the following supported values:
    ///  - $user: current user id
    /// </summary>
    public string? DefaultValue { get; set; } = null;

    public string? RelatedModelPropertyName { get; set; }

    public string? ReferencingEntityKey { get; set; }
}
