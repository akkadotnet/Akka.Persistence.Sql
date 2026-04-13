# TestKit implicit sender broken with xUnit v3 IAsyncLifetime due to [ThreadStatic] usage

## Describe the bug

`TestKitBase` stores the implicit sender (TestActor's cell) in `InternalCurrentActorCellKeeper.Current`, which is a `[ThreadStatic]` field. It also sets an `ActorCellKeepingSynchronizationContext` to preserve the value across `await` continuations.

xUnit v3 replaces the `SynchronizationContext` with its own (`MaxConcurrencySyncContext`) for test parallelism management. This means:

1. The `ActorCellKeepingSynchronizationContext` set in the TestKit constructor is overridden
2. `[ThreadStatic]` values don't flow when xUnit v3 runs `IAsyncLifetime.InitializeAsync()` continuations or test methods on different thread pool threads
3. `ActorRefImplicitSenderExtensions.Tell(receiver, message)` resolves the sender as `NoSender` (deadLetters) instead of the TestActor

This causes **all** TCK query tests (and any test using implicit sender via `actor.Tell(msg)`) to time out, because responses go to dead letters instead of the TestActor.

## Reproduction

Any test class that:
1. Inherits from `Akka.TestKit.Xunit.TestKit` (or `PluginSpec`, or any TCK spec base class)
2. Implements `IAsyncLifetime`
3. Has test methods that rely on implicit sender (`actor.Tell(msg)` without explicit sender)

```csharp
public class MySpec : Akka.TestKit.Xunit.TestKit, IAsyncLifetime
{
    public MySpec(ITestOutputHelper output) : base(config, output: output) { }

    public async ValueTask InitializeAsync()
    {
        // Any await here can cause a thread switch
        await SomeAsyncSetup();
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public void Test_using_implicit_sender()
    {
        var actor = Sys.ActorOf(Props.Create<EchoActor>());
        
        // This uses ActorRefImplicitSenderExtensions.Tell which calls
        // ActorCell.GetCurrentSelfOrNoSender() — returns NoSender because
        // [ThreadStatic] InternalCurrentActorCellKeeper.Current is null
        // on this thread
        actor.Tell("hello");
        
        // Times out — response went to deadLetters, not TestActor
        ExpectMsg("hello");
    }
}
```

### Observed log output

```
[INFO] Message [String] from [akka://test/user/$a] to [akka://test/deadLetters] was not delivered.
Message content: hello
```

## Root cause

In `TestKitBase.InitializeAndJoin()`:

```csharp
_testState.TestActor = val4;
if (!(this is INoImplicitSender))
{
    InternalCurrentActorCellKeeper.Current = (ActorCell)((ActorRefWithCell)val4).Underlying;
}
SynchronizationContext.SetSynchronizationContext(
    new ActorCellKeepingSynchronizationContext(InternalCurrentActorCellKeeper.Current));
```

`InternalCurrentActorCellKeeper` uses `[ThreadStatic]`:

```csharp
public static class InternalCurrentActorCellKeeper
{
    [ThreadStatic]
    private static ActorCell? _current;

    public static ActorCell? Current
    {
        get => _current;
        set => _current = value;
    }
}
```

`[ThreadStatic]` does not flow across threads. When xUnit v3 (or any framework) runs the test method on a different thread than the constructor, the implicit sender is lost.

The `ActorCellKeepingSynchronizationContext` was designed to work around this by restoring the cell on async continuations, but xUnit v3 replaces it with its own `SynchronizationContext`.

## Proposed fix

Replace `[ThreadStatic]` with `AsyncLocal<ActorCell>` in `InternalCurrentActorCellKeeper`:

```csharp
public static class InternalCurrentActorCellKeeper
{
    private static readonly AsyncLocal<ActorCell?> _current = new();

    public static ActorCell? Current
    {
        get => _current.Value;
        set => _current.Value = value;
    }
}
```

`AsyncLocal<T>` flows across:
- `await` continuations (regardless of `SynchronizationContext`)
- `Task.Run` and `ThreadPool.QueueUserWorkItem`
- `ExecutionContext` flow in general

This would make the implicit sender work correctly with any test framework's threading model without depending on `SynchronizationContext` cooperation.

### Impact assessment

- `AsyncLocal` has slightly higher overhead than `[ThreadStatic]` but this is negligible for a test-time utility
- `AsyncLocal` flows into child tasks by default, which matches the expected behavior (tests spawning background work should retain the implicit sender context)
- The `ActorCellKeepingSynchronizationContext` could be simplified or removed as it would no longer be needed for this purpose
- Actor message dispatch code that sets/restores `Current` during `Receive` would continue to work identically

## Current workarounds

1. **Avoid `IAsyncLifetime`** on TestKit-derived classes — move async initialization into the constructor with synchronous `.Wait()`. This keeps everything on the constructor thread where `[ThreadStatic]` is set.

2. **Use explicit sender** everywhere: `actor.Tell(msg, TestActor)` instead of `actor.Tell(msg)`. Not viable when test methods come from upstream packages like `Akka.Persistence.TCK`.

## Environment

- Akka.NET: 1.5.64
- Akka.TestKit.Xunit: 1.5.64
- xUnit: v3.2.2
- .NET: 8.0
