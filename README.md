# Forte.Functions.Testable
In-memory orchestration for testing Durable Functions

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

where MyFunction may look like this:

```c#
public partial class MyFunction
{
    [FunctionName(nameof(DurableFunction))]
    public static async Task DurableFunction(
        [OrchestrationTrigger] DurableOrchestrationContextBase context)
    {
        var input = context.GetInput<MyFunctionInput>();
        await context.CallActivityAsync(nameof(SomeActivity), input);
    }

    [FunctionName(nameof(SomeActivity))]
    public static Task SomeActivity([ActivityTrigger] DurableOrchestrationContextBase context)
    {
        return Task.CompletedTask;
    }
}
```

The InMemoryOrchestrationClient supports most features of durable functions including activities, retries, sub-orchestrations, external events etc.

Todo:

- Publish on Nuget
- Support other function arguments like ILogger etc.
- Support instance functions
- Support DI
- ...
