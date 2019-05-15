using System.Threading;

namespace Forte.Functions.Testable.Core
{
    internal class ExternalEventToken : CancellationTokenSource
    {
        public ExternalEventToken()
        {
        }

        public object Value { get; private set; }

        public void Notify(object value)
        {
            Value = value;
            Cancel(throwOnFirstException: false);
        }
    }
}