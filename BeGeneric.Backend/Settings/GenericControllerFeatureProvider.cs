using BeGeneric.Backend.Controllers;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.Configuration;
using System.Reflection;

namespace BeGeneric.Backend.Settings
{
    public class GenericControllerFeatureProvider : ControllerFeatureProvider
    {
        private readonly IConfiguration configuration;

        public GenericControllerFeatureProvider(IConfiguration configuration)
        {
            this.configuration = configuration;
        }

        protected override bool IsController(TypeInfo typeInfo)
        {
            var isController = base.IsController(typeInfo);

            if (isController)
            {
                if (typeInfo.BaseType != null && typeInfo.BaseType.IsGenericType && typeInfo.BaseType.GetGenericTypeDefinition() == typeof(GenericController<>))
                {
                    try
                    {
                        if (!configuration.GetValue<bool>(typeInfo.BaseType.GetGenericArguments()[0].Name))
                        {
                            isController = false;
                        }
                    }
                    catch
                    {
                        isController = false;
                    }
                }
            }

            return isController;
        }
    }
}