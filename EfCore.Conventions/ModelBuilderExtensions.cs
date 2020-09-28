using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using EfCore.Conventions.Attributes;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Microsoft.EntityFrameworkCore
{
    public static class ModelBuilderExtensions
    {
        public static ModelBuilder WithTableName(this ModelBuilder builder, Func<IMutableEntityType, string> func)
        {
            foreach (var type in builder.Model.GetEntityTypes())
            {
                type.SetTableName(func(type));
            }

            return builder;
        }

        private static ModelBuilder ApplyAction(this ModelBuilder builder, Action<IMutableEntityType> action)
        {
            foreach (var type in builder.Model.GetEntityTypes())
            {
                if (type.ClrType.GetCustomAttribute<NotMappedAttribute>() != null)
                {
                    continue;
                }

                action(type);
            }

            return builder;
        }

        private static ModelBuilder ApplyActionEachProperty(this ModelBuilder builder, Action<IMutableEntityType, PropertyInfo> action)
        {
            return builder.ApplyAction(type =>
            {
                foreach (var property in type.ClrType.GetProperties())
                {
                    if (property.GetCustomAttribute<NotMappedAttribute>() != null)
                    {
                        continue;
                    }

                    action(type, property);
                }
            });
        }

        public static ModelBuilder AddNavigationConventions(this ModelBuilder builder, params string[] conventions)
        {
            return builder.ApplyAction(type =>
            {
                var keys = type.FindPrimaryKey()?.Properties
                    .Select(it => it.Name)
                    .ToList();
                if (keys == null || keys.Count == 0)
                    return;
                if (keys.Count == 1 && string.Equals(keys[0], "id", StringComparison.OrdinalIgnoreCase))
                    return;
                foreach (var nav in type.GetNavigations())
                {
                    var fk = nav.ForeignKey;
                    if (fk.PrincipalEntityType != type || fk.PrincipalToDependent == null)
                        continue;
                    if (fk.Properties.Any(it => it.PropertyInfo != null))
                        continue;

                    bool addedFk = false;
                    var navigationPropertyName =
                        fk.DependentToPrincipal?.PropertyInfo.Name ?? "[navigationPropertyName]";
                    foreach (var convention in conventions)
                    {
                        if (addedFk)
                            break;
                        var fks = keys
                            .Select(key =>
                                convention.Replace("[principalEntityName]", type.ClrType.Name)
                                    .Replace("[principalPropertyName]", key)
                                    .Replace("[navigationPropertyName]", navigationPropertyName))
                            .ToArray();

                        if (fks.Except(fk.DeclaringEntityType.GetProperties().Select(it => it.Name)).Any())
                            continue;
                        if (fk.PrincipalToDependent.IsCollection())
                        {
                            addedFk = true;
                            builder.Entity(type.ClrType).HasMany(fk.PrincipalToDependent.Name)
                                .WithOne()
                                .HasForeignKey(fks);
                        }
                        else
                        {
                            builder.Entity(type.ClrType).HasOne(fk.PrincipalToDependent.Name)
                                .WithOne()
                                .HasForeignKey(fk.DeclaringEntityType.Name, fks);
                        }
                    }
                }

            });
        }

        public static ModelBuilder WithExtendedAttributes(this ModelBuilder builder)
        {
            return builder.ApplyAction(type =>
            {
                //composite keys
                var keys = (from it in type.ClrType.GetProperties()
                            where it.GetCustomAttribute<CompositeKeyAttribute>() != null
                            orderby it.GetCustomAttribute<ColumnAttribute>()?.Order ?? 0
                            select it.Name).ToArray();
                if (keys.Length > 1)
                {
                    builder.Entity(type.ClrType).HasKey(keys);
                }

                //indexes
                var indexes = from it in type.ClrType.GetProperties()
                              from index in it.GetCustomAttributes<IndexAttribute>()
                              orderby index.Name, index.Order
                              group new { it.Name, index } by index.Name ?? Guid.NewGuid().ToString();
                foreach (var index in indexes)
                {
                    var indexBuilder = builder.Entity(type.ClrType).HasIndex(index.Select(it => it.Name).ToArray());
                    var first = index.First();
                    if (first.index.Name != null)
                    {
                        indexBuilder = indexBuilder.HasName(first.Name);
                    }
                    if (first.index.IsUnique)
                    {
                        indexBuilder.IsUnique();
                    }
                }

                //on delete
                foreach (var foreignKey in type.GetForeignKeys())
                {
                    if (foreignKey.DeclaringEntityType != type)
                    {
                        continue;
                    }

                    var onDelete = foreignKey.DependentToPrincipal?.PropertyInfo?.GetCustomAttribute<OnDeleteAttribute>();
                    if (onDelete != null)
                    {
                        foreignKey.DeleteBehavior = onDelete.Behavior;
                    }
                }

                foreach (var property in type.GetProperties())
                {
                    //default value
                    var defaultValue = property.PropertyInfo?.GetCustomAttribute<DefaultValueAttribute>();
                    if (defaultValue != null)
                    {
                        property.SetDefaultValue(defaultValue.Value);
                    }
                }
            });
        }

        public static ModelBuilder ForPropertyType<T>(this ModelBuilder builder, Action<PropertyBuilder<T>> action)
        {
            return builder.ApplyActionEachProperty((type, property) =>
            {
                if (property.PropertyType != typeof(T))
                {
                    return;
                }
                var p = Expression.Parameter(typeof(ModelBuilder));
                var entity = GetEntity(p, type.ClrType);
                var propBuilder = GetProperty(entity, property.Name);
                var con = Expression.Constant(action);
                var invoke = Expression.Invoke(con, propBuilder);
                Expression.Lambda<Action<ModelBuilder>>(invoke, p).Compile()(builder);
            });
        }

        public static ModelBuilder SetDefaultTypeNameForType<T>(this ModelBuilder builder, string typeName)
        {
            return builder.ApplyActionEachProperty((type, property) =>
            {
                if (property.PropertyType != typeof(T) && Nullable.GetUnderlyingType(property.PropertyType) != typeof(T))
                {
                    return;
                }
                if (property.GetCustomAttribute<ColumnAttribute>()?.TypeName != null)
                {
                    return;
                }

                var typeBuilder = builder.Entity(type.ClrType);
                var propBuilder = typeBuilder.Property(property.Name);
                propBuilder.HasColumnType(typeName);
            });
        }

        public static ModelBuilder ForProperty(this ModelBuilder builder, Func<PropertyInfo, bool> predicate, Action<PropertyBuilder> action)
        {
            return builder.ApplyActionEachProperty((type, property) =>
            {
                if (!predicate(property))
                {
                    return;
                }

                var typeBuilder = builder.Entity(type.ClrType);
                var propBuilder = typeBuilder.Property(property.Name);
                action(propBuilder);
            });
        }

        public static ModelBuilder ForProperty(this ModelBuilder builder, Func<PropertyInfo, bool> predicate, Action<PropertyBuilder, PropertyInfo> action)
        {
            return builder.ApplyActionEachProperty((type, property) =>
            {
                if (!predicate(property))
                {
                    return;
                }

                var typeBuilder = builder.Entity(type.ClrType);
                var propBuilder = typeBuilder.Property(property.Name);
                action(propBuilder, property);
            });
        }

        private static Expression GetEntity(Expression modelBuilder, Type type)
        {
            var method = modelBuilder.Type.GetMethods().First(it => it.Name == "Entity" && it.IsGenericMethodDefinition);
            return Expression.Call(modelBuilder, method.MakeGenericMethod(type));
        }

        private static Expression GetProperty(Expression entityBuilder, string name)
        {
            var type = entityBuilder.Type.GetGenericArguments()[0];
            var p = Expression.Parameter(type);
            var prop = Expression.Property(p, name);
            var lambda = Expression.Lambda(prop, p);
            var quote = Expression.Quote(lambda);
            var method = entityBuilder.Type.GetMethods()
                .First(it => it.Name == "Property" && it.IsGenericMethodDefinition);
            return Expression.Call(entityBuilder, method.MakeGenericMethod(prop.Type), quote);
        }
    }
}