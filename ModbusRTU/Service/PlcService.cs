using ModbusRTU.Communication.Base;
using ModbusRTU.Communication.Base.Serial;
using ModbusRTU.Constants;
using ModbusRTU.Model;
using ModbusRTU.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace ModbusRTU.Service
{
    public class PlcService
    {
        private readonly ModbusRtuTransport _transport;

        public PlcService(ModbusRtuTransport transport)
        {
            _transport = transport;
        }

        public Task<PlcData> ReadDeviceAsync(DeviceConfig device, CancellationToken token)
        {
            return Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();

                ushort[] regs = _transport.ReadHoldingRegisters(device.SlaveId, 0, 5);

                return new PlcData
                {
                    DeviceName = device.Name,
                    SlaveId = device.SlaveId,
                    Status = regs[0],
                    Temperature = regs[1] / 10.0,
                    Pressure = regs[2] / 100.0,
                    Speed = regs[3],
                    UpdateTime = DateTime.Now
                };
            }, token);
        }

        public Task StartDeviceAsync(byte slaveId, CancellationToken token)
        {
            return Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();
                _transport.WriteSingleCoil(slaveId, 0, true);
            }, token);
        }

        public Task StopDeviceAsync(byte slaveId, CancellationToken token)
        {
            return Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();
                _transport.WriteSingleCoil(slaveId, 1, true);
            }, token);
        }

        public Task SetTemperatureAsync(byte slaveId, double temp, CancellationToken token)
        {
            return Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();

                ushort raw = (ushort)(temp * 10);
                _transport.WriteSingleRegister(slaveId, 2, raw);
            }, token);
        }
    }
}
