﻿using BeGeneric.Backend.Common.Models;

namespace BeGeneric.Backend.Services.GenericBackend.Helpers;

internal static class EntityLoaderHelper
{
    private static Dictionary<string, Guid> entityIds = new Dictionary<string, Guid>();
    private static object lockObject = new object();

    private static string GetEntityDefinitionKey(EntityDefinition entity)
    {
        return entity.EntityKey;
    }

    internal static List<Entity> ProcessEntities(this IEnumerable<EntityDefinition> entities)
    {
        lock (lockObject)
        {
            if (entityIds.Count == 0)
            {
                foreach (var entity in entities)
                {
                    entityIds.Add(GetEntityDefinitionKey(entity), Guid.NewGuid());
                }
            }
        }

        List<Entity> entitiesList = new();

        foreach (var entity in entities)
        {
            entitiesList.Add(entity.ToEntity());
        }

        foreach (var entity in entitiesList)
        {
            foreach (var property in entity.Properties)
            {
                if (property.ReferencingEntityId != null)
                {
                    property.ReferencingEntity = entitiesList.First(x => x.EntityId == property.ReferencingEntityId);
                }

                property.Entity = entitiesList.First(x => x.EntityId == property.EntityId);
            }

            foreach (var relation in entitiesList.SelectMany(x => x.EntityRelations1).Where(x => x.Entity2Id == entity.EntityId))
            {
                relation.Entity1 = entitiesList.First(x => x.EntityId == relation.Entity1Id);
                relation.Entity2 = entitiesList.First(x => x.EntityId == relation.Entity2Id);
            }

            entity.ReferencingProperties = entitiesList.SelectMany(x => x.Properties).Where(x => x.ReferencingEntityId == entity.EntityId).ToList();
            entity.EntityRelations2 = entitiesList.SelectMany(x => x.EntityRelations1).Where(x => x.Entity2Id == entity.EntityId).ToList();
        }

        return entitiesList;
    }

    internal static Entity ToEntity(this EntityDefinition entity)
    {
        Guid id = entityIds[GetEntityDefinitionKey(entity)];

        var ent = new Entity()
        {
            ControllerName = entity.ControllerName,
            EntityRelations1 = entity.EntityRelations != null ? entity.EntityRelations.Select(x => x.ToEntityRelation(id)).ToList() : new List<EntityRelation>(),
            EntityRoles = entity.EntityRoles?.Select(x => x.ToEntityRole(id))?.ToList() ?? new List<EntityRole>(),
            ObjectName = entity.ObjectName,
            Properties = entity.Properties.Select(x => x.ToProperty(id)).ToList(),
            SoftDeleteColumn = entity.SoftDeleteColumn,
            TableName = entity.TableName,
            EntityId = id,
        };

        if (ent.EntityRoles != null)
        {
            foreach (var er in ent.EntityRoles)
            {
                er.Entity = ent;
                er.EntitiesEntityId = id;
            }
        }

        return ent;
    }

    internal static Property ToProperty(this PropertyDefinition property, Guid entityId)
    {
        return new Property()
        {
            EntityId = entityId,
            PropertyId = Guid.NewGuid(),
            IsKey = property.IsKey ?? false,
            IsHidden = property.IsHidden ?? false,
            IsReadOnly = property.IsReadOnly ?? !string.IsNullOrEmpty(property.DefaultValue),
            DefaultValue = property.DefaultValue,
            ModelPropertyName = property.ModelPropertyName,
            PropertyName = property.PropertyName,
            RelatedModelPropertyName = property.RelatedModelPropertyName,
            ReferencingEntityId = string.IsNullOrEmpty(property.ReferencingEntityKey) ? null : entityIds[property.ReferencingEntityKey],
        };
    }

    internal static EntityRole ToEntityRole(this EntityRoleDefinition roleDefinition, Guid entityId)
    {
        return new EntityRole()
        {
            Delete = roleDefinition.Delete,
            GetAll = roleDefinition.GetAll,
            GetOne = roleDefinition.GetOne,
            Post = roleDefinition.Post,
            Put = roleDefinition.Put,
            ViewFilter = roleDefinition.ViewFilter,
            EditFilter = roleDefinition.EditFilter,
            EntitiesEntityId = entityId,
            Role = new()
            {
                RoleName = roleDefinition.RoleKey,
            }
        };
    }

    internal static EntityRelation ToEntityRelation(this EntityRelationDefinition entityRelationDefinition, Guid entityId)
    {
        return new EntityRelation()
        {
            ActiveColumnName = entityRelationDefinition.ActiveColumnName,
            CrossTableName = entityRelationDefinition.CrossTableName,
            Entity1PropertyName = entityRelationDefinition.EntityPropertyName,
            Entity2PropertyName = entityRelationDefinition.RelatedEntityPropertyName,
            Entity1ReferencingColumnName = entityRelationDefinition.EntityReferencingColumnName,
            Entity2ReferencingColumnName = entityRelationDefinition.RelatedEntityReferencingColumnName,
            ShowInEntity1 = entityRelationDefinition.ShowInEntity,
            ShowInEntity2 = entityRelationDefinition.ShowInRelatedEntity,
            ValidFromColumnName = entityRelationDefinition.ValidFromColumnName,
            ValidToColumnName = entityRelationDefinition.ValidToColumnName,
            EntityRelationId = Guid.NewGuid(),
            Entity1Id = entityId,
            Entity2Id = entityIds[entityRelationDefinition.RelatedEntityKey]
        };
    }
}
