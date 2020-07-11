using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using System.Reflection;
using System.Runtime.InteropServices;
using System;
using System.Linq.Expressions;

public class ClonerGenerator : MonoBehaviour
{
    Expression CloneMember(Expression target, Expression source, Type type)
    {
        Expression output = null;

        if (type.IsValueType && Type.GetTypeCode(type) == TypeCode.Object)
        {
            output = Expression.Assign(source, target);
        }

        return output;
    }

    Action<object, object> GenerateCloner(Type type)
    {
        List<Expression> function = new List<Expression>(32);
        ParameterExpression source = Expression.Parameter(type);
        ParameterExpression target = Expression.Parameter(type);
        BlockExpression s;

        MemberInfo[] members = type.GetMembers(BindingFlags.GetProperty | BindingFlags.SetProperty | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        foreach (MemberInfo member in members)
        {
            if (member.MemberType != MemberTypes.Field && member.MemberType != MemberTypes.Property)
                continue;

            MemberExpression memberValue = Expression.PropertyOrField(target, member.Name);
            Type memberType = (member.MemberType == MemberTypes.Field ? (member as FieldInfo).FieldType : (member as PropertyInfo).PropertyType);

            Expression cloner = CloneMember(Expression.PropertyOrField(target, type.Name), Expression.PropertyOrField(source, type.Name), memberType);

            if (cloner != null)
                function.Add(cloner);
        }

        return Expression.Lambda<Action<object, object>>(Expression.Block(function), target, source).Compile();
    }
}
