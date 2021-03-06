using System;
using System.Threading.Tasks;
using Forte.Functions.Testable.Tests.InMemoryOrchestration.TestFunctions;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace Forte.Functions.Testable.Tests.InMemoryOrchestration
{
    [TestClass]
    public class CompleteOrchestrationTests
    {
        private IServiceProvider _services = new ServiceCollection().BuildServiceProvider();

        [TestMethod]
        public async Task Can_execute_durable_function()
        {
            var input = new TestFunctionInput();

            var client = new InMemoryOrchestrationClient(typeof(Funcs).Assembly, _services);
            var instanceId = await client
                .StartNewAsync(nameof(Funcs.DurableFunctionWithOneActivity), input);
            
            try
            {
                await client.WaitForOrchestrationToReachStatus(instanceId, OrchestrationRuntimeStatus.Completed);

                var status = await client.GetStatusAsync(instanceId);

                Assert.AreEqual(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
            }
            finally
            {
                var status = await client.GetStatusAsync(instanceId);
                TestUtil.LogHistory(status, Console.Out);
            }
        }

        [TestMethod]
        public async Task Can_execute_durable_function_async()
        {
            var input = new TestFunctionInputAsync();

            var client = new InMemoryOrchestrationClient(typeof(Funcs).Assembly, _services);
            var instanceId = await client
                .StartNewAsync(nameof(Funcs.DurableFunctionWithOneActivityAsync), input);

            await client.WaitForOrchestrationToReachStatus(instanceId, OrchestrationRuntimeStatus.Completed);

            var status = await client.GetStatusAsync(instanceId);

            TestUtil.LogHistory(status, Console.Out);
            Assert.AreEqual(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
        }

        [TestMethod]
        public async Task Can_WaitForOrchestrationToFinish_when_success()
        {
            var input = new TestFunctionInputAsync();

            var client = new InMemoryOrchestrationClient(typeof(Funcs).Assembly, _services);
            var instanceId = await client
                .StartNewAsync(nameof(Funcs.DurableFunctionWithOneActivityAsync), input);

            var status = await client.WaitForOrchestrationToFinish(instanceId);

            Assert.AreEqual(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
        }

        [TestMethod]
        public async Task Can_WaitForOrchestrationToFinish_when_failed()
        {
            var input = new TestFunctionInputAsync();

            var client = new InMemoryOrchestrationClient(typeof(Funcs).Assembly, _services);
            var instanceId = await client
                .StartNewAsync(nameof(Funcs.FailAtGivenCallNoActivity), input);

            var status = await client.WaitForOrchestrationToFinish(instanceId);

            Assert.AreEqual(OrchestrationRuntimeStatus.Failed, status.RuntimeStatus);
        }

        [TestMethod]
        public async Task Can_execute_durable_function_with_return()
        {
            var input = new TestFunctionInput();

            var client = new InMemoryOrchestrationClient(typeof(Funcs).Assembly, _services);
            var instanceId = await client
                .StartNewAsync(nameof(Funcs.DurableFunctionWithOneActivityReturn), input);

            await client.WaitForOrchestrationToReachStatus(instanceId, OrchestrationRuntimeStatus.Completed);

            var status = await client.GetStatusAsync(instanceId);

            TestUtil.LogHistory(status, Console.Out);
            Assert.AreEqual(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
        }

        [TestMethod]
        public async Task Can_execute_durable_function_async_with_return()
        {
            var input = new TestFunctionInputAsync();

            var client = new InMemoryOrchestrationClient(typeof(Funcs).Assembly, _services);
            var instanceId = await client
                .StartNewAsync(nameof(Funcs.DurableFunctionWithOneActivityAsyncReturn), input);
          
            await client.WaitForOrchestrationToReachStatus(instanceId, OrchestrationRuntimeStatus.Completed);

            var status = await client.GetStatusAsync(instanceId);

            TestUtil.LogHistory(status, Console.Out);
            Assert.AreEqual(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
        }
      
        [TestMethod]
        public async Task Can_execute_direct_bound_durable_activities()
        {
            var client = new InMemoryOrchestrationClient(typeof(Funcs).Assembly, _services);
            var instanceId = await client
                .StartNewAsync(nameof(Funcs.DurableFunctionWithDirectBinding), null);

            await client.WaitForOrchestrationToReachStatus(instanceId, OrchestrationRuntimeStatus.Completed);

            var status = await client.GetStatusAsync(instanceId);

            TestUtil.LogHistory(status, Console.Out);
            Assert.AreEqual(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
        }

        [TestMethod]
        public async Task Can_execute_durable_function_with_output()
        {
            var input = new TestFunctionInput();

            var client = new InMemoryOrchestrationClient(typeof(Funcs).Assembly, _services);
            var instanceId = await client
                .StartNewAsync(nameof(Funcs.DurableFunctionWithOutput), input);

            await client.WaitForOrchestrationToReachStatus(instanceId, OrchestrationRuntimeStatus.Completed);

            var status = await client.GetStatusAsync(instanceId);

            TestUtil.LogHistory(status, Console.Out);
            Assert.AreEqual(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
            Assert.AreEqual(JToken.FromObject("OK"), status.Output);
        }

        [TestMethod]
        public async Task Activity_can_redefine_input_without_leaking_into_orchestrator_context()
        {
            var startInput = new TestFunctionInput{Token = "original"};

            var client = new InMemoryOrchestrationClient(typeof(Funcs).Assembly, _services);
            var instanceId = await client
                .StartNewAsync(nameof(Funcs.DurableFunctionWithSeparateActivityInput), startInput);

            await client.WaitForOrchestrationToReachStatus(instanceId, OrchestrationRuntimeStatus.Completed);

            var status = await client.GetStatusAsync(instanceId);

            var endInput = status.Input.ToObject<TestFunctionInput>();

            TestUtil.LogHistory(status, Console.Out);
            Assert.AreEqual(startInput.Token, endInput.Token, "Activity redefining input should not leak into orchestrator");
        }

        [TestMethod]
        public async Task Functions_cannot_mutate_their_input_source()
        {
            var startInput = new TestFunctionInput{Token = "original"};

            var client = new InMemoryOrchestrationClient(typeof(Funcs).Assembly, _services);
            var instanceId = await client
                .StartNewAsync(nameof(Funcs.DurableFunctionWithMutatedInput), startInput);

            await client.WaitForOrchestrationToReachStatus(instanceId, OrchestrationRuntimeStatus.Completed);

            var status = await client.GetStatusAsync(instanceId);

            var endInput = status.Input.ToObject<TestFunctionInput>();

            TestUtil.LogHistory(status, Console.Out);
            Assert.AreEqual(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);

            Assert.AreEqual(startInput.Token, endInput.Token, "Functions mutating inputs should not mutate their source");
        }
    }
}
