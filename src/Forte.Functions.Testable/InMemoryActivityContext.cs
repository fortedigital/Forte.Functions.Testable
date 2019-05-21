using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;

namespace Forte.Functions.Testable
{
    public class InMemoryActivityContext : DurableActivityContextBase
    {
        private readonly DurableOrchestrationContextBase _parentContext;
        private readonly object _input;

        public InMemoryActivityContext(DurableOrchestrationContextBase parentContext, object input)
        {
            _parentContext = parentContext;
            _input = input;
        }

        public override T GetInput<T>()
        {
            return (T)_input;
        }
    }
}