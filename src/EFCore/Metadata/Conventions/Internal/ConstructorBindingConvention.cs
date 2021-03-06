// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace Microsoft.EntityFrameworkCore.Metadata.Conventions.Internal
{
    /// <summary>
    ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
    ///     directly from your code. This API may change or be removed in future releases.
    /// </summary>
    public class ConstructorBindingConvention : IModelBuiltConvention
    {
        private readonly IConstructorBindingFactory _bindingFactory;

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public ConstructorBindingConvention([NotNull] IConstructorBindingFactory bindingFactory)
            => _bindingFactory = bindingFactory;

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public virtual InternalModelBuilder Apply(InternalModelBuilder modelBuilder)
        {
            foreach (var entityType in modelBuilder.Metadata.GetEntityTypes())
            {
                if (entityType.ClrType != null
                    && !entityType.ClrType.IsAbstract)
                {
                    var maxServiceParams = 0;
                    var minPropertyParams = int.MaxValue;
                    var foundBindings = new List<ConstructorBinding>();
                    var bindingFailures = new List<IEnumerable<ParameterInfo>>();

                    foreach (var constructor in entityType.ClrType.GetTypeInfo()
                        .DeclaredConstructors
                        .Where(c => !c.IsStatic))
                    {
                        // Trying to find the constructor with the most service properties
                        // followed by the least scalar property parameters
                        if (_bindingFactory.TryBindConstructor(entityType, constructor, out var binding, out var failures))
                        {
                            var serviceParamCount = binding.ParameterBindings.OfType<ServiceParameterBinding>().Count();
                            var propertyParamCount = binding.ParameterBindings.Count - serviceParamCount;

                            if (serviceParamCount == maxServiceParams
                                && propertyParamCount == minPropertyParams)
                            {
                                foundBindings.Add(binding);
                            }
                            else if (serviceParamCount > maxServiceParams)
                            {
                                foundBindings.Clear();
                                foundBindings.Add(binding);

                                maxServiceParams = serviceParamCount;
                                minPropertyParams = propertyParamCount;
                            }
                            else if (propertyParamCount < minPropertyParams)
                            {
                                foundBindings.Clear();
                                foundBindings.Add(binding);

                                maxServiceParams = serviceParamCount;
                                minPropertyParams = propertyParamCount;
                            }
                        }
                        else
                        {
                            bindingFailures.Add(failures);
                        }
                    }

                    if (foundBindings.Count == 0)
                    {
                        throw new InvalidOperationException(
                            CoreStrings.ConstructorNotFound(
                                entityType.DisplayName(),
                                string.Join("', '", bindingFailures.SelectMany(f => f).Select(f => f.Name))));
                    }

                    if (foundBindings.Count > 1)
                    {
                        throw new InvalidOperationException(
                            CoreStrings.ConstructorConflict(
                                FormatConstructorString(entityType, foundBindings[0]),
                                FormatConstructorString(entityType, foundBindings[1])));
                    }

                    entityType.Builder.HasAnnotation(
                        CoreAnnotationNames.ConstructorBinding,
                        foundBindings[0],
                        ConfigurationSource.Convention);
                }
            }

            //foreach (var entityType in modelBuilder.Metadata.GetEntityTypes())
            //{
            //    if (entityType.ClrType != null
            //        && !entityType.ClrType.IsAbstract)
            //    {
            //        var foundBinding = (ConstructorBinding)null;
            //        var bindingFailures = new List<IEnumerable<ParameterInfo>>();

            //        foreach (var constructor in entityType.ClrType.GetTypeInfo()
            //            .DeclaredConstructors
            //            .Where(c => !c.IsStatic)
            //            .OrderByDescending(c => c.GetParameters().Length))
            //        {
            //            var parameterCount = constructor.GetParameters().Length;

            //            if (foundBinding != null
            //                && foundBinding.ParameterBindings.Count != parameterCount)
            //            {
            //                break;
            //            }

            //            if (_bindingFactory.TryBindConstructor(entityType, constructor, out var binding, out var failures))
            //            {
            //                if (foundBinding?.ParameterBindings.Count == parameterCount)
            //                {
            //                    throw new InvalidOperationException(
            //                        CoreStrings.ConstructorConflict(
            //                            FormatConstructorString(entityType, foundBinding),
            //                            FormatConstructorString(entityType, binding)));
            //                }

            //                foundBinding = binding;
            //            }

            //            bindingFailures.Add(failures);
            //        }

            //        if (foundBinding == null)
            //        {
            //            throw new InvalidOperationException(
            //                CoreStrings.ConstructorNotFound(
            //                    entityType.DisplayName(),
            //                    string.Join("', '", bindingFailures.SelectMany(f => f).Select(f => f.Name))));
            //        }

            //        entityType.Builder.HasAnnotation(
            //            CoreAnnotationNames.ConstructorBinding,
            //            foundBinding,
            //            ConfigurationSource.Convention);
            //    }
            //}

            return modelBuilder;
        }

        private static string FormatConstructorString(EntityType entityType, ConstructorBinding binding)
            => entityType.DisplayName() + "(" + string.Join(", ", binding.ParameterBindings.Select(b => b.ParameterType.ShortDisplayName())) + ")";
    }
}
