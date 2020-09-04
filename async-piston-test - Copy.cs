/*
Unfortunately this does not work, Task, TaskCompletionSource, and INotifyCompletion are prohibited by the Programmable Block.
*/
IMyExtendedPistonBase piston1 = null;
IMyExtendedPistonBase piston2 = null;
List<AsyncOp> queue = new List<AsyncOp>();
int count = 0;

public Program()
{
    // piston1 = FilterBlocks<IMyExtendedPistonBase>(p => p.CustomName == "Piston Async 1");
    // piston2 = FilterBlocks<IMyExtendedPistonBase>(p => p.CustomName == "Piston Async 2");

    // piston1.Enabled = false;
    // piston2.Enabled = false;
    // piston1.Velocity = 1;
    // piston2.Velocity = 1;

    // Runtime.UpdateFrequency = UpdateFrequency.Update100;
}

public void Main(string argument, UpdateType updateSource)
{
    if (argument.ToUpper() == "START") {
        DoStart();
        return;
    }

    // TODO: if because of Update100:
    ProcessQueue();
}

async void DoStart() {
    // Echo("Extending piston 1");
    // await ExtendAsync(piston1, 10);
    // Echo("Extending piston 2");
    // await ExtendAsync(piston2, 10);
    // Echo("Done");

    Echo("DoStart starting");
    await DoSomethingAsync();
    Echo("DoStart done");
}

AsyncOp DoSomethingAsync() {
    var asyncOp = new AsyncOp(() => {
        return (++count >= 3);
    });
    queue.Add(asyncOp);
    return asyncOp;
}

// AsyncOp ExtendAsync(IMyExtendedPistonBase piston, float length) {
//     // piston.Extend();
//     // piston.Enabled = true;

//     piston.MinLimit = length;
//     piston.MaxLimit = length;
//     if (piston.CurrentPosition > length) {
//         piston.Retract();
//     } else {
//         piston.Extend();
//     }
//     piston.Enabled = true;


//     // TODO: ...

//     // var tcs = new TaskCompletionSource<Object>();
//     // tcs.SetResult(null);
//     // return tcs.Task;

//     var asyncOp = new AsyncOp(() => {
//         piston.Extend();
//     });
//     queue.Add(promise);
//     return promise;
// }

private void ProcessQueue() {
    Echo("Processing the queue...");
    int i = 0;
    while (i < queue.Count) {
        var queueItem = queue[i];
        if (queueItem.IsCompleted) {
            Echo($"Queued item {i} is done...");
            queue.RemoveAt(i);
        } else {
            Echo($"Queued item {i} is NOT done...");
            i++;
        }
    }
}

public List<T> FilterBlocks<T>(Func<T, Boolean> filter = null) where T : class, IMyTerminalBlock
{
    var blocks = new List<T>();
    GridTerminalSystem.GetBlocksOfType(blocks, x => {
        if (!x.IsSameConstructAs(Me)) return false;
        return (filter == null) || filter(x);
    });
    return blocks.ConvertAll(x => (T)x);
}

// DOESN'T WORK: Use of INotifyCompletion is prohibited
public class AsyncOp : System.Runtime.CompilerServices.INotifyCompletion {
    private Func<bool> isDone;
    private Action continuation = null;
    public bool IsCompleted {
        get {
            if (isDone()) {
                continuation?.Invoke();
                return true;
            }
            return false;
        }
    }

    public AsyncOp(Func<bool> isDone) {
        this.isDone = isDone;
    }

    public AsyncOp GetAwaiter() {
        return this;
    }

    public void GetResult () {}

    public void OnCompleted(Action continuation) {
        this.continuation = continuation;
        // Echo("Continuation set");
    }
}
