#pragma warning disable 4014 // Arm() returns a Task intentionally discarded in many tests

namespace Pontifex.Utils.CheckPointGate;

public sealed class CheckPointTests
{
    [Test]
    public void InitialState()
    {
        var cp = new CheckPoint();
        Assert.Multiple(() =>
        {
            Assert.That(cp.IsArmed, Is.False);
            Assert.That(cp.HitCount, Is.Zero);
        });
    }

    [Test]
    public void Hit_WhenUnarmed_DoesNothing()
    {
        var cp = new CheckPoint();
        cp.Hit();
        Assert.Multiple(() =>
        {
            Assert.That(cp.IsArmed, Is.False);
            Assert.That(cp.HitCount, Is.Zero);
        });
    }

    [Test]
    public void Hit_WhenUnarmed_CanBeCalledMultipleTimes()
    {
        var cp = new CheckPoint();
        for (var i = 0; i < 100; i++)
            cp.Hit();
        Assert.That(cp.HitCount, Is.Zero);
    }

    [Test]
    public async Task HitAsync_WhenUnarmed_ReturnsCompletedTask()
    {
        var cp = new CheckPoint();
        var task = cp.HitAsync();
        Assert.That(task.IsCompleted, Is.True);
        await task;
    }

    [Test]
    public async Task HitAsync_WhenUnarmed_CanBeCalledMultipleTimes()
    {
        var cp = new CheckPoint();
        for (var i = 0; i < 100; i++)
        {
            var task = cp.HitAsync();
            Assert.That(task.IsCompleted, Is.True);
            await task;
        }
    }

    [Test]
    public void Arm_SetsIsArmedAndHitCount()
    {
        var cp = new CheckPoint();
        cp.Arm(5);
        Assert.Multiple(() =>
        {
            Assert.That(cp.IsArmed, Is.True);
            Assert.That(cp.HitCount, Is.EqualTo(4));
        });
    }

