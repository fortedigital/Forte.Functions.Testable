using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Text;
using System.Threading.Tasks;
using Forte.Functions.Testable.Tests.InMemoryOrchestration.TestFunctions;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Forte.Functions.Testable.Tests
{
    [TestClass]
    public class EntityTests
    {
        private readonly IServiceProvider _services = new ServiceCollection().BuildServiceProvider();

        [TestMethod]
        public async Task Can_wait_for_entity_operation()
        {
            var client = new InMemoryOrchestrationClient(typeof(Entity).Assembly, _services);

            var entityId = new EntityId("Entity", "1");
            await client.SignalEntityAsync(entityId, nameof(Entity.SetStateOperation));

            await client.WaitForEntityOperation(entityId, nameof(Entity.SetStateOperation));

            var state = await client.ReadEntityStateAsync<bool>(entityId);

            Assert.AreEqual(true, state.EntityState);
        }

        [TestMethod]
        public async Task Can_wait_for_entity_destruction()
        {
            var client = new InMemoryOrchestrationClient(typeof(Entity).Assembly, _services);

            var entityId = new EntityId("Entity", "1");
            await client.SignalEntityAsync(entityId, nameof(Entity.DestructOperation));

            var state = await client.ReadEntityStateAsync<bool>(entityId);
            Assert.AreEqual(true, state.EntityExists);

            await client.WaitForEntityDestruction(entityId);

            var state2 = await client.ReadEntityStateAsync<bool>(entityId);

            Assert.AreEqual(false, state2.EntityExists);
        }

        [TestMethod]
        public async Task Can_wait_for_entity_state_change()
        {
            var client = new InMemoryOrchestrationClient(typeof(Entity).Assembly, _services);

            var entityId = new EntityId("Entity", "1");
            await client.SignalEntityAsync(entityId, nameof(Entity.SetStateOperation));

            await client.WaitForNextStateChange(entityId);

            var state = await client.ReadEntityStateAsync<bool>(entityId);

            Assert.AreEqual(true, state.EntityState);
        }
    }
}
