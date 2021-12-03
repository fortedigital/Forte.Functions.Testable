using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Newtonsoft.Json.Linq;

namespace Forte.Functions.Testable
{
    public class InMemoryActivityContext : IDurableActivityContext, IInMemoryContextInput
    {
        private readonly IDurableOrchestrationContext _parentContext;

        private object _input;
        private JToken _serializedInput;

        public object Input
        {
            get => _input;
            set
            {
                _input = value;
                _serializedInput = value == null
                    ? JValue.CreateNull()
                    : JToken.FromObject(value);
            }
        }

        public InMemoryActivityContext(IDurableOrchestrationContext parentContext, object input, string name = null)
        {
            Name = name;
            _parentContext = parentContext;
            Input = input;
        }
        public string InstanceId => _parentContext.InstanceId;

        public T GetInput<T>()
        {
            return _serializedInput.ToObject<T>();
        }

        public string Name { get; }
    }
}
