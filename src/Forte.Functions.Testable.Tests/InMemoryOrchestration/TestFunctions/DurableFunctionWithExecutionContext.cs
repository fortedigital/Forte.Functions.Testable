using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Forte.Functions.Testable.Tests.InMemoryOrchestration.TestFunctions
{
    public class DurableFunctionWithExecutionContext
    {

        [FunctionName(nameof(ExecutionContextFunction))]
        public async Task ExecutionContextFunction(
            [OrchestrationTrigger] IDurableOrchestrationContext context, 
            ExecutionContext ec)
        {
            Assert.IsNotNull(ec);
        }
    }
}