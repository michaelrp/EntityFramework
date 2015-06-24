// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using JetBrains.Annotations;
using Microsoft.Data.Entity.Metadata;
using Microsoft.Data.Entity.Utilities;

namespace Microsoft.Data.Entity.Internal
{
    public abstract class ModelValidator : IModelValidator
    {
        public virtual void Validate(IModel model)
        {
            EnsureNoShadowEntities(model);
            EnsureNoShadowKeys(model);
            EnsureNonNullPrimaryKeys(model);
            EnsureValidForeignKeyChains(model);
        }

        protected virtual void EnsureNoShadowEntities([NotNull] IModel model)
        {
            var firstShadowEntity = model.EntityTypes.FirstOrDefault(entityType => !entityType.HasClrType());
            if (firstShadowEntity != null)
            {
                ShowError(CoreStrings.ShadowEntity(firstShadowEntity.Name));
            }
        }

        protected virtual void EnsureNoShadowKeys([NotNull] IModel model)
        {
            foreach (var entityType in model.EntityTypes)
            {
                foreach (var key in entityType.GetKeys())
                {
                    if (key.Properties.Any(p => p.IsShadowProperty))
                    {
                        string message;
                        var referencingFk = model.FindReferencingForeignKeys(key).FirstOrDefault();
                        if (referencingFk != null)
                        {
                            message = CoreStrings.ReferencedShadowKey(
                                Property.Format(key.Properties),
                                entityType.Name,
                                Property.Format(key.Properties.Where(p => p.IsShadowProperty)),
                                Property.Format(referencingFk.Properties),
                                referencingFk.DeclaringEntityType.Name);
                        }
                        else
                        {
                            message = CoreStrings.ShadowKey(
                                Property.Format(key.Properties),
                                entityType.Name,
                                Property.Format(key.Properties.Where(p => p.IsShadowProperty)));
                        }

                        ShowWarning(message);
                    }
                }
            }
        }

        protected virtual void EnsureNonNullPrimaryKeys([NotNull] IModel model)
        {
            Check.NotNull(model, nameof(model));

            var entityTypeWithNullPk = model.EntityTypes.FirstOrDefault(et => et.FindPrimaryKey() == null);
            if (entityTypeWithNullPk != null)
            {
                ShowError(CoreStrings.EntityRequiresKey(entityTypeWithNullPk.Name));
            }
        }

        protected virtual void EnsureValidForeignKeyChains([NotNull] IModel model)
        {
            var verifiedProperties = new Dictionary<IProperty, IProperty>();
            foreach (var entityType in model.EntityTypes)
            {
                foreach (var foreignKey in entityType.GetForeignKeys())
                {
                    foreach (var property in foreignKey.Properties)
                    {
                        string errorMessage;
                        VerifyRootPrincipal(property, entityType, verifiedProperties, ImmutableList<IForeignKey>.Empty, out errorMessage);
                        if (errorMessage != null)
                        {
                            ShowError(errorMessage);
                        }
                    }
                }
            }
        }

        private IProperty VerifyRootPrincipal(
            IProperty keyProperty,
            IEntityType entityType,
            Dictionary<IProperty, IProperty> verifiedProperties,
            ImmutableList<IForeignKey> visitedForeignKeys,
            out string errorMessage)
        {
            errorMessage = null;
            IProperty rootPrincipal;
            if (verifiedProperties.TryGetValue(keyProperty, out rootPrincipal))
            {
                return rootPrincipal;
            }

            var rootPrincipals = new Dictionary<IProperty, IForeignKey>();
            foreach (var foreignKey in entityType.GetForeignKeys())
            {
                for (var index = 0; index < foreignKey.Properties.Count; index++)
                {
                    if (keyProperty == foreignKey.Properties[index])
                    {
                        var nextPrincipalProperty = foreignKey.PrincipalKey.Properties[index];
                        if (visitedForeignKeys.Contains(foreignKey))
                        {
                            var cycleStart = visitedForeignKeys.IndexOf(foreignKey);
                            var cycle = visitedForeignKeys.GetRange(cycleStart, visitedForeignKeys.Count - cycleStart);
                            errorMessage = CoreStrings.CircularDependency(cycle.Select(fk => fk.ToString()).Join());
                            continue;
                        }
                        rootPrincipal = VerifyRootPrincipal(nextPrincipalProperty, foreignKey.PrincipalEntityType, verifiedProperties, visitedForeignKeys.Add(foreignKey), out errorMessage);
                        if (rootPrincipal == null)
                        {
                            if (keyProperty.RequiresValueGenerator)
                            {
                                rootPrincipals[keyProperty] = foreignKey;
                            }
                            continue;
                        }

                        if (keyProperty.RequiresValueGenerator)
                        {
                            ShowError(CoreStrings.ForeignKeyValueGenerationOnAdd(
                                keyProperty.Name,
                                keyProperty.DeclaringEntityType.DisplayName(),
                                Property.Format(foreignKey.Properties)));
                            return keyProperty;
                        }

                        rootPrincipals[rootPrincipal] = foreignKey;
                    }
                }
            }

            if (rootPrincipals.Count == 0)
            {
                if (errorMessage != null)
                {
                    return null;
                }

                if (!keyProperty.RequiresValueGenerator)
                {
                    ShowError(CoreStrings.PrincipalKeyNoValueGenerationOnAdd(keyProperty.Name, keyProperty.DeclaringEntityType.DisplayName()));
                    return null;
                }

                return keyProperty;
            }

            if (rootPrincipals.Count > 1)
            {
                var firstRoot = rootPrincipals.Keys.ElementAt(0);
                var secondRoot = rootPrincipals.Keys.ElementAt(1);
                ShowWarning(CoreStrings.MultipleRootPrincipals(
                    rootPrincipals[firstRoot].DeclaringEntityType.DisplayName(),
                    Property.Format(rootPrincipals[firstRoot].Properties),
                    firstRoot.DeclaringEntityType.DisplayName(),
                    firstRoot.Name,
                    Property.Format(rootPrincipals[secondRoot].Properties),
                    secondRoot.DeclaringEntityType.DisplayName(),
                    secondRoot.Name));

                return firstRoot;
            }

            errorMessage = null;
            rootPrincipal = rootPrincipals.Keys.Single();
            verifiedProperties[keyProperty] = rootPrincipal;
            return rootPrincipal;
        }

        protected virtual void ShowError([NotNull] string message)
        {
            throw new InvalidOperationException(message);
        }

        protected abstract void ShowWarning([NotNull] string message);
    }
}
