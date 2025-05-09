using BeGeneric.Backend.Common.Models;

namespace BeGeneric.Backend.Common.Helpers;

public static class CasingHelper
{
    public static string CamelCaseName(this Property property)
    {
        return property == null
            ? null
            : (property.ModelPropertyName ?? property.PropertyName)[0].ToString().ToLowerInvariant() + (property.ModelPropertyName ?? property.PropertyName)[1..];
    }

    public static string CamelCaseName(this Entity entity)
    {
        return entity == null
            ? null
            : (entity.ObjectName ?? entity.TableName)[0].ToString().ToLowerInvariant() + (entity.ObjectName ?? entity.TableName)[1..];
    }

    public static string TitleCaseName(this Property property)
    {
        return property == null
            ? null
            : (property.ModelPropertyName ?? property.PropertyName)[0].ToString().ToUpperInvariant() + (property.ModelPropertyName ?? property.PropertyName)[1..];
    }

    public static string TitleCaseName(this Entity entity)
    {
        return entity == null
            ? null
            : (entity.ObjectName ?? entity.TableName)[0].ToString().ToUpperInvariant() + (entity.ObjectName ?? entity.TableName)[1..];
    }

    public static string TitleCaseOriginalName(this Property property)
    {
        return property == null ? null : property.PropertyName[0].ToString().ToUpperInvariant() + property.PropertyName[1..];
    }

    public static string TitleCaseOriginalName(this Entity entity)
    {
        return entity == null ? null : entity.TableName[0].ToString().ToUpperInvariant() + entity.TableName[1..];
    }

    public static string TitleCaseOriginalName(this string name)
    {
        return name == null ? null : name[0].ToString().ToUpperInvariant() + name[1..];
    }

    public static string CamelCaseName(this string name)
    {
        return name == null ? null : name[0].ToString().ToLowerInvariant() + name[1..];
    }
}
