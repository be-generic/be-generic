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

                var deleteAction = newController.Actions.Where(x => x.ActionName.Contains("DeleteRelatedEntity")).First();
                var postAction = newController.Actions.Where(x => x.ActionName.Contains("PostRelatedEntity")).First();

                foreach (var crossRelation in this.entities.Where(x => x.EntityRelations != null).SelectMany(x => x.EntityRelations).Where(x => entity.EntityRelations.Contains(x) || x.RelatedEntityKey == entity.EntityKey))
                {
                    var newDeleteAction = new ActionModel(deleteAction)
                    {
                        ActionName = "delete_" + crossRelation.RelatedEntityPropertyName.ToLowerInvariant()
                    };

                    newDeleteAction.Selectors.Clear();
                    newDeleteAction.Selectors.Add(new SelectorModel(deleteAction.Selectors[0])
                    {
                        AttributeRouteModel = new AttributeRouteModel(deleteAction.Selectors[0].AttributeRouteModel)
                        {
                            Template = "{id}/" + crossRelation.RelatedEntityPropertyName.ToLowerInvariant() + "/{relatedEntityId}"
                        }
                    });

                    newController.Actions.Add(newDeleteAction);

                    var newPostAction = new ActionModel(postAction)
                    {
                        ActionName = "post_" + crossRelation.RelatedEntityPropertyName.ToLowerInvariant()
                    };

                    newPostAction.Selectors.Clear();
                    newPostAction.Selectors.Add(new SelectorModel(postAction.Selectors[0])
                    {
                        AttributeRouteModel = new AttributeRouteModel(postAction.Selectors[0].AttributeRouteModel)
                        {
                            Template = "{id}/" + crossRelation.RelatedEntityPropertyName.ToLowerInvariant()
                        }
                    });

                    newController.Actions.Add(newPostAction);
                }

                newController.Actions.Remove(postAction);
                newController.Actions.Remove(deleteAction);

                context.Result.Controllers.Add(newController);
            }

            context.Result.Controllers.Remove(genericController);
        }

        public void OnProvidersExecuted(ApplicationModelProviderContext context) { }

        public int Order => -1000 + 10;
    }
}
