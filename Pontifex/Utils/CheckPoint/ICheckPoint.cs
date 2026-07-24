using System;
using System.Threading.Tasks;

namespace Pontifex.Utils.CheckPointGate
{
    /// <summary>
    /// An object that represents a breakpoint.
    /// When armed, it accepts a limited number of hits freely,
    /// then blocks the next hit until the controller release
    /// </summary>
    public interface ICheckPoint
    {
        /// <summary>
        /// Signals a hit. If the gate is unarmed or has remaining capacity,
        /// returns immediately. Otherwise blocks the calling thread until
        /// the gate is reset or disposed.
        /// </summary>
        void Hit();

        /// <summary>
        /// Signals a hit asynchronously, like <see cref="Hit"/>, but returns
        /// a task that completes when the hit is accepted or the gate is
        /// released. The returned task never faults or cancels.
        /// </summary>
        ValueTask HitAsync();
    }

    /// <summary>
    /// Full control handle for a checkpoint gate: arm with a hit quota,
    /// reset, and inspect state. All members are thread-safe.
    /// </summary>
    public interface ICheckPointCtl : ICheckPoint, IDisposable
    {
        /// <summary>
        /// Whether the gate is currently armed (<c>true</c> after <see cref="Arm"/>
        /// until <see cref="Reset"/> or dispose). Does not change when the
        /// hit quota runs out — the gate remains armed while blocking.
        /// </summary>
        bool IsArmed { get; }

        /// <summary>
        /// Remaining free hits before the next hit blocks. Equal to
        /// <c>requiredHits - 1</c> right after <see cref="Arm"/>,
        /// decremented by each hit, never negative. <c>0</c> when unarmed.
        /// </summary>
        int HitCount { get; }

        /// <summary>
        /// Arms the gate. If already armed, implicitly resets first
        /// (unblocking hits and completing the prior <see cref="Arm"/> task
        /// with <see cref="CheckPointWaitResult.Released"/>).
        /// </summary>
        /// <param name="requiredHits">Free hits before blocking. Must be > 0.</param>
        /// <returns>
        /// A task that completes with <see cref="CheckPointWaitResult.Reached"/>
        /// when the last free hit arrives and the blocking hit blocks,
        /// or <see cref="CheckPointWaitResult.Released"/> if reset/disposed
        /// before that point.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="requiredHits"/> is zero or negative.
        /// </exception>
        /// <exception cref="ObjectDisposedException">The gate is disposed.</exception>
        Task<CheckPointWaitResult> Arm(int requiredHits = 1);

        /// <summary>
        /// Resets the gate to unarmed. Sets <see cref="HitCount"/> to 0
        /// and <see cref="IsArmed"/> to <c>false</c>. Unblocks all blocked
        /// hits and completes any pending <see cref="Arm"/> task with
        /// <see cref="CheckPointWaitResult.Released"/>. No-op if already
        /// unarmed or disposed.
        /// </summary>
        void Reset();
    }
}
