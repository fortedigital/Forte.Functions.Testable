# Forte.Functions.Testable
In-memory orchestration for testing Durable Functions

[![Build Status](https://fortedigital.visualstudio.com/Forte.OpenSource/_apis/build/status/Forte.Functions.Testable?branchName=master)](https://fortedigital.visualstudio.com/Forte.OpenSource/_build/latest?definitionId=102&branchName=master)

By leveraging DurableOrchestrationClientBase, this project implements InMemoryOrchestrationClient which allows durable functions to be executed in-memory and observed with no mocking. For example, you could write a test such as:

```c#
[TestMethod]
public async Task Can_execute_durable_function()
{
    var input = new MyFunctionInput();

    var client = new InMemoryOrchestrationClient(typeof(MyFunction).Assembly);
    var instanceId = await client
        .StartNewAsync(nameof(MyFunction.DurableFunction), input);

    await client.WaitForOrchestrationToReachStatus(instanceId, OrchestrationRuntimeStatus.Completed);

    var status = await client.GetStatusAsync(instanceId);

    Assert.AreEqual(OrchestrationRuntimeStatus.Completed, status.RuntimeStatus);
}
```

Tests can use the `WaitForOrchestrationToReachStatus`, `WaitForOrchestrationToExpectEvent`, `RaiseEventAsync` and `Timeshift` methods on the orchestration client to direct the durable function.

Contrast this with the approach suggested in https://docs.microsoft.com/en-us/azure/azure-functions/durable/durable-functions-unit-testing which requires a lot of brittle mocking/setup to accomplish this.


The implementation is currently a proof-of-concept and supports most features of durable functions including activities, retries, sub-orchestrations, external events etc. The major difference is of course that this orchestration client does not have the replay-behavior of durable functions, but simply executes the durable function in-memory with async/await-like behavior. 
