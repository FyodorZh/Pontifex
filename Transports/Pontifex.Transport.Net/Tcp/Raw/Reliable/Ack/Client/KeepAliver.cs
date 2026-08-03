using System;
using Actuarius.Memory;
using Operarius;
using Pontifex.Utils;

namespace Pontifex.Raw.Reliable.Ack.Tcp
{
    internal class KeepAliver : IPeriodicLogic
    {
        private readonly RawReliableAckTcpClient _owner;
        private readonly IMemoryRental _memoryRental;
        private ILogicDriverCtl? _driver;

        public KeepAliver(RawReliableAckTcpClient owner, IMemoryRental memoryRental)
        {
            _owner = owner;
            _memoryRental = memoryRental;
        }

        bool ILogic<IPeriodicLogicDriverCtl>.LogicStarted(IPeriodicLogicDriverCtl driver)
        {
            _driver = driver;
            return true;
        }

        void IPeriodicLogic.LogicTick(IPeriodicLogicDriverCtl driver)
        {
            try
            {
                if (_owner.ConnectionState != RawReliableAckTcpClient.State.Connecting)
                {
                    DateTime now = DateTime.UtcNow;
                    long data = now.ToBinary();

                    var buffer = _memoryRental.CollectablePool.Acquire<UnionDataList>();
                    buffer.PutFirst(data);

                    var result = _owner.DoSend(PacketType.Ping, buffer);
                    if (result != SendResult.Ok)
                    {
                        _owner.Stop(new StopReasons.TextFail(_owner.Name, "{0}: Keep alive send failed with result '{1}'", _owner, result));
                    }
                }

                _owner.Tick();
            }
            catch (Exception ex)
            {
                _owner.Stop(new StopReasons.ExceptionFail(_owner.Name, ex, _owner + ": Keep alive failed."));
            }
        }

        public void Stop()
        {
            _driver?.Stop();
        }

        void ILogic<IPeriodicLogicDriverCtl>.LogicStopped()
        {
        }
    }
}