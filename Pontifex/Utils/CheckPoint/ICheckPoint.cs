using System;
using System.Threading.Tasks;

namespace Pontifex.Utils.CheckPointGate
{
    /// <summary>
    ///   Represents a checkpoint gate that can be hit (signalled) by
    ///   producer code.  The gate may either accept the hit immediately
    ///   or block the caller until the gate is released, depending on
    ///   the current state controlled by <see cref="ICheckPointCtl"/>.
    /// </summary>
    public interface ICheckPoint
    {
        /// <summary>
        ///   Signals a hit on this checkpoint.
        ///   <list type="bullet">
        ///     <item><description>
        ///       If the gate is <b>not armed</b> — returns immediately (no-op).
        ///     </description></item>
        ///     <item><description>
        ///       If the gate is <b>armed</b> and <c>HitCount &gt; 0</c> —
        ///       decrements <c>HitCount</c> and returns immediately.
        ///     </description></item>
        ///     <item><description>
        ///       If the gate is <b>armed</b> and <c>HitCount == 0</c> —
        ///       blocks the calling thread until <see cref="ICheckPointCtl.Reset()"/>
        ///       or <see cref="IDisposable.Dispose"/> is invoked.
        ///     </description></item>
        ///   </list>
        /// </summary>
        void Hit();

        /// <summary>
        ///   Signals a hit on this checkpoint and returns a <see cref="Task"/>
        ///   that completes when the hit is accepted or the gate is released.
        ///   <list type="bullet">
        ///     <item><description>
        ///       If the gate is <b>not armed</b> — returns a completed task.
        ///     </description></item>
        ///     <item><description>
        ///       If the gate is <b>armed</b> and <c>HitCount &gt; 0</c> —
        ///       decrements <c>HitCount</c> and returns a completed task.
        ///     </description></item>
        ///     <item><description>
        ///       If the gate is <b>armed</b> and <c>HitCount == 0</c> —
        ///       returns a task that completes when <see cref="ICheckPointCtl.Reset()"/>
        ///       or <see cref="IDisposable.Dispose"/> is invoked.
        ///     </description></item>
        ///   </list>
        ///   The returned task never faults or is cancelled under normal
        ///   operational conditions.
        /// </summary>
        /// <returns>A task that represents the asynchronous Hit operation.</returns>
        Task HitAsync();
    }

    /// <summary>
    ///   Provides full control over a checkpoint gate: arming with a
    ///   required hit count, resetting the gate, and inspecting the
    ///   current state.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///     A checkpoint gate is a synchronisation primitive that allows
    ///     a configurable number of <see cref="ICheckPoint.Hit"/> /
    ///     <see cref="ICheckPoint.HitAsync"/> calls before it blocks
    ///     subsequent calls, and which exposes an awaitable signal
    ///     (<see cref="Arm"/>) that completes when the blocking hit
    ///     arrives.
    ///   </para>
    ///   <para>
    ///     All members are thread-safe.  <see cref="Arm"/> call
    ///     linearises with respect to other <c>Arm</c>, <c>Reset</c>,
    ///     and <c>Dispose</c> calls.
    ///   </para>
    /// </remarks>
    public interface ICheckPointCtl : ICheckPoint, IDisposable
    {
        /// <summary>
        ///   Gets whether the gate is currently armed.
        /// </summary>
        /// <value>
        ///   <see langword="true"/> from the moment <see cref="Arm"/>
        ///   is invoked until <see cref="Reset"/> or <see cref="IDisposable.Dispose"/>
        ///   is called; <see langword="false"/> otherwise.
        ///   Reaching the required hit count does <b>not</b> change this value.
        /// </value>
        bool IsArmed { get; }

        /// <summary>
        ///   Gets the number of remaining hits permitted before the gate
        ///   blocks.  The counter is interpreted as <c>remaining hits - 1</c>
        ///   so that when it reaches <c>0</c> the <b>next</b> hit will block.
        /// </summary>
        /// <value>
        ///   <c>0</c> when the gate is not armed.
        ///   <c><paramref name="requiredHits"/> - 1</c> immediately after
        ///   <see cref="Arm(int)"/>.
        ///   Decremented by each successful <see cref="ICheckPoint.Hit"/> /
        ///   <see cref="ICheckPoint.HitAsync"/> call while armed.
        ///   Never negative.
        /// </value>
        int HitCount { get; }

        /// <summary>
        ///   Arms the gate and returns a task that completes when the
        ///   required number of hits has been exhausted (i.e. the next
        ///   hit will block) <b>or</b> when the gate is released before
        ///   that point.
        /// </summary>
        /// <param name="requiredHits">
        ///   The number of <see cref="ICheckPoint.Hit"/> /
        ///   <see cref="ICheckPoint.HitAsync"/> calls that must occur
        ///   before the gate blocks.  Must be greater than zero.
        /// </param>
        /// <returns>
        ///   A <see cref="Task{CheckPointWaitResult}"/> that completes with
        ///   <see cref="CheckPointWaitResult.Reached"/> when the blocking
        ///   hit arrives, or with <see cref="CheckPointWaitResult.Released"/>
        ///   when <see cref="Reset"/> or <see cref="IDisposable.Dispose"/>
        ///   is invoked (or a new <c>Arm</c> call replaces this one).
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        ///   Thrown when <paramref name="requiredHits"/> is less than or equal to zero.
        /// </exception>
        /// <remarks>
        ///   <para>
        ///     Each call to <c>Arm</c> implicitly performs a <see cref="Reset"/>
        ///     first: any previously pending <c>Arm</c> task completes with
        ///     <see cref="CheckPointWaitResult.Released"/>, any blocked hits
        ///     are unblocked, and <see cref="HitCount"/> starts fresh.
        ///   </para>
        ///   <para>
        ///     After a successful <c>Arm(requiredHits)</c>, the first
        ///     <c>requiredHits - 1</c> hits return immediately.  The
        ///     <c>requiredHits</c>-th hit blocks the caller, and it is at
        ///     that moment that the returned task transitions to
        ///     <see cref="CheckPointWaitResult.Reached"/>.
        ///   </para>
        /// </remarks>
        Task<CheckPointWaitResult> Arm(int requiredHits = 1);

        /// <summary>
        ///   Resets the gate to its unarmed state.  If the gate is already
        ///   unarmed, this is a no-op.
        /// </summary>
        /// <remarks>
        ///   <para>
        ///     <see cref="HitCount"/> is set to <c>0</c> and
        ///     <see cref="IsArmed"/> becomes <see langword="false"/>.
        ///   </para>
        ///   <para>
        ///     Any threads blocked in <see cref="ICheckPoint.Hit"/> are
        ///     unblocked, and any pending tasks from
        ///     <see cref="ICheckPoint.HitAsync"/> or <see cref="Arm"/>
        ///     (if the gate had not yet reached the required hit count)
        ///     are completed with <see cref="CheckPointWaitResult.Released"/>.
        ///   </para>
        /// </remarks>
        void Reset();
    }
}