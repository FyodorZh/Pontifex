using System;
using System.Collections.Generic;
using System.Threading;
using Actuarius.Collections;
using Actuarius.Concurrent;
using Actuarius.Memory;
using Operarius;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Raw
{
    /// <summary>
    /// Abstract base class for a raw data endpoint. Provides thread-safe outbound message admission and
    /// serialization through a command queue drained on the non-periodic driver thread, inbound message
    /// dispatch to the handler, and the one-shot start/stop lifecycle shared by raw transports.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Thread safety</b>: All public and protected instance members are safe to call
    /// concurrently from any thread. Outbound messages are transmitted in admission order on the single
    /// driver thread; inbound delivery and stop notifications are serialized through
    /// <see cref="_handlerLock"/>.</description></item>
    /// <item><description><b>Preconditions/Postconditions</b>: A stop request is latched. Once StopNow,
    /// StopGracefully, or the driver has stopped this endpoint, it cannot be started again.</description></item>
    /// </list>
    /// </remarks>
    public abstract class RawEndpoint : IRawEndpoint, INonPeriodicLogic
    {
        private readonly IRawTransport _owner;
        private readonly long _bufferCapacityBytes;
        private readonly IRawHandler _handler;
        
        private readonly ConcurrentQueueValve<Command> _commandsQueue;
        
        private long _totalBuffersSize;

        private volatile int _stopRequested;

        private volatile INonPeriodicLogicDriverCtl? _driverCtl;


        /// <summary>
        /// Serializes handler callback delivery. It is re-entrant for the calling thread and guards every
        /// invocation of the endpoint handler.
        /// </summary>
        protected readonly object _handlerLock = new();
        
        /// <summary>
        /// Gets the remote endpoint address, or null when no remote address is known. Safe to read
        /// concurrently.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Other notes</b>: The value is captured at construction and never changes for the
        /// lifetime of the endpoint, including after a stop.</description></item>
        /// </list>
        /// </remarks>
        public IEndPoint? RemoteEndPoint { get; }

        /// <summary>
        /// Gets the maximum message size in bytes supported by the transport. It is an inclusive maximum
        /// for the application payload and excludes transport framing and control metadata. Empty payloads
        /// are valid. Safe to read concurrently.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Other notes</b>: The value is fixed at construction. Admission compares the size of
        /// a message to be sent against this limit.</description></item>
        /// </list>
        /// </remarks>
        public int MessageMaxByteSize { get; }

        /// <summary>
        /// Gets the name of the owning transport.
        /// </summary>
        protected string TransportName => _owner.Name;

        /// <summary>
        /// Gets the logging system of the owning transport.
        /// </summary>
        protected ILogger Log => _owner.Log;

        /// <summary>
        /// Gets the memory rental system of the owning transport, used to rent buffers.
        /// </summary>
        protected IMemoryRental Memory => _owner.Memory;
        
        /// <summary>
        /// Initializes a new raw endpoint owned by the given transport.
        /// </summary>
        /// <param name="owner">The owning transport that provides the transport name, logging, and
        /// memory rental.</param>
        /// <param name="remoteEndpoint">The remote endpoint address, or null when it is unknown.</param>
        /// <param name="handler">The handler that receives inbound messages and stop notifications.</param>
        /// <param name="messageMaxByteSize">The inclusive maximum message size, in bytes,
        /// admitted by this endpoint.</param>
        /// <param name="bufferCapacityBytes">The byte budget for messages accepted by this endpoint
        /// but not yet transmitted.</param>
        protected RawEndpoint(IRawTransport owner, IEndPoint remoteEndpoint, IRawHandler handler, 
            int messageMaxByteSize, long bufferCapacityBytes)
        {
            RemoteEndPoint = remoteEndpoint;
            MessageMaxByteSize = messageMaxByteSize;

            _owner = owner;
            _bufferCapacityBytes = bufferCapacityBytes;
            _handler = handler;
            
            _commandsQueue = new ConcurrentQueueValve<Command>(
                new SynchronizedBySpinLockConcurrentQueue<Command>(
                    new SystemQueue<Command>()),
                command =>
                {
                    switch (command.Type)
                    {
                        case CommandType.SendData:
                            Interlocked.Add(ref _totalBuffersSize, -command.DataSize);
                            command.Data?.Release();
                            break;
                    }
                });
        }

        /// <summary>
        /// Transmits a single outbound buffer through the concrete carrier.
        /// </summary>
        /// <param name="bufferToSend">The buffer to transmit. Ownership transfers to the implementation.</param>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Preconditions/Postconditions</b>: The implementation receives ownership of
        /// bufferToSend and must call Release() on it exactly once, including when the carrier cannot
        /// accept it. It must not throw.</description></item>
        /// <item><description><b>Thread safety</b>: Invoked only on the non-periodic driver thread, never
        /// concurrently with another invocation.</description></item>
        /// <item><description><b>Side effects</b>: A throwing override stops the endpoint; the buffer already
        /// popped from the queue is not released by the base.</description></item>
        /// </list>
        /// </remarks>
        protected abstract void DoSendRaw(UnionDataList bufferToSend);
        
        /// <summary>
        /// Determines whether the endpoint can currently admit a new outbound message.
        /// </summary>
        /// <param name="errorCode">When the method returns false, receives the SendResult that describes why
        /// sending is not allowed; otherwise it is set to SendResult.Ok.</param>
        /// <returns>true when a message may be admitted; otherwise false with errorCode set.</returns>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Other notes</b>: The default implementation returns false with NotConnected when a
        /// stop was requested or the endpoint logic has not been started yet. Overrides add transport-specific
        /// flow control and must not throw.</description></item>
        /// </list>
        /// </remarks>
        protected virtual bool CanSendNow(out SendResult errorCode)
        {
            errorCode = SendResult.Ok;
            
            if (_stopRequested != 0)
            {
                errorCode = SendResult.NotConnected;
            }
            else if (_driverCtl == null)
            {
                errorCode = SendResult.NotConnected;
            }
            
            return errorCode == SendResult.Ok;
        }

        /// <summary>
        /// Validates and admits one outbound buffer for transmission, or rejects it synchronously.
        /// </summary>
        /// <param name="bufferToSend">The buffer to send. Ownership transfers to the endpoint
        /// unconditionally, whatever the result.</param>
        /// <returns>A SendResult describing the admission: Ok, or a synchronous rejection.</returns>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Preconditions/Postconditions</b>: On any result other than InvalidMessage the base
        /// consumes and releases the buffer. On Ok the buffer is owned by the endpoint and released after
        /// transmission or when it is dropped by a stop.</description></item>
        /// <item><description><b>Thread safety</b>: Safe for concurrent calls from any thread; concurrent Ok sends
        /// are transmitted in admission order.</description></item>
        /// <item><description><b>Other notes</b>: The byte budget enforced by bufferCapacityBytes is a best-effort
        /// bound under concurrent senders. A successful admission requests a driver invocation.</description></item>
        /// </list>
        /// </remarks>
        protected SendResult ScheduleToSend(UnionDataList bufferToSend)
        {
            if (bufferToSend == null! || !bufferToSend.IsAlive)
            {
                return SendResult.InvalidMessage;
            }

            int bufferSize = bufferToSend.GetDataSize();
            if (bufferSize > MessageMaxByteSize)
            {
                bufferToSend.Release();
                return SendResult.MessageTooBig;
            }
            
            if (!CanSendNow(out var errorCode))
            {
                bufferToSend.Release();
                return errorCode;
            }
            
            Interlocked.Add(ref _totalBuffersSize, bufferSize);
            if (_totalBuffersSize > _bufferCapacityBytes)
            {
                Interlocked.Add(ref _totalBuffersSize, -bufferSize);
                bufferToSend.Release();
                return SendResult.BufferOverflow;
            }

            switch (_commandsQueue.EnqueueEx(new Command(bufferToSend, bufferSize)))
            {
                case ValveEnqueueResult.Ok:
                    // continue
                    break;
                
                case ValveEnqueueResult.Overflown:
                    Interlocked.Add(ref _totalBuffersSize, -bufferSize);
                    bufferToSend.Release();
                    return SendResult.BufferOverflow;
                
                case ValveEnqueueResult.Rejected:
                    return SendResult.NotConnected;
                
                default:
                    throw new ArgumentOutOfRangeException();
            }
            
            _driverCtl?.RequestInvocation();
            return SendResult.Ok;
        }

        /// <inheritdoc/>
        public virtual SendResult Send(UnionDataList bufferToSend)
        {
            return ScheduleToSend(bufferToSend);
        }

        /// <summary>
        /// Stops the endpoint immediately: discards accepted but not yet transmitted messages, requests the
        /// driver to stop, and notifies the handler once.
        /// </summary>
        /// <param name="reason">The reason the endpoint is being stopped.</param>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Safe to call from any thread, including concurrently with other
        /// stop calls and handler callbacks. Only the first caller performs the stop; later calls are
        /// no-ops.</description></item>
        /// <item><description><b>Preconditions/Postconditions</b>: Exactly one handler stop notification is delivered
        /// per endpoint. After this call the endpoint is permanently stopped.</description></item>
        /// <item><description><b>Side effects</b>: Queued messages not yet transmitted are released. A transmission
        /// already in progress may still complete.</description></item>
        /// </list>
        /// </remarks>
        public void StopNow(StopReason reason)
        {
            if (Interlocked.Exchange(ref _stopRequested, 1) == 0)
            {
                _commandsQueue.CloseValve();
                _driverCtl?.Stop();
                OnStopped(reason);
            }
        }

        /// <summary>
        /// Requests a graceful stop: messages already accepted are flushed before the stop completes.
        /// </summary>
        /// <param name="reason">The reason the endpoint is being stopped.</param>
        /// <returns>true if this call performed the stop transition; false if a stop had already been
        /// requested.</returns>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Preconditions/Postconditions</b>: A later driver tick is required to finalize the
        /// stop. Messages admitted before the graceful-stop marker are transmitted first; messages admitted
        /// after it are dropped. A message admitted concurrently with the stop may return Ok and still be
        /// dropped.</description></item>
        /// <item><description><b>Thread safety</b>: Safe to call from any thread, including concurrently with other
        /// stop calls. Only the first caller performs the stop transition.</description></item>
        /// </list>
        /// </remarks>
        public bool StopGracefully(StopReason reason)
        {
            if (Interlocked.Exchange(ref _stopRequested, 1) == 0)
            {
                _commandsQueue.Put(new Command());
                OnStopped(reason);
                return true;
            }

            return false;
        }
        
        /// <summary>
        /// Populates dst with the IControl interfaces exposed by this endpoint, optionally filtered by
        /// predicate.
        /// </summary>
        /// <param name="dst">The list that receives the exposed controls.</param>
        /// <param name="predicate">An optional filter; when non-null, a control is added only if
        /// it is accepted.</param>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Other notes</b>: The base implementation adds no controls; overrides add the
        /// endpoint-specific controls.</description></item>
        /// </list>
        /// </remarks>
        public virtual void GetControls(List<IControl> dst, Predicate<IControl>? predicate = null)
        {
        }
        
        /// <summary>
        /// Delivers one inbound message to the handler, or drops it when the endpoint is not active.
        /// </summary>
        /// <param name="message">The received message. On delivery, ownership transfers to the handler;
        /// on drop, the base releases it.</param>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Callers may invoke this from several threads but should serialize
        /// their calls. Delivery and the stop notification are mutually exclusive, so a message is never
        /// delivered after the stop notification has been raised.</description></item>
        /// <item><description><b>Preconditions/Postconditions</b>: The message is released exactly once when the
        /// endpoint is stopped or has not been started yet.</description></item>
        /// </list>
        /// </remarks>
        protected virtual void ProcessInboundMessage(UnionDataList message)
        {
            bool canProcess;
            lock (_handlerLock)
            {
                canProcess = (_stopRequested == 0 && _driverCtl != null);
                if (canProcess)
                {
                    _handler.OnReceived(message);
                }
            }
            if (!canProcess)
            {
                message.Release();
            }
        }

        /// <summary>
        /// Invoked once when the endpoint logic is started successfully.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Called on the non-periodic driver thread, without holding the
        /// handler lock.</description></item>
        /// </list>
        /// </remarks>
        protected virtual void OnStarted()
        {
        }
        
        /// <summary>
        /// Notifies the handler that the endpoint has stopped, holding the handler lock.
        /// </summary>
        /// <param name="reason">The reason the endpoint stopped.</param>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Delivered exactly once per endpoint, from the thread that
        /// performed the stop transition; it is serialized with inbound delivery.</description></item>
        /// </list>
        /// </remarks>
        protected virtual void OnStopped(StopReason reason)
        {
            lock (_handlerLock)
            {
                _handler.OnStopped(reason);
            }
        }

        bool ILogic<INonPeriodicLogicDriverCtl>.LogicStarted(INonPeriodicLogicDriverCtl driver)
        {
            if (_stopRequested == 0)
            {
                _driverCtl = driver;
                OnStarted();
                return true;
            }

            return false;
        }

        void ILogic<INonPeriodicLogicDriverCtl>.LogicStopped()
        {
            StopNow(new Unknown(_owner.Name));
            _driverCtl = null;
        }

        void INonPeriodicLogic.LogicTick(INonPeriodicLogicDriverCtl driver)
        {
            while (_commandsQueue.TryPop(out var cmd))
            {
                switch (cmd.Type)
                {
                    case CommandType.Stop:
                        _commandsQueue.CloseValve();
                        _driverCtl?.Stop();
                        return;
                    case CommandType.SendData:
                        try
                        {
                            DoSendRaw(cmd.Data!);
                        }
                        catch (Exception ex)
                        {
                            StopNow(new ExceptionFail(TransportName, ex, "Fatal! RawEndpoint.DoSendRaw newer throws"));
                        }
                        finally
                        {
                            Interlocked.Add(ref _totalBuffersSize, -cmd.DataSize);    
                        }
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }
        
        private enum CommandType
        {
            Stop = 0,
            SendData
        }
        
        private readonly struct Command
        {
            public readonly CommandType Type;
            public readonly UnionDataList? Data;
            public readonly int DataSize;

            public Command(UnionDataList data, int dataSize)
            {
                Type = CommandType.SendData;
                Data = data;
                DataSize = dataSize;
            }
        }
    }
}