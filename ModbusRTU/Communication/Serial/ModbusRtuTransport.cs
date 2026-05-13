using NModbus;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModbusRTU.Communication.Base.Serial
{
    public class ModbusRtuTransport : IDisposable
    {
        private readonly object _lock = new object();

        private SerialPort _serialPort;
        private IModbusMaster _master;
        private SerialPortAdapter _adapter;

        private readonly string _portName;
        private readonly int _baudRate;
        private readonly Parity _parity;
        private readonly int _dataBits;
        private readonly StopBits _stopBits;

        public bool IsConnected =>
            _serialPort != null && _serialPort.IsOpen && _master != null;

        public ModbusRtuTransport(
            string portName,
            int baudRate,
            Parity parity,
            int dataBits,
            StopBits stopBits)
        {
            _portName = portName;
            _baudRate = baudRate;
            _parity = parity;
            _dataBits = dataBits;
            _stopBits = stopBits;
        }

        public void Connect()
        {
            lock (_lock)
            {
                Disconnect();

                _serialPort = new SerialPort(_portName)
                {
                    BaudRate = _baudRate,
                    Parity = _parity,
                    DataBits = _dataBits,
                    StopBits = _stopBits,
                    ReadTimeout = 1000,
                    WriteTimeout = 1000
                };

                _serialPort.Open();

                _adapter = new SerialPortAdapter(_serialPort);

                var factory = new ModbusFactory();
                _master = factory.CreateRtuMaster(_adapter);

                _master.Transport.Retries = 0;
                _master.Transport.ReadTimeout = 1000;
                _master.Transport.WriteTimeout = 1000;
            }
        }

        public ushort[] ReadHoldingRegisters(byte slaveId, ushort start, ushort count)
        {
            lock (_lock)
            {
                EnsureConnected();
                // ReadHoldingRegisters()
                // 构建 Modbus RTU 请求帧 -> _serialPort.Write(...)发请求 -> _serialPort.Read(...)等待PLC返回 -> 解析响应帧 -> 返回 ushort[]
                return _master.ReadHoldingRegisters(slaveId, start, count);
            }
        }

        public bool[] ReadCoils(byte slaveId, ushort start, ushort count)
        {
            lock (_lock)
            {
                EnsureConnected();
                return _master.ReadCoils(slaveId, start, count);
            }
        }

        public void WriteSingleCoil(byte slaveId, ushort address, bool value)
        {
            lock (_lock)
            {
                EnsureConnected();
                _master.WriteSingleCoil(slaveId, address, value);
            }
        }

        public void WriteSingleRegister(byte slaveId, ushort address, ushort value)
        {
            lock (_lock)
            {
                EnsureConnected();
                _master.WriteSingleRegister(slaveId, address, value);
            }
        }

        public void WriteMultipleRegisters(byte slaveId, ushort start, ushort[] values)
        {
            lock (_lock)
            {
                EnsureConnected();
                _master.WriteMultipleRegisters(slaveId, start, values);
            }
        }

        public void Disconnect()
        {
            lock (_lock)
            {
                try
                {
                    _master?.Dispose();
                    _master = null;

                    _adapter = null;

                    if (_serialPort != null)
                    {
                        if (_serialPort.IsOpen)
                            _serialPort.Close();

                        _serialPort.Dispose();
                        _serialPort = null;
                    }
                }
                catch
                {
                }
            }
        }

        private void EnsureConnected()
        {
            if (!IsConnected)
                throw new InvalidOperationException("Modbus RTU 未连接");
        }

        public void Dispose()
        {
            Disconnect();
        }
    }
}
