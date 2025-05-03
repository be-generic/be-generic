using BeGeneric.Backend.Controllers;
using BeGeneric.Backend.Services.BeGeneric.DatabaseStructure;
using BeGeneric.Backend.Services.GenericBackend.Helpers;
using BeGeneric.Backend.Settings;
using BeGeneric.GenericModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using System;
using System.Reflection;

namespace BeGeneric.Backend.Services.BeGeneric
{
    public class ServiceControllerDynamicRouteProvider : IApplicationModelProvider
    {
        private readonly List<EntityDefinition> entities;
        private readonly IDatabaseStructureService structureService;

        public ServiceControllerDynamicRouteProvider(List<EntityDefinition> entities, IDatabaseStructureService structureService)
        {
            this.entities = entities;
            this.structureService = structureService;
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

            Dictionary<string, Type> generatedTypes = new();

            foreach (var entity in this.entities.Where(x => !string.IsNullOrEmpty(x.ControllerName)))
            {
                Type dtoType = DTOObjectHelper.BuildDTOObject(this.entities, entity, structureService, generatedTypes);
                Type postDtoType = DTOObjectHelper.BuildDTOObject(this.entities, entity, structureService, generatedTypes, true);

                if (!generatedTypes.ContainsKey("Search" + (string.IsNullOrEmpty(entity.TableName) ? entity.ObjectName : entity.TableName)))
                {
                    Type pageResultType = typeof(PagedResult<>);
                    Type getType = pageResultType.MakeGenericType(dtoType);
                    generatedTypes.Add("Search" + (string.IsNullOrEmpty(entity.TableName) ? entity.ObjectName : entity.TableName), getType);
                }

                Type getReturnType = generatedTypes["Search" + (string.IsNullOrEmpty(entity.TableName) ? entity.ObjectName : entity.TableName)];

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

                foreach (var crossRelation in this.entities.Where(x => x.EntityRelations != null).SelectMany(x => x.EntityRelations).Where(x => (entity.EntityRelations?.Contains(x) ?? false) || x.RelatedEntityKey == entity.EntityKey))
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

                if (entity.EntityRoles != null && entity.EntityRoles.Count > 0 && !entity.EntityRoles.Any(x => x.Delete))
                {
                    var actualDeleteAction = newController.Actions.Where(x => x.ActionName.EndsWith("Delete")).First();
                    newController.Actions.Remove(actualDeleteAction);
                }
                else
                {
                    var actualDeleteAction = newController.Actions.Where(x => x.ActionName.EndsWith("Delete")).First();
                    actualDeleteAction.Filters.Add(new ProducesResponseTypeAttribute(StatusCodes.Status204NoContent));
                    actualDeleteAction.Filters.Add(new ProducesResponseTypeAttribute(StatusCodes.Status401Unauthorized));
                    actualDeleteAction.Filters.Add(new ProducesResponseTypeAttribute(StatusCodes.Status403Forbidden));
                    actualDeleteAction.Filters.Add(new ProducesResponseTypeAttribute(StatusCodes.Status500InternalServerError));
                }

                var actualPostAction = newController.Actions.Where(x => x.ActionName.EndsWith("Post")).First();
                if (entity.EntityRoles != null && entity.EntityRoles.Count > 0 && !entity.EntityRoles.Any(x => x.Post))
                {
                    newController.Actions.Remove(actualPostAction);
                }
                else
                {
                    actualPostAction.Parameters.Remove(actualPostAction.Parameters.Where(x => x.Name == "fieldValues").First());
                    actualPostAction.Parameters.Add(new ParameterModel(new DummyParameterInfo(postDtoType, "fieldValues", actualPostAction.ActionMethod), new List<object>())
                    {
                        Action = actualPostAction,
                        ParameterName = "fieldValues",
                    });

                    actualPostAction.Filters.Add(new ProducesResponseTypeAttribute(dtoType, StatusCodes.Status200OK));
                    actualPostAction.Filters.Add(new ProducesResponseTypeAttribute(StatusCodes.Status401Unauthorized));
                    actualPostAction.Filters.Add(new ProducesResponseTypeAttribute(StatusCodes.Status403Forbidden));
                    actualPostAction.Filters.Add(new ProducesResponseTypeAttribute(StatusCodes.Status500InternalServerError));
                }

                var actualPatchAction = newController.Actions.Where(x => x.ActionName.EndsWith("Patch")).First();
                var actualPatchReturnAction = newController.Actions.Where(x => x.ActionName.EndsWith("PatchReturn")).First();

                if (entity.EntityRoles != null && entity.EntityRoles.Count > 0 && !entity.EntityRoles.Any(x => x.Put))
                {
                    newController.Actions.Remove(actualPatchAction);
                    newController.Actions.Remove(actualPatchReturnAction);
                }
                else
                {
                    actualPatchAction.Parameters.Remove(actualPatchAction.Parameters.Where(x => x.Name == "fieldValues").First());
                    actualPatchAction.Parameters.Add(new ParameterModel(new DummyParameterInfo(dtoType, "fieldValues", actualPatchAction.ActionMethod), new List<object>())
                    {
                        Action = actualPatchAction,
                        ParameterName = "fieldValues",
                    });

                    actualPatchAction.Filters.Add(new ProducesResponseTypeAttribute(StatusCodes.Status204NoContent));
                    actualPatchAction.Filters.Add(new ProducesResponseTypeAttribute(StatusCodes.Status401Unauthorized));
                    actualPatchAction.Filters.Add(new ProducesResponseTypeAttribute(StatusCodes.Status403Forbidden));
                    actualPatchAction.Filters.Add(new ProducesResponseTypeAttribute(StatusCodes.Status500InternalServerError));

                    actualPatchReturnAction.Parameters.Remove(actualPatchReturnAction.Parameters.Where(x => x.Name == "fieldValues").First());
                    actualPatchReturnAction.Parameters.Add(new ParameterModel(new DummyParameterInfo(dtoType, "fieldValues", actualPatchReturnAction.ActionMethod), new List<object>())
                    {
                        Action = actualPatchReturnAction,
                        ParameterName = "fieldValues",
                    });

                    actualPatchReturnAction.Filters.Add(new ProducesResponseTypeAttribute(dtoType, StatusCodes.Status200OK));
                    actualPatchReturnAction.Filters.Add(new ProducesResponseTypeAttribute(StatusCodes.Status401Unauthorized));
                    actualPatchReturnAction.Filters.Add(new ProducesResponseTypeAttribute(StatusCodes.Status403Forbidden));
                    actualPatchReturnAction.Filters.Add(new ProducesResponseTypeAttribute(StatusCodes.Status500InternalServerError));
                }

                var actualGetAction = newController.Actions.Where(x => x.ActionName.Contains("GetAll")).First();
                var actualGetFilterAction = newController.Actions.Where(x => x.ActionName.Contains("GetWithFilter")).First();
                if (entity.EntityRoles != null && entity.EntityRoles.Count > 0 && !entity.EntityRoles.Any(x => x.GetAll))
                {
                    newController.Actions.Remove(actualGetAction);
                    newController.Actions.Remove(actualGetFilterAction);
                }
                else
                {
                    actualGetAction.Filters.Add(new ProducesResponseTypeAttribute(getReturnType, StatusCodes.Status200OK));
                    actualGetAction.Filters.Add(new ProducesResponseTypeAttribute(StatusCodes.Status400BadRequest));
                    actualGetAction.Filters.Add(new ProducesResponseTypeAttribute(StatusCodes.Status401Unauthorized));
                    actualGetAction.Filters.Add(new ProducesResponseTypeAttribute(StatusCodes.Status403Forbidden));
                    actualGetAction.Filters.Add(new ProducesResponseTypeAttribute(StatusCodes.Status500InternalServerError));

                    actualGetFilterAction.Filters.Add(new ProducesResponseTypeAttribute(getReturnType, StatusCodes.Status200OK));
                    actualGetFilterAction.Filters.Add(new ProducesResponseTypeAttribute(StatusCodes.Status400BadRequest));
                    actualGetFilterAction.Filters.Add(new ProducesResponseTypeAttribute(StatusCodes.Status401Unauthorized));
                    actualGetFilterAction.Filters.Add(new ProducesResponseTypeAttribute(StatusCodes.Status403Forbidden));
                    actualGetFilterAction.Filters.Add(new ProducesResponseTypeAttribute(StatusCodes.Status500InternalServerError));
                }

                var actualGetOneAction = newController.Actions.Where(x => x.ActionName.Contains("GetOne")).First();
                if (entity.EntityRoles != null && entity.EntityRoles.Count > 0 && !entity.EntityRoles.Any(x => x.GetOne))
                {
                    newController.Actions.Remove(actualGetOneAction);
                }
                else
                {
                    actualGetOneAction.Filters.Add(new ProducesResponseTypeAttribute(dtoType, StatusCodes.Status200OK));
                    actualGetOneAction.Filters.Add(new ProducesResponseTypeAttribute(StatusCodes.Status400BadRequest));
                    actualGetOneAction.Filters.Add(new ProducesResponseTypeAttribute(StatusCodes.Status401Unauthorized));
                    actualGetOneAction.Filters.Add(new ProducesResponseTypeAttribute(StatusCodes.Status403Forbidden));
                    actualGetOneAction.Filters.Add(new ProducesResponseTypeAttribute(StatusCodes.Status404NotFound));
                    actualGetOneAction.Filters.Add(new ProducesResponseTypeAttribute(StatusCodes.Status500InternalServerError));
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

    public class DummyParameterInfo: ParameterInfo
    {
        private string name;
        private Type type;

        public DummyParameterInfo(Type type, string name, MemberInfo actionMember) : base() 
        { 
            this.type = type;
            this.name = name;
            this.MemberImpl = actionMember;
        }

        public override string? Name => this.name;

        public override Type ParameterType => type;

        public override bool HasDefaultValue => false;
    }
        
}
public class SimplePropertyInfo : PropertyInfo
{
    private readonly string name;
    private readonly Type propertyType;

    public SimplePropertyInfo(string name, Type propertyType)
    {
        if (propertyType == null)
        {
            throw new ArgumentNullException(null, nameof(propertyType));
        }

        this.name = name;
        this.propertyType = propertyType;
    }

    public override string Name => name;

    public override Type PropertyType => propertyType;

    public override PropertyAttributes Attributes => throw new NotImplementedException();

    public override bool CanRead => true;

    public override bool CanWrite => true;

    public override Type DeclaringType => throw new NotImplementedException();

    public override Type ReflectedType => throw new NotImplementedException();

    public override MethodInfo[] GetAccessors(bool nonPublic) => throw new NotImplementedException();

    public override MethodInfo GetGetMethod(bool nonPublic) => throw new NotImplementedException();

    public override ParameterInfo[] GetIndexParameters() => throw new NotImplementedException();

    public override MethodInfo GetSetMethod(bool nonPublic) => throw new NotImplementedException();

    public override object GetValue(object obj, BindingFlags invokeAttr, Binder binder, object[] index, System.Globalization.CultureInfo culture) => throw new NotImplementedException();

    public override void SetValue(object obj, object value, BindingFlags invokeAttr, Binder binder, object[] index, System.Globalization.CultureInfo culture) => throw new NotImplementedException();

    public override object[] GetCustomAttributes(Type attributeType, bool inherit)
    {
        return new Attribute[0];
    }

    public override object[] GetCustomAttributes(bool inherit) => throw new NotImplementedException();

    public override bool IsDefined(Type attributeType, bool inherit) => throw new NotImplementedException();
}
