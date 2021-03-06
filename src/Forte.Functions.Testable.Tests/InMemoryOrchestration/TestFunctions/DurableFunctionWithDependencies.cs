using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Forte.Functions.Testable.Tests.InMemoryOrchestration.TestFunctions
{
    public class DurableFunctionWithDependencies
    {
        private readonly IFoo _foo;

        public DurableFunctionWithDependencies(IFoo foo)
        {
            _foo = foo;
        }

        [FunctionName(nameof(Function))]
        public async Task Function(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            Assert.IsNotNull(_foo);
        }
    }


    public interface IFoo
    {

    }

    public class Foo : IFoo
    {

    }
}