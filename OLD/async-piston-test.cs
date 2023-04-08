/*
This works, creating Promises ou
*/
IMyExtendedPistonBase piston1 = null;
IMyExtendedPistonBase piston2 = null;
// List<AsyncOp> queue = new List<AsyncOp>();

public Program()
{
    piston1 = FilterBlocks<IMyExtendedPistonBase>(p => p.CustomName == "Piston 1").First();
    piston2 = FilterBlocks<IMyExtendedPistonBase>(p => p.CustomName == "Piston 2").First();
    piston1.Enabled = false;
    piston2.Enabled = false;
    piston1.Velocity = 1;
    piston2.Velocity = 1;

    Runtime.UpdateFrequency |= UpdateFrequency.Once;
    Runtime.UpdateFrequency |= UpdateFrequency.Update100;
}

public void Main(string argument, UpdateType updateSource)
{
    // if (argument.ToUpper() == "START") {
    //     DoStart();
    //     return;
    // }

    if ((updateSource & UpdateType.Once) > 0) {
        DoStart();
    }
    if ((updateSource & UpdateType.Update100) > 0) {
        ProcessQueue();
    }
}

void DoStart() {
    // Echo("Extending piston 1");
    // await ExtendAsync(piston1, 10);
    // Echo("Extending piston 2");
    // await ExtendAsync(piston2, 10);
    // Echo("Done");

    Echo("Before promise chain");
    MyPromise.create(() => ExtendAsync(piston1))
        .then(() => ExtendAsync(piston2))
        .done();
    Echo("After promise chain");
}

IEnumerator<bool> ExtendAsync(IMyExtendedPistonBase piston) {
    return WrapAsEnumerator(() => {
        Echo($"{piston.CustomName} Extend()");

        float length = 10;

        piston.MinLimit = length;
        piston.MaxLimit = length;
        if (piston.CurrentPosition > length) {
            piston.Retract();
        } else {
            piston.Extend();
        }
        piston.Enabled = true;
    }, () => piston.CurrentPosition == piston.MinLimit);
}

IEnumerator<bool> WrapAsEnumerator(Action func, Func<bool> isDone) {
    func();
    for (;;) {
        if (isDone()) {
            yield break;
        } else {
            yield return true;
        }
    }
}

// IEnumerator<bool> DoSomethingElseAsync() {
//     Echo("DoSomethingElseAsync");
//     int count = 0;
//     for (;;) {
//         if (++count >= 1) { // simulate it taking a while, only done after repeated attempts
//             yield break;
//         } else {
//             yield return false;
//         }
//     }
// }

private void ProcessQueue() {
    Echo("Processing the queue...");
    // int i = 0;
    // while (i < queue.Count) {
    //     var queueItem = queue[i];
    //     if (queueItem.IsCompleted) {
    //         Echo($"Queued item {i} is done...");
    //         queue.RemoveAt(i);
    //     } else {
    //         Echo($"Queued item {i} is NOT done...");
    //         i++;
    //     }
    // }
    Queue.ProcessQueue();
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

public class MyPromise {
    private IEnumerator<bool> enumerator;
    MyPromise previousPromise = null;
    MyPromise nextPromise = null;

    public static MyPromise create(Func<IEnumerator<bool>> func) {
        return new MyPromise(func);
    }

    public MyPromise(Func<IEnumerator<bool>> func) {
        enumerator = func();
    }

    public MyPromise(Func<IEnumerator<bool>> func, MyPromise previousPromise) : this(func) {
        this.previousPromise = previousPromise;
    }

    public MyPromise then(Func<IEnumerator<bool>> func) {
        nextPromise = new MyPromise(func, this);
        return nextPromise;
    }

    public void done() {
        if (previousPromise != null) {
            previousPromise.done();
        } else {
            run();
        }
    }

    public void run() {
        // Queue.Add(enumerator, () => nextPromise?.run());
        Queue.Add(this);
    }

    public bool hasFinished() {
        return !enumerator.MoveNext(); // true means it's still working, false that it's done
    }

    public void onFinished() {
        nextPromise?.run();
    }
}

public class Queue {
    private static List<MyPromise> queue = new List<MyPromise>();

    public static void Add(MyPromise promise) {
        queue.Add(promise);
    }

    public static void ProcessQueue() {
        // Echo("Processing the queue...");
        int i = 0;
        while (i < queue.Count) {
            var promise = queue[i];

            if (promise.hasFinished()) {
                // Echo($"Queued item {i} is DONE...");
                queue.RemoveAt(i);
                // promise.then();
                promise.onFinished();
            } else {
                // Echo($"Queued item {i} is NOT done...");
                i++;
            }
        }
    }
}
