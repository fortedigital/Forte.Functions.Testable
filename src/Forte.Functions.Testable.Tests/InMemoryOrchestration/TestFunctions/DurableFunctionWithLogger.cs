using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Forte.Functions.Testable.Tests.InMemoryOrchestration.TestFunctions
{
    public class DurableFunctionWithLogger
    {
        private readonly ILogger _logger;

        public DurableFunctionWithLogger(ILogger logger)
        {
            _logger = logger;
            Assert.IsNotNull(_logger, "Logger injected into constructor was null");
            _logger.LogInformation("Function class instantiated");
        }

        [FunctionName(nameof(LoggerFn))]
        public async Task LoggerFn(
            [OrchestrationTrigger] IDurableOrchestrationContext context, ILogger logger)
        {
            Assert.IsNotNull(logger, "Logger injected into function was null");

            _logger.LogInformation("Activity executing");
        }
    }
}