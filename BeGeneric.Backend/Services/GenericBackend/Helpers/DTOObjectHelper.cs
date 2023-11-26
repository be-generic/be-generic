﻿using BeGeneric.Backend.Settings;
using System.Reflection.Emit;
using System.Reflection;
using BeGeneric.Helpers;
using BeGeneric.Backend.Services.BeGeneric.DatabaseStructure;

namespace BeGeneric.Backend.Services.GenericBackend.Helpers
{
    internal static class DTOObjectHelper
    {
        internal static Type BuildDTOObject(List<EntityDefinition> entities, EntityDefinition entity, IDatabaseStructureService structureService, Dictionary<string, Type> generatedTypes, bool isPost = false)
        {
            string typeName = $@"{(isPost ? "POST_" : "")}{(string.IsNullOrEmpty(entity.ObjectName) ? entity.TableName.TitleCaseOriginalName() : entity.ObjectName.TitleCaseOriginalName())}DTO";

            if (generatedTypes.ContainsKey(typeName))
            {
                return generatedTypes[typeName];
            }

            TypeBuilder typeBuilder = GetTypeBuilder(typeName);

            typeBuilder.DefineDefaultConstructor(MethodAttributes.Public);
            
            foreach (var property in entity.Properties.Where(x => (!(x.IsKey ?? false) || !isPost) && !(x.IsHidden ?? false)))
            {
                if (!string.IsNullOrEmpty(property.DefaultValue) && isPost)
                {
                    continue;
                }

                if (string.IsNullOrEmpty(property.ReferencingEntityKey))
                {
                    var dataType = structureService.GetFieldType(property.PropertyName, entity.TableName);
                    DefineProperty(typeBuilder, entity, property, dataType);
                }
                else
                {
                    var relatedEntity = entities.First(x => x.EntityKey == property.ReferencingEntityKey);
                    string relatedTypeName = $@"{(string.IsNullOrEmpty(relatedEntity.ObjectName) ? relatedEntity.TableName.TitleCaseOriginalName() : relatedEntity.ObjectName.TitleCaseOriginalName())}DTO";

                    if (!generatedTypes.ContainsKey(relatedTypeName))
                    {
                        _ = BuildDTOObject(entities, relatedEntity, structureService, generatedTypes);
                    }

                    DefineProperty(typeBuilder, entity, property, generatedTypes[relatedTypeName]);
                }
            }

            var type = typeBuilder.CreateType();
            
            generatedTypes.Add(typeName, type);

            return type;
        }

        private static TypeBuilder GetTypeBuilder(string typeName)
        {
            var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(Guid.NewGuid().ToString()), AssemblyBuilderAccess.Run);
            var moduleBuilder = assemblyBuilder.DefineDynamicModule("BeGenericAutogeneratedModule");
            var typeBuilder = moduleBuilder.DefineType(typeName,
                TypeAttributes.Public |
                TypeAttributes.Class |
                TypeAttributes.AutoClass |
                TypeAttributes.AnsiClass |
                TypeAttributes.BeforeFieldInit |
                TypeAttributes.AutoLayout,
                null);

            return typeBuilder;
        }

        private static void DefineProperty(TypeBuilder typeBuilder, EntityDefinition entity, PropertyDefinition property, Type dataType)
        {
            string propertyName = string.IsNullOrEmpty(property.ModelPropertyName) ? property.PropertyName.TitleCaseOriginalName() : property.ModelPropertyName.TitleCaseOriginalName();

            PropertyBuilder propertyBuilder = typeBuilder.DefineProperty(propertyName, PropertyAttributes.HasDefault, dataType, null);
            MethodBuilder getPropMthdBldr = typeBuilder.DefineMethod("get_" + propertyName, MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig, dataType, Type.EmptyTypes);
            ILGenerator getIl = getPropMthdBldr.GetILGenerator();

            FieldBuilder fieldBuilder = typeBuilder.DefineField("_" + propertyName, dataType, FieldAttributes.Private);

            getIl.Emit(OpCodes.Ldarg_0);
            getIl.Emit(OpCodes.Ldfld, fieldBuilder);
            getIl.Emit(OpCodes.Ret);

            MethodBuilder setPropMthdBldr =
                typeBuilder.DefineMethod("set_" + propertyName,
                  MethodAttributes.Public |
                  MethodAttributes.SpecialName |
                  MethodAttributes.HideBySig,
                  null, new[] { dataType });

            ILGenerator setIl = setPropMthdBldr.GetILGenerator();
            Label modifyProperty = setIl.DefineLabel();
            Label exitSet = setIl.DefineLabel();

            setIl.MarkLabel(modifyProperty);
            setIl.Emit(OpCodes.Ldarg_0);
            setIl.Emit(OpCodes.Ldarg_1);
            setIl.Emit(OpCodes.Stfld, fieldBuilder);

            setIl.Emit(OpCodes.Nop);
            setIl.MarkLabel(exitSet);
            setIl.Emit(OpCodes.Ret);

            propertyBuilder.SetGetMethod(getPropMthdBldr);
            propertyBuilder.SetSetMethod(setPropMthdBldr);
        }
    }
}
