using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;

namespace Forte.Functions.Testable.Tests.InMemoryOrchestration.TestFunctions
{
    public static class Entity
    {
        [FunctionName(nameof(SetStateOperation))]
        public static async Task SetStateOperation(
            [EntityTrigger] IDurableEntityContext ctx)
        {
            ctx.SetState(true);
        }

        [FunctionName(nameof(DestructOperation))]
        public static async Task DestructOperation(
            [EntityTrigger] IDurableEntityContext ctx)
        {
            ctx.DestructOnExit();
        }
    }
}