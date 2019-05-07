using System.Threading.Tasks;
using Forte.Functions.Testable.Tests.InMemoryOrchestration.TestFunctions;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.DependencyInjection;
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
    }
}