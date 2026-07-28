using System;
using Pontifex.Utils.CheckPointGate;

namespace Pontifex
{
    /// <summary>
    /// Provides test-only deterministic control over the local lifecycle of any
    /// <see cref="ITransport"/> instance that derives from <see cref="AnyTransport"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This control is required only for an implementation that claims
    /// Carrier-Independent Core Conformance. A conformance adapter obtains it
    /// through <see cref="ITransport.GetControls"/> before starting the transport.
    /// </para>
    /// <para>
    /// The control deliberately has no packet injection, outbound interception,
    /// logging, or memory-observation capability. It must not directly invoke
    /// application callbacks or fabricate <see cref="SendResult"/> values.
    /// </para>
    /// <para>
    /// Implementations may expose this control only on instances constructed by
    /// their conformance adapter. Ordinary production instances must incur no
    /// conformance-control hot-path overhead.
    /// </para>
    /// </remarks>
    public interface IConformanceControl : IControl
    {
        /// <summary>
        /// A <see cref="ITransport.Stop"/> call is about to transition a running
        /// transport out of its running state.
        /// </summary>
        /// <remarks>
        /// The checkpoint must be reached before the transition becomes visible to
        /// a concurrent operation that checks the transport state. A returned gate
        /// is inactive until armed by the test. A checkpoint hit calls
        /// <see cref="ICheckPoint.Hit"/> and therefore blocks only while its gate
        /// is armed. All returned gates and this getter are safe for concurrent use.
        /// </remarks>
        ICheckPoint BeforeStopStateTransitionGate { get; }

        /// <summary>
        /// A successfully started transport is about to invoke its
        /// <see cref="ITransport.Start"/>-supplied <c>onStopped</c> callback.
        /// </summary>
        /// <remarks>
        /// The transport has already completed its terminal state transition when
        /// this checkpoint is reached. The gate permits tests to race repeated
        /// <c>Stop</c> calls with callback dispatch and verify exactly-once
        /// notification. A returned gate is inactive until armed by the test.
        /// A checkpoint hit calls <see cref="ICheckPoint.Hit"/> and therefore
        /// blocks only while its gate is armed. All returned gates and this
        /// getter are safe for concurrent use.
        /// </remarks>
        ICheckPoint BeforeStoppedCallbackGate { get; }

        /// <summary>
        /// Arms a one-shot fault that makes the next <see cref="ITransport.Start"/>
        /// call fail.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The transport has already started, start is in progress, or a start
        /// failure is already armed.
        /// </exception>
        /// <remarks>
        /// The next controlled <c>Start</c> call must return <see langword="false"/>,
        /// invalidate the transport, and must not invoke the callback supplied to
        /// that <c>Start</c> call. The fault is consumed by that one call.
        /// </remarks>
        void FailNextStart();

        /// <summary>
        /// Initiates one unrecoverable failure of the currently running transport.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The transport is not running, stopping has begun, or an unrecoverable
        /// failure has already been initiated.
        /// </exception>
        /// <remarks>
        /// The implementation must use its ordinary unrecoverable-failure path.
        /// It must stop, invalidate the transport, and invoke the callback supplied
        /// to a successful <c>Start</c> exactly once. It must not invoke that
        /// callback directly, synthesize a <see cref="StopReason"/>, or fabricate
        /// data-plane activity solely for the test.
        /// </remarks>
        void InjectUnrecoverableFailure();
    }
}
