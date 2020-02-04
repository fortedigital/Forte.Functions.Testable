using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

namespace Forte.Functions.Testable.Core
{
    internal class Invoker
    {
        private Assembly _assembly;
        private IServiceProvider _services;

        public Invoker(Assembly assembly, IServiceProvider services)
        {
            _assembly = assembly;
            _services = services;
        }


        public MethodInfo FindFunctionByName(string functionName)
        {
            return _assembly.GetExportedTypes()
                .SelectMany(type => type.GetMethods())
                .FirstOrDefault(method => method.GetCustomAttribute<FunctionNameAttribute>()?.Name == functionName);
        }



        public IEnumerable<object> ParametersForFunction(MethodInfo function, object context)
        {
            foreach (var parameter in function.GetParameters())
            {
                if (typeof(IDurableOrchestrationContext).IsAssignableFrom(parameter.ParameterType)
                    || typeof(IDurableEntityContext).IsAssignableFrom(parameter.ParameterType)
                    || typeof(IDurableActivityContext).IsAssignableFrom(parameter.ParameterType))
                {
                    yield return context;
                }
                else if (typeof(CancellationToken).IsAssignableFrom(parameter.ParameterType))
                {
                    yield return CancellationToken.None;
                }
                else
                {
                    yield return _services.GetService(parameter.ParameterType);
                }
            }
        }

        public async Task<dynamic> InvokeFunction(MethodInfo function, object instance, object[] parameters)
        {
            if (function.ReturnType.IsGenericType)
            {
                return await(dynamic)function.Invoke(instance, parameters);
            }
            else
            {
                await(dynamic)function.Invoke(instance, parameters);
                return default;
            }
        }
    }
}