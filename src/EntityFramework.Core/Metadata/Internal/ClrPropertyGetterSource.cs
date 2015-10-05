// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Reflection;

namespace Microsoft.Data.Entity.Metadata.Internal
{
    public class ClrPropertyGetterSource : ClrAccessorSource<IClrPropertyGetter>
    {
        protected override IClrPropertyGetter CreateGeneric<TEntity, TValue, TNonNullableEnumValue>(PropertyInfo property)
            => new ClrPropertyGetter<TEntity, TValue>(
                (Func<TEntity, TValue>)property.GetMethod.CreateDelegate(typeof(Func<TEntity, TValue>)));

        protected override IClrPropertyGetter NetNativeCreate(PropertyInfo property)
        {
            var tEntity = property.DeclaringType;
            var tValue = property.PropertyType;
            var getterType = typeof(ClrPropertyGetter<,>).MakeGenericType(tEntity, tValue);
            var funcType = typeof(Func<,>).MakeGenericType(tEntity, tValue);
            return (IClrPropertyGetter)Activator.CreateInstance(getterType, property.GetMethod.CreateDelegate(funcType));
        }
    }
}
