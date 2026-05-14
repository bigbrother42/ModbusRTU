using ModbusRTU.Communication.Base.Serial;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ModbusRTU.Communication.Base
{
    public class ModbusScheduler : IDisposable
    {
        private readonly ModbusRtuTransport _transport;
        private readonly ModbusRequestQueue _queue;

        private CancellationTokenSource _cts;
        private Task _workerTask;

        private int _continuousFailCount = 0;
        private const int MaxFailCount = 3;

        public event Action<string> Log;
        public event Action<bool> ConnectionChanged;

        public ModbusScheduler(ModbusRtuTransport transport, int requestQueueMaxLength = 256)
        {
            _transport = transport;
            _queue = new ModbusRequestQueue(requestQueueMaxLength);
            _queue.RequestDropped += msg => Log?.Invoke(msg);
        }

        public int RequestQueueCount => _queue.Count;

        public void Start()
        {
            if (_workerTask != null && !_workerTask.IsCompleted)
            {
                Log?.Invoke("调度器已在运行，请先停止后再启动。");
                return;
            }

            _cts?.Dispose();
            _cts = new CancellationTokenSource();

            TryConnect();

            _workerTask = Task.Run(() => WorkerLoopAsync(_cts.Token));
        }

        public async Task StopAsync(int timeoutMs = 3000)
        {
            _cts?.Cancel();
            _queue.Clear();
            _transport.Disconnect();

            if (_workerTask != null)
            {
                Task completed = await Task.WhenAny(_workerTask, Task.Delay(timeoutMs));
                if (completed != _workerTask)
                {
                    Log?.Invoke("Scheduler停止超时！");
                }
            }

            _workerTask = null;
            _cts?.Dispose();
            _cts = null;
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

                            Log?.Invoke($"请求失败：{request.Name}，{FormatCommException(ex)}");

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

        private static string FormatCommException(Exception ex)
        {
            var e = ex.GetBaseException();

            if (e is TimeoutException)
                return "类型：超时（设备无响应或响应过慢）";
            if (e is UnauthorizedAccessException)
                return "类型：串口被占用或无权限";
            if (e is IOException)
                return "类型：IO/串口异常（可能已拔线）";
            if (e is InvalidOperationException)
                return "类型：未连接或非法操作";
            return "原因：" + e.Message;
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

        /// <summary>
        /// 释放串口与 Modbus 资源。调用方须已先通过 <see cref="StopAsync"/> 停止后台 worker（例如窗体 <c>StopSession</c> 中的顺序）。
        /// </summary>
        public void Dispose()
        {
            _transport.Dispose();
        }
    }
}
