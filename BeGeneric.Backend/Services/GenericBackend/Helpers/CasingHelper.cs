using BeGeneric.Backend.Models;

namespace BeGeneric.Helpers
{
    internal static class CasingHelper
    {
        internal static string CamelCaseName(this Property property)
        {
            if (property == null)
            {
                return null;
            }

            return (property.ModelPropertyName ?? property.PropertyName)[0].ToString().ToLowerInvariant() + (property.ModelPropertyName ?? property.PropertyName)[1..];
        }

        internal static string CamelCaseName(this Entity entity)
        {
            if (entity == null)
            {
                return null;
            }

            return (entity.ObjectName ?? entity.TableName)[0].ToString().ToLowerInvariant() + (entity.ObjectName ?? entity.TableName)[1..];
        }

        internal static string TitleCaseName(this Property property)
        {
            if (property == null)
            {
                return null;
            }

            return (property.ModelPropertyName ?? property.PropertyName)[0].ToString().ToUpperInvariant() + (property.ModelPropertyName ?? property.PropertyName)[1..];
        }

        internal static string TitleCaseName(this Entity entity)
        {
            if (entity == null)
            {
                return null;
            }

            return (entity.ObjectName ?? entity.TableName)[0].ToString().ToUpperInvariant() + (entity.ObjectName ?? entity.TableName)[1..];
        }

        internal static string TitleCaseOriginalName(this Property property)
        {
            if (property == null)
            {
                return null;
            }

            return property.PropertyName[0].ToString().ToUpperInvariant() + property.PropertyName[1..];
        }

        internal static string TitleCaseOriginalName(this Entity entity)
        {
            if (entity == null)
            {
                return null;
            }

            return entity.TableName[0].ToString().ToUpperInvariant() + entity.TableName[1..];
        }

        internal static string TitleCaseOriginalName(this string name)
        {
            if (name == null)
            {
                return null;
            }

            return name[0].ToString().ToUpperInvariant() + name[1..];
        }

        internal static string CamelCaseName(this string name)
        {
            if (name == null)
            {
                return null;
            }

            return name[0].ToString().ToLowerInvariant() + name[1..];
        }
    }
}
