using BeGeneric.Backend.Controllers;
using BeGeneric.Backend.Settings;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace BeGeneric.Backend.Services.BeGeneric
{
    public class ServiceControllerDynamicRouteProvider : IApplicationModelProvider
    {
        private readonly List<EntityDefinition> entities;

        public ServiceControllerDynamicRouteProvider(List<EntityDefinition> entities)
        {
            this.entities = entities;
        }

        public void OnProvidersExecuting(ApplicationModelProviderContext context)
        {
            if (this.entities == null)
            {
                return;
            }

            var genericController = context
                .Result
                .Controllers
                .Where(x => x.ControllerType.BaseType != null && 
                    x.ControllerType.BaseType.IsGenericType && 
                    x.ControllerType.BaseType.GetGenericTypeDefinition() == typeof(GenericController<>))
                .First();

            var selector = genericController.Selectors.First();

            foreach (var entity in this.entities.Where(x => !string.IsNullOrEmpty(x.ControllerName)))
            {
                var newController = new ControllerModel(genericController)
                {
                    ControllerName = entity.ControllerName
                };

                newController.Selectors.Clear();
                newController.Selectors.Add(new SelectorModel(selector)
                {
                    AttributeRouteModel = new AttributeRouteModel(selector.AttributeRouteModel)
                    {
                        Template = entity.ControllerName
                    }
                });

                context.Result.Controllers.Add(newController);
            }

            context.Result.Controllers.Remove(genericController);
        }

        public void OnProvidersExecuted(ApplicationModelProviderContext context) { }

        public int Order => -1000 + 10;
    }
}
