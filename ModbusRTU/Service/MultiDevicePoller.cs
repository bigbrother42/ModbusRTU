using ModbusRTU.Communication.Base;
using ModbusRTU.Constants;
using ModbusRTU.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ModbusRTU.Service
{
    public class MultiDevicePoller
    {
        private readonly List<DeviceConfig> _devices;
        private readonly ModbusScheduler _scheduler;
        private readonly PlcService _deviceService;
        private readonly PlcDataBuffer _buffer;

        private CancellationTokenSource _cts;

        public MultiDevicePoller(
            List<DeviceConfig> devices,
            ModbusScheduler scheduler,
            PlcService deviceService,
            PlcDataBuffer buffer)
        {
            _devices = devices;
            _scheduler = scheduler;
            _deviceService = deviceService;
            _buffer = buffer;
        }

        public void Start()
        {
            _cts = new CancellationTokenSource();
            Task.Run(() => PollLoopAsync(_cts.Token));
        }

        public void Stop()
        {
            _cts?.Cancel();
        }

        private async Task PollLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                foreach (var device in _devices)
                {
                    if ((DateTime.Now - device.LastPollTime).TotalMilliseconds >= device.PollIntervalMs)
                    {
                        device.LastPollTime = DateTime.Now;

                        var currentDevice = device;

                        _scheduler.Enqueue(new ModbusRequest
                        {
                            Name = $"轮询设备 {currentDevice.Name}",
                            SlaveId = currentDevice.SlaveId,
                            Priority = ModbusPriority.Low,
                            ExecuteAsync = async ct =>
                            {
                                PlcData data = await _deviceService.ReadDeviceAsync(currentDevice, ct);
                                _buffer.AddLatest(data);
                            }
                        });
                    }
                }

                await Task.Delay(10, token);
            }
        }
    }
}
