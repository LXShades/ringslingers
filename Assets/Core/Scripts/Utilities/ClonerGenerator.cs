using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using System.Reflection;
using System.Runtime.InteropServices;
using System;
using System.Linq.Expressions;
using System.Linq;

public static class ClonerGenerator
{
    private static Dictionary<string, Action<object, object>> clonerByType = new Dictionary<string, Action<object, object>>();

    private static Dictionary<string, string> clonerInfoByType = new Dictionary<string, string>();

    public static Expression CloneMember(Expression target, Expression source, Type type)
    {
        Expression output = null;

        if (type.IsValueType && Type.GetTypeCode(type) == TypeCode.Object)
        {
            output = Expression.Assign(target, source);
        }

        return output;
    }

    public static Action<object, object> GenerateCloner(Type type)
    {
        List<Expression> function = new List<Expression>(32);
        List<string> affectedVariables = new List<string>(32);
        ParameterExpression sourceGeneric = Expression.Parameter(typeof(object));
        ParameterExpression targetGeneric = Expression.Parameter(typeof(object));
        ParameterExpression source = Expression.Variable(type);
        ParameterExpression target = Expression.Variable(type);

        function.Add(Expression.Assign(source, Expression.Convert(sourceGeneric, type)));
        function.Add(Expression.Assign(target, Expression.Convert(targetGeneric, type)));

        MemberInfo[] members = type.GetMembers(BindingFlags.GetProperty | BindingFlags.SetProperty | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        foreach (MemberInfo member in members)
        {
            if (member.MemberType != MemberTypes.Field && member.MemberType != MemberTypes.Property)
                continue;

            Type memberType = (member.MemberType == MemberTypes.Field ? (member as FieldInfo).FieldType : (member as PropertyInfo).PropertyType);

            Expression cloner = CloneMember(Expression.PropertyOrField(target, member.Name), Expression.PropertyOrField(source, member.Name), memberType);

            if (cloner != null)
            {
                function.Add(cloner);
                affectedVariables.Add(member.Name);
            }
        }

        function.Add(Expression.Assign(
            Expression.PropertyOrField(Expression.PropertyOrField(target, "transform"), "position"),
            Expression.PropertyOrField(Expression.PropertyOrField(source, "transform"), "position")
            )
        );
        function.Add(Expression.Assign(
            Expression.PropertyOrField(Expression.PropertyOrField(target, "transform"), "rotation"),
            Expression.PropertyOrField(Expression.PropertyOrField(source, "transform"), "rotation")
            )
        );

        clonerInfoByType[type.Name] = string.Join(", ", affectedVariables);

        return Expression.Lambda<Action<object, object>>(Expression.Block(new[] { source, target }, function), targetGeneric, sourceGeneric).Compile();
    }

    public static Action<object, object> GetOrCreateCloner(Type type)
    {
        Action<object, object> cloner;

        if (!clonerByType.TryGetValue(type.Name, out cloner))
        {
            cloner = GenerateCloner(type);
            clonerByType.Add(type.Name, cloner);

            Debug.Log($"{type.Name} cloner info: {clonerInfoByType[type.Name]}");
        }

        return cloner;
    }
}
