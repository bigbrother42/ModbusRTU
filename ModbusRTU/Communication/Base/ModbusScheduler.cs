using ModbusRTU.Communication.Base.Serial;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ModbusRTU.Communication.Base
{
    public class ModbusScheduler : IDisposable
    {
        private readonly ModbusRtuTransport _transport;
        private readonly ModbusRequestQueue _queue = new ModbusRequestQueue();

        private CancellationTokenSource _cts;
        private Task _workerTask;

        private int _continuousFailCount = 0;
        private const int MaxFailCount = 3;

        public event Action<string> Log;
        public event Action<bool> ConnectionChanged;

        public ModbusScheduler(ModbusRtuTransport transport)
        {
            _transport = transport;
        }

        public void Start()
        {
            _cts = new CancellationTokenSource();

            TryConnect();

            _workerTask = Task.Run(() => WorkerLoopAsync(_cts.Token));
        }

        public void Stop()
        {
            _cts?.Cancel();
            _queue.Clear();
            _transport.Disconnect();
        }

        public void Enqueue(ModbusRequest request)
        {
            _queue.Enqueue(request);
        }

        private async Task WorkerLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (!_transport.IsConnected)
                    {
                        await ReconnectAsync(token);
                        await Task.Delay(500, token);
                        continue;
                    }

                    if (_queue.TryDequeue(out ModbusRequest request))
                    {
                        try
                        {
                            Log?.Invoke($"执行请求：{request.Name}，优先级：{request.Priority}");

                            await request.ExecuteAsync(token);

                            _continuousFailCount = 0;
                        }
                        catch (Exception ex)
                        {
                            _continuousFailCount++;

                            Log?.Invoke($"请求失败：{request.Name}，原因：{ex.Message}");

                            if (_continuousFailCount >= MaxFailCount)
                            {
                                Log?.Invoke("连续通信失败，断开连接，准备重连");
                                _transport.Disconnect();
                                ConnectionChanged?.Invoke(false);
                            }
                        }
                    }
                    else
                    {
                        await Task.Delay(20, token);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Log?.Invoke("调度器异常：" + ex.Message);
                    await Task.Delay(500, token);
                }
            }
        }

        private void TryConnect()
        {
            try
            {
                _transport.Connect();
                _continuousFailCount = 0;

                Log?.Invoke("Modbus 连接成功");
                ConnectionChanged?.Invoke(true);
            }
            catch (Exception ex)
            {
                Log?.Invoke("Modbus 连接失败：" + ex.Message);
                ConnectionChanged?.Invoke(false);
            }
        }

        private async Task ReconnectAsync(CancellationToken token)
        {
            while (!_transport.IsConnected && !token.IsCancellationRequested)
            {
                try
                {
                    Log?.Invoke("尝试重连 Modbus...");
                    _transport.Connect();

                    _continuousFailCount = 0;
                    Log?.Invoke("重连成功");
                    ConnectionChanged?.Invoke(true);
                }
                catch (Exception ex)
                {
                    Log?.Invoke("重连失败：" + ex.Message);
                    ConnectionChanged?.Invoke(false);

                    await Task.Delay(2000, token);
                }
            }
        }

        public void Dispose()
        {
            Stop();
            _transport.Dispose();
        }
    }
}
