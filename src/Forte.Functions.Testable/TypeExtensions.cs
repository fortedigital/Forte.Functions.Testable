using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Forte.Functions.Testable
{
    public class AwaitableInfo
    {
        /// <summary>
        /// True when an instance of the type can be awaited, such as with the await keyword.
        /// </summary>
        public bool IsAwaitable { get; }

        /// <summary>
        /// The expected type resulting from the await operation.
        /// </summary>
        public Type ResultType { get; }

        public AwaitableInfo(bool isAwaitable, Type resultType)
        {
            IsAwaitable = isAwaitable;
            ResultType = resultType;
        }
    }

    public static class TypeExtensions
    {
        /// <summary>
        /// Returns information about a type describing its ability to be awaited (such as using the await keyword).
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static AwaitableInfo GetAwaitableInfo(this Type type)
        {
            AwaitableInfo GetInfoShallow(MethodInfo getAwaiterMethod)
            {
                if (getAwaiterMethod == null)
                {
                    return new AwaitableInfo(false, typeof(void));
                }

                var awaiterResultType = getAwaiterMethod.ReturnType.GetAwaiterResultType();

                return awaiterResultType == null
                    ? new AwaitableInfo(false, typeof(void))
                    : new AwaitableInfo(true, awaiterResultType);

            }

            var typeInfo = GetInfoShallow(type.GetMethod("GetAwaiter", BindingFlags.Public | BindingFlags.Instance));

            if (typeInfo.IsAwaitable)
            {
                return typeInfo;
            }

            return GetInfoShallow(type.GetExtensionMethods("GetAwaiter").FirstOrDefault());
        }

        /// <summary>
        /// Assuming that the type is an awaiter, returns the type that would be produced by awaiting this awaiter.
        /// Returns null when the type is not an awaiter.
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        private static Type GetAwaiterResultType(this Type type)
        {
            var isCompletedProperty = type.GetProperty("IsCompleted", BindingFlags.Public | BindingFlags.Instance);
            var getResultMethod = type.GetMethod("GetResult", BindingFlags.Public | BindingFlags.Instance);

            var isAwaiter = typeof(INotifyCompletion).IsAssignableFrom(type) &&
                isCompletedProperty?.CanRead == true && isCompletedProperty.PropertyType == typeof(bool) &&
                getResultMethod?.ReturnType != null;

            return isAwaiter
                ? getResultMethod.ReturnType
                : null;
        }

        /// <summary>
        /// Returns all extension methods of a given type with a given name.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="methodName"></param>
        /// <returns></returns>
        private static IEnumerable<MethodInfo> GetExtensionMethods(this Type type, string methodName)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .Where(t => t.IsSealed && !t.IsGenericType && !t.IsNested)
                .Select(t => t.GetMethod(methodName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                .Where(t =>
                    t != null &&
                    t.IsDefined(typeof(ExtensionAttribute), false)
                )
                .Where(t =>
                {
                    var thisParameterType = t.GetParameters()[0].ParameterType;

                    return thisParameterType.IsGenericType
                        ? thisParameterType == type.GetGenericTypeDefinition()
                        : thisParameterType == type;
                });
        }
    }
}
