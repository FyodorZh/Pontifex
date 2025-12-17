using System;
using Actuarius.Memory;
using Actuarius.PeriodicLogic;
using Pontifex.Utils;

namespace Pontifex.Transports.Tcp
{
    internal class KeepAliver : IPeriodicLogic
    {
        private readonly AckRawTcpClient _owner;
        private readonly IMemoryRental _memoryRental;
        private ILogicDriverCtl _driver;

        public KeepAliver(AckRawTcpClient owner, IMemoryRental memoryRental)
        {
            _owner = owner;
            _memoryRental = memoryRental;
        }

        bool IPeriodicLogic.LogicStarted(ILogicDriverCtl driver)
        {
            _driver = driver;
            return true;
        }

        void IPeriodicLogic.LogicTick()
        {
            try
            {
                if (_owner.ConnectionState != AckRawTcpClient.State.Connecting)
                {
                    DateTime now = DateTime.UtcNow;
                    long data = now.ToBinary();

                    var buffer = _memoryRental.CollectablePool.Acquire<UnionDataList>();
                    buffer.PutFirst(data);

                    var result = _owner.DoSend(PacketType.Ping, buffer);
                    if (result != SendResult.Ok)
                    {
                        _owner.Stop(new StopReasons.TextFail(_owner.Type, "{0}: Keep alive send failed with result '{1}'", _owner, result));
                    }
                }

                _owner.Tick();
            }
            catch (Exception ex)
            {
                _owner.Stop(new StopReasons.ExceptionFail(_owner.Type, ex, _owner + ": Keep alive failed."));
            }
        }

        public void Stop()
        {
            if (_driver != null)
            {
                _driver.Stop();
            }
        }

        void IPeriodicLogic.LogicStopped()
        {
        }
    }
}