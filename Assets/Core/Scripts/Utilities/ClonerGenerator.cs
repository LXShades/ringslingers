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

    public static Expression CloneMember(Expression target, Expression source, Expression targetWorld, Expression sourceWorld, Type type)
    {
        Expression output = null;

        if (type.IsValueType)
        {
            output = Expression.Assign(target, source);
        }
        else if (type == typeof(WorldObject) && sourceWorld != null && targetWorld != null)
        {
            output = Expression.Assign(target, 
                Expression.Condition(
                    Expression.Call(typeof(UnityEngine.Object).GetMethod("op_Inequality"), source, Expression.Constant(null, typeof(WorldObject))),
                    Expression.Call(targetWorld, typeof(World).GetMethod("FindEquivalentWorldObject"), source),
                    Expression.Constant(null, typeof(WorldObject))
                )
            );
        }
        else if (type.IsSubclassOf(typeof(WorldObjectComponent)) && sourceWorld != null && targetWorld != null)
        {
            output = Expression.Assign(target,
                Expression.Condition(
                    Expression.Call(typeof(UnityEngine.Object).GetMethod("op_Inequality"), source, Expression.Constant(null, type)),
                    Expression.Convert(
                        Expression.Call(targetWorld, typeof(World).GetMethod("FindEquivalentWorldObjectComponent"), source),
                        type
                    ),
                    Expression.Constant(null, type)
                )
            );
        }
        else if (type == typeof(GameObject) && sourceWorld != null && targetWorld != null)
        {
            // If the GameObject reference has a WorldObject, get its WorldObject in its new world
            ParameterExpression sourceAsWorldObject = Expression.Variable(typeof(WorldObject));
            ParameterExpression targetAsWorldObject = Expression.Variable(typeof(WorldObject));

            output = Expression.Block(
                new[] { sourceAsWorldObject, targetAsWorldObject },
                Expression.Assign(
                    sourceAsWorldObject,
                    Expression.Condition(
                        Expression.Call(typeof(UnityEngine.Object).GetMethod("op_Inequality"), source, Expression.Constant(null, typeof(GameObject))),
                        Expression.Call(source, typeof(GameObject).GetMethod("GetComponent", new Type[0]).MakeGenericMethod(typeof(WorldObject))),
                        Expression.Constant(null, typeof(WorldObject))
                    )
                ), // worldObject = (source != null ? source.GetComponent<WorldObject>() : null)
                Expression.Assign(targetAsWorldObject,
                    Expression.Condition(
                        Expression.NotEqual(sourceAsWorldObject, Expression.Constant(null, typeof(WorldObject))),
                        Expression.Call(targetWorld, typeof(World).GetMethod("FindEquivalentWorldObject"), sourceAsWorldObject),
                        Expression.Constant(null, typeof(WorldObject))
                    )
                ), // targetAsWorldObject = worldObject != null ? target.world.GetObjectById(worldObject.objId) : null
                Expression.IfThenElse(
                    Expression.NotEqual(targetAsWorldObject, Expression.Constant(null, typeof(WorldObject))),
                    Expression.Assign(target, Expression.PropertyOrField(targetAsWorldObject, "gameObject")),
                    Expression.Assign(target, source)
                ) // if (targetAsWorldObject != null) target = targetAsWorldObject.gameObject else target = source
            );
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
        Expression sourceWorld = null;
        Expression targetWorld = null;

        function.Add(Expression.Assign(source, Expression.Convert(sourceGeneric, type)));
        function.Add(Expression.Assign(target, Expression.Convert(targetGeneric, type)));

        if (type.IsSubclassOf(typeof(WorldObjectComponent)))
        {
            sourceWorld = Expression.PropertyOrField(Expression.PropertyOrField(source, "worldObject"), "world");
            targetWorld = Expression.PropertyOrField(Expression.PropertyOrField(target, "worldObject"), "world");
        }

        MemberInfo[] members = type.GetMembers(BindingFlags.GetProperty | BindingFlags.SetProperty | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        foreach (MemberInfo member in members)
        {
            if (member.MemberType != MemberTypes.Field && member.MemberType != MemberTypes.Property)
                continue;

            Type memberType = (member.MemberType == MemberTypes.Field ? (member as FieldInfo).FieldType : (member as PropertyInfo).PropertyType);

            Expression cloner = CloneMember(Expression.PropertyOrField(target, member.Name), Expression.PropertyOrField(source, member.Name), targetWorld, sourceWorld, memberType);

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
        }

        return cloner;
    }

    public static string GetClonerInfo(Type type)
    {
        if (!clonerByType.ContainsKey(type.Name))
        {
            GetOrCreateCloner(type);
        }

        return clonerInfoByType[type.Name];
    }
}
