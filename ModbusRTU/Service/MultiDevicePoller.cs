using ModbusRTU.Communication.Base;
using ModbusRTU.Constants;
using ModbusRTU.Model;
using System;
using System.Collections.Generic;
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
        private Task _pollTask;

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
            Stop(waitForExit: true);

            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            _pollTask = Task.Run(() => PollLoopAsync(token));
        }

        public async Task Stop(bool waitForExit = true, int timeoutMs = 2000)
        {
            _cts?.Cancel();

            if (waitForExit && _pollTask != null)
            {
                Task completed = await Task.WhenAny(_pollTask, Task.Delay(timeoutMs));
                if (completed != _pollTask)
                {
                    
                }
            }

            _pollTask = null;
            _cts?.Dispose();
            _cts = null;
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
