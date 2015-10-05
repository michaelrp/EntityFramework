// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Reflection;

namespace Microsoft.Data.Entity.Metadata.Internal
{
    public class ClrPropertySetterSource : ClrAccessorSource<IClrPropertySetter>
    {
        protected override IClrPropertySetter CreateGeneric<TEntity, TValue, TNonNullableEnumValue>(PropertyInfo property)
        {
            // TODO: Handle case where there is not setter or setter is private on a base type
            // Issue #753
            var setterDelegate = (Action<TEntity, TValue>)property.SetMethod.CreateDelegate(typeof(Action<TEntity, TValue>));

            return (property.PropertyType.IsNullableType()
                    && property.PropertyType.UnwrapNullableType().GetTypeInfo().IsEnum) ?
                new NullableEnumClrPropertySetter<TEntity, TValue, TNonNullableEnumValue>(setterDelegate) :
                (IClrPropertySetter)new ClrPropertySetter<TEntity, TValue>(setterDelegate);
        }

        protected override IClrPropertySetter NetNativeCreate(PropertyInfo property)
        {
            var tEntity = property.DeclaringType;
            var tValue = property.PropertyType;

            var actionType = typeof(Action<,>).MakeGenericType(tEntity, tValue);

            // TODO: Handle case where there is not setter or setter is private on a base type
            // Issue #753
            var setterDelegate = property.SetMethod.CreateDelegate(actionType);

            Type setterType;
            if (property.PropertyType.IsNullableType()
                    && property.PropertyType.UnwrapNullableType().GetTypeInfo().IsEnum)
            {
                var tNonNullableEnumValue = property.PropertyType.UnwrapNullableType();
                setterType = typeof(NullableEnumClrPropertySetter<,,>).MakeGenericType(tEntity, tValue, tNonNullableEnumValue);
            }
            else
            {
                setterType = typeof(ClrPropertySetter<,>).MakeGenericType(tEntity, tValue);
            }

            return (IClrPropertySetter)Activator.CreateInstance(setterType, setterDelegate);
        }
    }
}
