using Microsoft.Azure.WebJobs;
using Newtonsoft.Json.Linq;

namespace Forte.Functions.Testable
{
    public class InMemoryActivityContext : DurableActivityContextBase, IInMemoryContextInput
    {
        private readonly DurableOrchestrationContextBase _parentContext;

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

        public InMemoryActivityContext(DurableOrchestrationContextBase parentContext, object input)
        {
            _parentContext = parentContext;
            Input = input;
        }
        public override string InstanceId => _parentContext.InstanceId;

        public override T GetInput<T>()
        {
            return _serializedInput.ToObject<T>();
        }
    }
}