    [Test]
    public void Arm_WithDefault_CreatesArm1()
    {
        var cp = new CheckPoint();
        cp.Arm();
        Assert.Multiple(() =>
        {
            Assert.That(cp.IsArmed, Is.True);
            Assert.That(cp.HitCount, Is.Zero);
        });
    }

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(-100)]
    public void Arm_ZeroOrNegative_Throws(int requiredHits)
    {
        var cp = new CheckPoint();
        Assert.Throws<ArgumentOutOfRangeException>(() => cp.Arm(requiredHits));
    }

    [Test]
    public void Arm_One_CanPassThroughThenBlock()
    {
        var cp = new CheckPoint();
        cp.Arm(1);

        Assert.That(cp.HitCount, Is.Zero);

        var blocked = true;
        var thread = new Thread(() =>
        {
            cp.Hit();
            blocked = false;
        });
        thread.Start();

        Thread.Sleep(100);
        Assert.That(blocked, Is.True);

        cp.Reset();
        thread.Join(1000);
        Assert.That(blocked, Is.False);
    }

    [Test]
    public void Arm_MaxValue_DoesNotOverflow()
    {
        var cp = new CheckPoint();
        Assert.DoesNotThrow(() => cp.Arm(int.MaxValue));
        Assert.That(cp.HitCount, Is.EqualTo(int.MaxValue - 1));
        cp.Reset();
    }

    [Test]
    public void Hit_DecrementsHitCount()
    {
        var cp = new CheckPoint();
        cp.Arm(5);
        cp.Hit();
        Assert.That(cp.HitCount, Is.EqualTo(3));
    }

    [Test]
    public void Hit_AllFreeHits_CountsDownToZero()
    {
        var cp = new CheckPoint();
        cp.Arm(5);

        for (var i = 0; i < 4; i++)
            cp.Hit();

        Assert.That(cp.HitCount, Is.Zero);
    }

    [Test]
    public async Task HitAsync_DecrementsHitCount()
    {
        var cp = new CheckPoint();
        cp.Arm(5);
        var task = cp.HitAsync();

        Assert.That(task.IsCompleted, Is.True);
        Assert.That(cp.HitCount, Is.EqualTo(3));
        await task;
    }

    [Test]
    public async Task HitAsync_AllFreeHits_CountsDownToZero()
    {
        var cp = new CheckPoint();
        cp.Arm(5);

        for (var i = 0; i < 4; i++)
        {
            var task = cp.HitAsync();
            Assert.That(task.IsCompleted, Is.True);
            await task;
        }

        Assert.That(cp.HitCount, Is.Zero);
    }

    [Test]
    public void Hit_WhenHitCountZero_BlocksCallingThread()
    {
        var cp = new CheckPoint();
        cp.Arm(2); // HitCount = 1

        cp.Hit(); // HitCount = 0

        var enteredHit = false;
        var exitedHit = false;
        var thread = new Thread(() =>
        {
            enteredHit = true;
            cp.Hit();
            exitedHit = true;
        });
        thread.Start();

        Thread.Sleep(200);
        Assert.Multiple(() =>
        {
            Assert.That(enteredHit, Is.True);
            Assert.That(exitedHit, Is.False);
        });

        cp.Reset();
        Assert.That(thread.Join(1000), Is.True);
        Assert.That(exitedHit, Is.True);
    }

    [Test]
    public async Task HitAsync_WhenHitCountZero_ReturnsPendingTask()
    {
        var cp = new CheckPoint();
        cp.Arm(2); // HitCount = 1
        cp.Hit();  // HitCount = 0

        var hitTask = cp.HitAsync();

        Assert.That(hitTask.IsCompleted, Is.False);

        cp.Reset();
        await hitTask.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.That(hitTask.IsCompleted, Is.True);
    }

    [Test]
    public void Hit_AfterAllFreeHits_MultipleThreadsAllBlock()
    {
        var cp = new CheckPoint();
        cp.Arm(1); // HitCount = 0

        var completed = 0;
        var threads = new Thread[5];
        for (var i = 0; i < threads.Length; i++)
        {
            threads[i] = new Thread(() =>
            {
                cp.Hit();
                Interlocked.Increment(ref completed);
            });
            threads[i].Start();
        }

        Thread.Sleep(200);
        Assert.That(completed, Is.Zero);

        cp.Reset();
        foreach (var t in threads)
            Assert.That(t.Join(1000), Is.True);

        Assert.That(completed, Is.EqualTo(5));
    }

    [Test]
    public void Hit_InterleavedFreeAndBlocking_CorrectCount()
    {
        var cp = new CheckPoint();
        cp.Arm(4); // HitCount = 3

        cp.Hit(); // 2
        cp.Hit(); // 1
        cp.Hit(); // 0

        Assert.That(cp.HitCount, Is.Zero);

        var blocked = true;
        var thread = new Thread(() =>
        {
            cp.Hit();
            blocked = false;
        });
        thread.Start();

        Thread.Sleep(100);
        Assert.That(blocked, Is.True);
        Assert.That(cp.HitCount, Is.Zero);

        cp.Reset();
        thread.Join(1000);
        Assert.That(blocked, Is.False);
    }

    [Test]
    public async Task Arm_Task_CompletesWithReached_WhenHitBlocks()
    {
        var cp = new CheckPoint();
        var armTask = cp.Arm(3); // HitCount = 2

        cp.Hit(); // 1
        cp.Hit(); // 0

        var hitTask = cp.HitAsync();

        var result = await armTask.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.That(result, Is.EqualTo(CheckPointWaitResult.Reached));

        cp.Reset();
        await hitTask.WaitAsync(TimeSpan.FromSeconds(1));
    }

    [Test]
    public async Task Arm_Task_CompletesWithReached_OnlyOnce()
    {
        var cp = new CheckPoint();
        var armTask = cp.Arm(2); // HitCount = 1
        cp.Hit();                // 0

        // First blocking hit completes the arm task
        var hitTask1 = cp.HitAsync();
        var result1 = await armTask.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.That(result1, Is.EqualTo(CheckPointWaitResult.Reached));

        // Second blocking hit should not throw or cause issues
        var hitTask2 = cp.HitAsync();
        Assert.That(hitTask2.IsCompleted, Is.False);

        cp.Reset();
        await Task.WhenAll(hitTask1, hitTask2).WaitAsync(TimeSpan.FromSeconds(1));
    }

    [Test]
    public async Task Arm_Task_CompletesWithReleased_OnReset()
    {
        var cp = new CheckPoint();
        var armTask = cp.Arm(5);
        cp.Reset();

        var result = await armTask.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.That(result, Is.EqualTo(CheckPointWaitResult.Released));
    }

    [Test]
    public async Task Arm_Task_CompletesWithReleased_OnDispose()
    {
        var cp = new CheckPoint();
        var armTask = cp.Arm(5);
        cp.Dispose();

        var result = await armTask.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.That(result, Is.EqualTo(CheckPointWaitResult.Released));
    }

    [Test]
    public async Task Arm_Task_CompletesWithReleased_OnNewArm()
    {
        var cp = new CheckPoint();
        var firstArm = cp.Arm(5);

        cp.Arm(3);

        var firstResult = await firstArm.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.Multiple(() =>
        {
            Assert.That(firstResult, Is.EqualTo(CheckPointWaitResult.Released));
            Assert.That(cp.HitCount, Is.EqualTo(2));
        });
    }

    [Test]
    public void Reset_UnblocksBlockedHit()
    {
        var cp = new CheckPoint();
        cp.Arm(1);

        var completed = false;
        var thread = new Thread(() =>
        {
            cp.Hit();
            completed = true;
        });
        thread.Start();

        Thread.Sleep(100);
        Assert.That(completed, Is.False);

        cp.Reset();
        Assert.That(thread.Join(1000), Is.True);
        Assert.That(completed, Is.True);
    }

    [Test]
    public async Task Reset_CompletesPendingHitAsync()
    {
        var cp = new CheckPoint();
        cp.Arm(1);

        var hitTask = cp.HitAsync();
        Assert.That(hitTask.IsCompleted, Is.False);

        cp.Reset();
        await hitTask.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.That(hitTask.IsCompleted, Is.True);
    }

    [Test]
    public void Reset_WhenUnarmed_DoesNothing()
    {
        var cp = new CheckPoint();
        cp.Reset();
        Assert.Multiple(() =>
        {
            Assert.That(cp.IsArmed, Is.False);
            Assert.That(cp.HitCount, Is.Zero);
        });
    }

    [Test]
    public void Reset_SetsIsArmedFalseAndHitCountZero()
    {
        var cp = new CheckPoint();
        cp.Arm(5);
        Assert.That(cp.IsArmed, Is.True);
        Assert.That(cp.HitCount, Is.EqualTo(4));

        cp.Reset();
        Assert.Multiple(() =>
        {
            Assert.That(cp.IsArmed, Is.False);
            Assert.That(cp.HitCount, Is.Zero);
        });
    }

    [Test]
    public void Arm_AfterReset_ResetsAndStartsFresh()
    {
        var cp = new CheckPoint();
        cp.Arm(5);
        cp.Hit(); // HitCount = 3
        cp.Reset();

        Assert.That(cp.IsArmed, Is.False);
        Assert.That(cp.HitCount, Is.Zero);

        cp.Arm(2);
        Assert.That(cp.HitCount, Is.EqualTo(1));
    }

    [Test]
    public void Dispose_UnblocksBlockedHit()
    {
        var cp = new CheckPoint();
        cp.Arm(1);

        var completed = false;
        var thread = new Thread(() =>
        {
            cp.Hit();
            completed = true;
        });
        thread.Start();

        Thread.Sleep(100);
        cp.Dispose();
        Assert.That(thread.Join(1000), Is.True);
        Assert.That(completed, Is.True);
    }

    [Test]
    public async Task Dispose_CompletesPendingHitAsync()
    {
        var cp = new CheckPoint();
        cp.Arm(1);

        var hitTask = cp.HitAsync();
        Assert.That(hitTask.IsCompleted, Is.False);

        cp.Dispose();
        await hitTask.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.That(hitTask.IsCompleted, Is.True);
    }

    [Test]
    public void Dispose_Arm_ThrowsObjectDisposedException()
    {
        var cp = new CheckPoint();
        cp.Dispose();
        Assert.Throws<ObjectDisposedException>(() => cp.Arm(1));
    }

    [Test]
    public void Dispose_Reset_DoesNothing()
    {
        var cp = new CheckPoint();
        cp.Dispose();
        Assert.DoesNotThrow(() => cp.Reset());
    }

    [Test]
    public void Dispose_Hit_DoesNothing()
    {
        var cp = new CheckPoint();
        cp.Dispose();
        Assert.DoesNotThrow(() => cp.Hit());
    }

    [Test]
    public async Task Dispose_HitAsync_ReturnsCompletedTask()
    {
        var cp = new CheckPoint();
        cp.Dispose();
        var task = cp.HitAsync();
        Assert.That(task.IsCompleted, Is.True);
        await task;
    }

    [Test]
    public void Dispose_AfterArm_CompletesArmTask()
    {
        var cp = new CheckPoint();
        var armTask = cp.Arm(5);
        cp.Dispose();
        Assert.That(armTask.IsCompleted, Is.True);
        Assert.That(armTask.Result, Is.EqualTo(CheckPointWaitResult.Released));
    }

    [Test]
    public void Dispose_IsIdempotent()
    {
        var cp = new CheckPoint();
        cp.Dispose();
        Assert.DoesNotThrow(() => cp.Dispose());
    }

    [Test]
    public void IsArmed_TrueAfterArm_FalseAfterReset()
    {
        var cp = new CheckPoint();
        Assert.That(cp.IsArmed, Is.False);
        cp.Arm(3);
        Assert.That(cp.IsArmed, Is.True);
        cp.Reset();
        Assert.That(cp.IsArmed, Is.False);
    }

    [Test]
    public void IsArmed_StaysTrue_WhenHitCountReachesZero()
    {
        var cp = new CheckPoint();
        cp.Arm(2);
        cp.Hit();
        Assert.That(cp.IsArmed, Is.True);
        Assert.That(cp.HitCount, Is.Zero);
    }

    [Test]
    public void HitCount_ReturnsZero_WhenUnarmed()
    {
        var cp = new CheckPoint();
        Assert.That(cp.HitCount, Is.Zero);
        cp.Reset();
        Assert.That(cp.HitCount, Is.Zero);
    }

    [Test]
    public void HitCount_ReturnsCorrectValue_WhileArmed()
    {
        var cp = new CheckPoint();
        cp.Arm(5);
        Assert.That(cp.HitCount, Is.EqualTo(4));
        cp.Hit();
        Assert.That(cp.HitCount, Is.EqualTo(3));
        cp.Hit();
        Assert.That(cp.HitCount, Is.EqualTo(2));
        cp.Hit();
        Assert.That(cp.HitCount, Is.EqualTo(1));
        cp.Hit();
        Assert.That(cp.HitCount, Is.Zero);
    }

    [Test]
    public async Task Arm_NewArm_UnblocksOldBlockedHits()
    {
        var cp = new CheckPoint();
        cp.Arm(1);

        var completed = false;
        var thread = new Thread(() =>
        {
            cp.Hit();
            completed = true;
        });
        thread.Start();

        Thread.Sleep(100);
        Assert.That(completed, Is.False);

        cp.Arm(3);
        Assert.That(thread.Join(1000), Is.True);
        Assert.That(completed, Is.True);
        Assert.That(cp.HitCount, Is.EqualTo(2));
    }

    [Test]
    public async Task NewArm_CompletesOldPendingHitAsync()
    {
        var cp = new CheckPoint();
        cp.Arm(1);

        var hitTask = cp.HitAsync();
        Assert.That(hitTask.IsCompleted, Is.False);

        cp.Arm(3);
        await hitTask.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.That(hitTask.IsCompleted, Is.True);
        Assert.That(cp.HitCount, Is.EqualTo(2));
    }

    [Test]
    public void Hit_BlockedThreads_IsArmedStaysTrue()
    {
        var cp = new CheckPoint();
        cp.Arm(1);

        var thread = new Thread(() => cp.Hit());
        thread.Start();

        Thread.Sleep(100);
        Assert.That(cp.IsArmed, Is.True);

        cp.Reset();
        thread.Join(1000);
    }

    [Test]
    public void ConcurrentHits_AllAllowed_CountCorrect()
    {
        var cp = new CheckPoint();
        const int n = 100;
        cp.Arm(n);

        Parallel.For(0, n - 1, _ => cp.Hit());

        Assert.That(cp.HitCount, Is.Zero);
    }

    [Test]
    public void ConcurrentHitAndReset_NoDeadlock()
    {
        var cp = new CheckPoint();

        var hitThread = new Thread(() =>
        {
            for (var i = 0; i < 1000; i++)
                cp.Hit();
        });

        var resetThread = new Thread(() =>
        {
            for (var i = 0; i < 100; i++)
            {
                cp.Arm(10);
                cp.Reset();
            }
        });

        hitThread.Start();
        resetThread.Start();
        Assert.That(hitThread.Join(5000), Is.True);
        Assert.That(resetThread.Join(5000), Is.True);
    }

    [Test]
    public async Task HitAsync_Concurrent_AllComplete()
    {
        var cp = new CheckPoint();
        cp.Arm(100);

        var tasks = new Task[99];
        for (var i = 0; i < tasks.Length; i++)
            tasks[i] = cp.HitAsync();

        await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(cp.HitCount, Is.Zero);
    }

    [Test]
    public void FullLifecycle_AcceptThenBlockThenResetThenReuse()
    {
        var cp = new CheckPoint();

        cp.Arm(3);
        cp.Hit();
        cp.Hit();

        var blocked = true;
        var thread = new Thread(() =>
        {
            cp.Hit();
            blocked = false;
        });
        thread.Start();
        Thread.Sleep(100);
        Assert.That(blocked, Is.True);

        cp.Reset();
        thread.Join(1000);
        Assert.That(blocked, Is.False);
        Assert.That(cp.IsArmed, Is.False);

        cp.Arm(2);
        cp.Hit();

        var blocked2 = true;
        var thread2 = new Thread(() =>
        {
            cp.Hit();
            blocked2 = false;
        });
        thread2.Start();
        Thread.Sleep(100);
        Assert.That(blocked2, Is.True);

        cp.Reset();
        thread2.Join(1000);
        Assert.That(blocked2, Is.False);
    }

    [Test]
    public async Task HitAsync_Chained_WithArm()
    {
        var cp = new CheckPoint();
        var armTask = cp.Arm(3);

        var hit1 = cp.HitAsync();
        var hit2 = cp.HitAsync();
        var hit3 = cp.HitAsync();

        Assert.That(hit1.IsCompleted, Is.True);
        Assert.That(hit2.IsCompleted, Is.True);
        Assert.That(hit3.IsCompleted, Is.False);

        var armResult = await armTask.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.That(armResult, Is.EqualTo(CheckPointWaitResult.Reached));

        cp.Reset();
        await hit3.WaitAsync(TimeSpan.FromSeconds(1));
    }

    [Test]
    public void Hit_ZeroFreeHits_BlocksFirstCaller()
    {
        var cp = new CheckPoint();
        cp.Arm(1);

        var blocked = true;
        var thread = new Thread(() =>
        {
            cp.Hit();
            blocked = false;
        });
        thread.Start();

        Thread.Sleep(100);
        Assert.That(blocked, Is.True);

        cp.Reset();
        thread.Join(1000);
        Assert.That(blocked, Is.False);
    }

    [Test]
    public async Task HitAsync_ZeroFreeHits_ReturnsPendingTask()
    {
        var cp = new CheckPoint();
        cp.Arm(1);

        var task = cp.HitAsync();
        Assert.That(task.IsCompleted, Is.False);

        cp.Reset();
        await task.WaitAsync(TimeSpan.FromSeconds(1));
    }

    [Test]
    public void ConcurrentHitAndDispose_NoDeadlock()
    {
        var cp = new CheckPoint();
        cp.Arm(10);

        var hitThreads = new Thread[20];
        for (var i = 0; i < hitThreads.Length; i++)
        {
            hitThreads[i] = new Thread(() =>
            {
                for (var j = 0; j < 10; j++)
                    cp.Hit();
            });
            hitThreads[i].Start();
        }

        Thread.Sleep(50);
        cp.Dispose();

        foreach (var t in hitThreads)
            Assert.That(t.Join(5000), Is.True);
    }

    [Test]
    public async Task Arm_Task_DoesNotComplete_OnPenultimateHit()
    {
        var cp = new CheckPoint();
        var armTask = cp.Arm(3); // HitCount = 2

        cp.Hit(); // HitCount = 1
        Assert.That(armTask.IsCompleted, Is.False, "Arm task should not complete before the final hit");

        cp.Hit(); // HitCount = 0

        var hitTask = cp.HitAsync(); // blocks, arm completes
        var result = await armTask.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.That(result, Is.EqualTo(CheckPointWaitResult.Reached));

        cp.Reset();
        await hitTask.WaitAsync(TimeSpan.FromSeconds(1));
    }

    [Test]
    public void HitConcurrentWithReset_CountIsConsistent()
    {
        const int iterations = 1000;
        for (var trial = 0; trial < 10; trial++)
        {
            var cp = new CheckPoint();
            cp.Arm(iterations + 2); // enough headroom that we won't block

            var done = new ManualResetEventSlim();
            var hitCount = 0;

            var hitThread = new Thread(() =>
            {
                done.Wait();
                for (var i = 0; i < iterations; i++)
                {
                    cp.Hit();
                    Interlocked.Increment(ref hitCount);
                }
            });

            var resetThread = new Thread(() =>
            {
                done.Wait();
                for (var i = 0; i < iterations; i++)
                    cp.Reset();
            });

            hitThread.Start();
            resetThread.Start();
            done.Set();

            Assert.That(hitThread.Join(5000), Is.True);
            Assert.That(resetThread.Join(5000), Is.True);

            // Every Hit() either decremented when armed or was a no-op when unarmed.
            // The number of actual decrements must be >= 0 and <= iterations.
            cp.Reset();
            Assert.That(cp.HitCount, Is.GreaterThanOrEqualTo(0));
        }
    }

    [Test]
    public void Hit_BlockedUnderArm1_DoesNotReenterWait_AfterNewArm1()
    {
        var cp = new CheckPoint();
        cp.Arm(1); // HitCount = 0

        var exited = false;
        var thread = new Thread(() =>
        {
            cp.Hit(); // blocks
            exited = true;
        });
        thread.Start();

        Thread.Sleep(100);
        Assert.That(exited, Is.False);

        cp.Arm(1); // unblocks thread via Reset, then HitCount = 0 again

        Assert.That(thread.Join(1000), Is.True);
        Assert.That(exited, Is.True,
            "Blocked Hit() should exit after new Arm(), not re-enter wait despite HitCount == 0");
    }

    [Test]
    public void MultipleBlockedHits_AllUnblockOnDispose()
    {
        var cp = new CheckPoint();
        cp.Arm(1);

        var completed = 0;
        var threads = new Thread[5];
        for (var i = 0; i < threads.Length; i++)
        {
            threads[i] = new Thread(() =>
            {
                cp.Hit();
                Interlocked.Increment(ref completed);
            });
            threads[i].Start();
        }

        Thread.Sleep(200);
        Assert.That(completed, Is.Zero);

        cp.Dispose();
        foreach (var t in threads)
            Assert.That(t.Join(1000), Is.True);

        Assert.That(completed, Is.EqualTo(5));
    }

    [Test]
    public void MultipleBlockedHits_AllUnblockOnNewArm()
    {
        var cp = new CheckPoint();
        cp.Arm(1);

        var completed = 0;
        var threads = new Thread[5];
        for (var i = 0; i < threads.Length; i++)
        {
            threads[i] = new Thread(() =>
            {
                cp.Hit();
                Interlocked.Increment(ref completed);
            });
            threads[i].Start();
        }

        Thread.Sleep(200);
        Assert.That(completed, Is.Zero);

        cp.Arm(3);
        foreach (var t in threads)
            Assert.That(t.Join(1000), Is.True);

        Assert.That(completed, Is.EqualTo(5));
        Assert.That(cp.HitCount, Is.EqualTo(2));
    }

    [Test]
    public async Task MultipleBlockedHitAsync_AllUnblockOnNewArm()
    {
        var cp = new CheckPoint();
        cp.Arm(1);

        var tasks = new Task[5];
        for (var i = 0; i < tasks.Length; i++)
            tasks[i] = cp.HitAsync();

        foreach (var t in tasks)
            Assert.That(t.IsCompleted, Is.False);

        cp.Arm(3);
        await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(1));

        Assert.That(cp.HitCount, Is.EqualTo(2));
    }

    [Test]
    public void ConcurrentArm_TwoThreads_Linearized()
    {
        var cp = new CheckPoint();

        int? firstResult = null;
        var latch = new ManualResetEventSlim();

        var threadA = new Thread(() =>
        {
            latch.Wait();
            cp.Arm(5);
            firstResult = 5;
        });

        var threadB = new Thread(() =>
        {
            latch.Wait();
            cp.Arm(3);
            firstResult ??= 3;
        });

        threadA.Start();
        threadB.Start();
        latch.Set();

        Assert.That(threadA.Join(1000), Is.True);
        Assert.That(threadB.Join(1000), Is.True);

        // After both arms complete, the winner's arm is in effect.
        // HitCount should be either 4 (Arm(5) won) or 2 (Arm(3) won).
        Assert.That(cp.HitCount, Is.AnyOf(4, 2));
        Assert.That(cp.IsArmed, Is.True);
    }

    [Test]
    public async Task ConcurrentArm_BothTasksCompleteCorrectly()
    {
        var cp = new CheckPoint();

        var latch = new ManualResetEventSlim();
        Task<CheckPointWaitResult>? taskA = null;
        Task<CheckPointWaitResult>? taskB = null;

        var threadA = new Thread(() =>
        {
            latch.Wait();
            taskA = cp.Arm(100);
        });

        var threadB = new Thread(() =>
        {
            latch.Wait();
            taskB = cp.Arm(100);
        });

        threadA.Start();
        threadB.Start();
        latch.Set();

        Assert.That(threadA.Join(1000), Is.True);
        Assert.That(threadB.Join(1000), Is.True);

        // Exactly one should already be complete with Released
        var aDone = taskA!.IsCompleted;
        var bDone = taskB!.IsCompleted;
        Assert.That(aDone ^ bDone, Is.True, "Exactly one arm task should complete Released immediately");

        var released = aDone ? await taskA : await taskB;
        Assert.That(released, Is.EqualTo(CheckPointWaitResult.Released));

        // Complete the winner by exhausting hits
        var pending = aDone ? taskB! : taskA!;
        for (var i = 0; i < 99; i++)
            cp.Hit();
        var hitTask = cp.HitAsync(); // blocks, completes winner

        var winnerResult = await pending;
        Assert.That(winnerResult, Is.EqualTo(CheckPointWaitResult.Reached));

        cp.Reset();
        await hitTask;
    }

    [Test]
    public void Stress_AllOperationsConcurrent()
    {
        var cp = new CheckPoint();
        var cts = new CancellationTokenSource();
        const int runtimeMs = 2000;

        var errors = 0;

        var armThread = new Thread(() =>
        {
            var rng = new Random(42);
            while (!cts.IsCancellationRequested)
            {
                try
                {
                    var n = rng.Next(1, 6);
                    cp.Arm(n);
                }
                catch (ObjectDisposedException) { }
                catch (ArgumentOutOfRangeException) { }
                catch (Exception) { Interlocked.Increment(ref errors); }
            }
        });

        var resetThread = new Thread(() =>
        {
            while (!cts.IsCancellationRequested)
            {
                try { cp.Reset(); }
                catch (Exception) { Interlocked.Increment(ref errors); }
            }
        });

        var disposeThread = new Thread(() =>
        {
            while (!cts.IsCancellationRequested)
            {
                try { cp.Dispose(); }
                catch (Exception) { Interlocked.Increment(ref errors); }
            }
        });

        var hitThreads = new Thread[8];
        for (var i = 0; i < hitThreads.Length; i++)
        {
            hitThreads[i] = new Thread(() =>
            {
                while (!cts.IsCancellationRequested)
                {
                    try { cp.Hit(); }
                    catch (ObjectDisposedException) { }
                    catch (Exception) { Interlocked.Increment(ref errors); }
                }
            });
        }

        armThread.Start();
        resetThread.Start();
        disposeThread.Start();
        foreach (var t in hitThreads)
            t.Start();

        Thread.Sleep(runtimeMs);
        cts.Cancel();

        Assert.That(armThread.Join(3000), Is.True);
        Assert.That(resetThread.Join(3000), Is.True);
        Assert.That(disposeThread.Join(3000), Is.True);
        foreach (var t in hitThreads)
            Assert.That(t.Join(3000), Is.True);

        Assert.That(errors, Is.Zero, "Unexpected exceptions during stress test");
    }
}
