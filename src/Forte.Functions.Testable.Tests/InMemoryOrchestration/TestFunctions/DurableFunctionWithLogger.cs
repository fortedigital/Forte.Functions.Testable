using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Forte.Functions.Testable.Tests.InMemoryOrchestration.TestFunctions
{
    public class DurableFunctionWithLogger
    {
        
        public DurableFunctionWithLogger(ILoggerFactory loggerFactory)
        {
            Assert.IsNotNull(loggerFactory, "Logger injected into constructor was null");
            loggerFactory.CreateLogger(nameof(DurableFunctionWithLogger)).LogInformation("Function class instantiated");
        }

        [FunctionName(nameof(LoggerFn))]
        public async Task LoggerFn(
            [OrchestrationTrigger] IDurableOrchestrationContext context, ILogger logger)
        {
            Assert.IsNotNull(logger, "Logger injected into function was null");

            logger.LogInformation("Activity executing");
        }
    }
}