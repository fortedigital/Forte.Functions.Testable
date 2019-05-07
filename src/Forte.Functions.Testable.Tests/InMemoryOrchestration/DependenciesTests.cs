using System;
using System.Threading.Tasks;
using Forte.Functions.Testable.Tests.InMemoryOrchestration.TestFunctions;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Forte.Functions.Testable.Tests.InMemoryOrchestration
{
    [TestClass]
    public class DependenciesTests
    {
        [TestMethod]
        public async Task Durable_function_class_can_have_dependencies()
        {
            var services = new ServiceCollection();
            services.AddTransient<IFoo, Foo>();

            var client = new InMemoryOrchestrationClient(typeof(Funcs).Assembly, services.BuildServiceProvider());

            var instanceId = await client.StartNewAsync(nameof(DurableFunctionWithDependencies.Function), null);

            await client.WaitForOrchestrationToReachStatus(instanceId, OrchestrationRuntimeStatus.Completed);

            var status = await client.GetStatusAsync(instanceId);

            Assert.AreEqual(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
        }


        [TestMethod]
        public async Task Activity_can_inject_logger()
        {
            var services = new ServiceCollection();
            services.AddTransient<ILogger, ConsoleWriteLineLogger>();

            var client = new InMemoryOrchestrationClient(typeof(Funcs).Assembly, services.BuildServiceProvider());

            var instanceId = await client.StartNewAsync(nameof(DurableFunctionWithLogger.LoggerFn), null);

            await client.WaitForOrchestrationToReachStatus(instanceId, OrchestrationRuntimeStatus.Completed);

            var status = await client.GetStatusAsync(instanceId);

            Assert.AreEqual(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
        }
    }

    public class ConsoleWriteLineLogger : ILogger, IDisposable
    {
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            Console.WriteLine(formatter(state,exception));
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            return this;
        }

        public void Dispose()
        {   
        }
    }
}