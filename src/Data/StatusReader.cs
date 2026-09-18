using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ModInfoPanel.Data
{
    /// <summary>按“状态类字段”读取玩家当前状态值（CUCoreLib BodyStatus / 组件 / Body 字段）。</summary>
    internal static class StatusReader
    {
        private static readonly Dictionary<string, Type> Types = new Dictionary<string, Type>(StringComparer.Ordinal);
        private static readonly Dictionary<string, FieldInfo> Fields = new Dictionary<string, FieldInfo>(StringComparer.Ordinal);
        private static readonly Dictionary<string, MethodInfo> Getters = new Dictionary<string, MethodInfo>(StringComparer.Ordinal);

        public static bool TryRead(string typeFullName, string fieldName, out float value)
        {
            value = 0f;
            if (string.IsNullOrWhiteSpace(typeFullName) || string.IsNullOrWhiteSpace(fieldName))
            {
                return false;
            }

            try
            {
                Type type = ResolveType(typeFullName);
                if (type == null)
                {
                    return false;
                }

                object target = ResolveTarget(type);
                if (target == null)
                {
                    return false;
                }

                FieldInfo field = ResolveField(type, fieldName);
                if (field == null)
                {
                    return false;
                }

                object raw = field.GetValue(target);
                if (raw == null)
                {
                    return false;
                }

                value = Convert.ToSingle(raw);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static Type ResolveType(string fullName)
        {
            if (Types.TryGetValue(fullName, out Type cached))
            {
                return cached;
            }

            Type found = null;
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    found = assembly.GetType(fullName, throwOnError: false);
                }
                catch
                {
                    found = null;
                }

                if (found != null)
                {
                    break;
                }
            }

            Types[fullName] = found;
            return found;
        }

        private static FieldInfo ResolveField(Type type, string fieldName)
        {
            string key = (type.FullName ?? type.Name) + "|" + fieldName;
            if (Fields.TryGetValue(key, out FieldInfo cached))
            {
                return cached;
            }

            FieldInfo field = null;
            for (Type current = type; current != null && field == null; current = current.BaseType)
            {
                field = current.GetField(fieldName,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            }

            Fields[key] = field;
            return field;
        }

        private static object ResolveTarget(Type type)
        {
            Body body = null;
            try
            {
                if (PlayerCamera.main != null)
                {
                    body = PlayerCamera.main.body;
                }
            }
            catch
            {
            }

            if (body == null)
            {
                return null;
            }

            if (IsBodyStatus(type))
            {
                MethodInfo getter = GetStatusGetter(type);
                if (getter != null)
                {
                    try
                    {
                        return getter.Invoke(null, new object[] { body });
                    }
                    catch
                    {
                    }
                }

                return null;
            }

            if (typeof(Component).IsAssignableFrom(type))
            {
                try
                {
                    return body.GetComponent(type);
                }
                catch
                {
                }
            }

            if (type == typeof(Body))
            {
                return body;
            }

            return null;
        }

        private static bool IsBodyStatus(Type type)
        {
            for (Type current = type?.BaseType; current != null; current = current.BaseType)
            {
                if (string.Equals(current.FullName, "CUCoreLib.Data.BodyStatus", StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static MethodInfo GetStatusGetter(Type statusType)
        {
            string key = statusType.FullName ?? statusType.Name;
            if (Getters.TryGetValue(key, out MethodInfo cached))
            {
                return cached;
            }

            MethodInfo result = null;
            try
            {
                Type extensions = null;
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        extensions = assembly.GetType("CUCoreLib.Helpers.StatusExtensions", throwOnError: false);
                    }
                    catch
                    {
                        extensions = null;
                    }

                    if (extensions != null)
                    {
                        break;
                    }
                }

                if (extensions != null)
                {
                    foreach (MethodInfo method in extensions.GetMethods(
                                 BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
                    {
                        if (method.Name != "GetStatus" || !method.IsGenericMethodDefinition)
                        {
                            continue;
                        }

                        ParameterInfo[] parameters = method.GetParameters();
                        if (parameters.Length != 1 || parameters[0].ParameterType != typeof(Body))
                        {
                            continue;
                        }

                        if (method.GetGenericArguments().Length != 1)
                        {
                            continue;
                        }

                        result = method.MakeGenericMethod(statusType);
                        break;
                    }
                }
            }
            catch
            {
                result = null;
            }

            Getters[key] = result;
            return result;
        }
    }
}
