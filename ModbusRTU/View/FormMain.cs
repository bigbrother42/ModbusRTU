using ModbusRTU.Communication.Base;
using ModbusRTU.Communication.Base.Serial;
using ModbusRTU.Constants;
using ModbusRTU.Model;
using ModbusRTU.Service;
using NModbus;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ModbusRTU.View
{
    public partial class FormMain : Form
    {
        private const int MaxLogLines = 800;

        private ModbusRtuTransport _transport;
        private ModbusScheduler _scheduler;
        private PlcService _deviceService;
        private MultiDevicePoller _poller;

        private readonly PlcDataBuffer _buffer = new PlcDataBuffer(100);
        private readonly LatestPlcDataCache _latestCache = new LatestPlcDataCache();

        private CancellationTokenSource _bufferConsumeCts;
        private Task _bufferConsumeTask;

        private BindingList<PlcData> _plcDataList = new BindingList<PlcData>();

        private System.Windows.Forms.Timer _uiTimer;

        private bool _sessionRunning;
        private bool _isClosing;

        public FormMain()
        {
            InitializeComponent();

            InitUi();

            InitGrid();
        }

        #region 初始化

        private void InitUi()
        {
            this.DoubleBuffered = true;

            cmbPort.Items.AddRange(SerialPort.GetPortNames());

            if (cmbPort.Items.Count > 0)
                cmbPort.SelectedIndex = 0;

            cmbBaudRate.Items.AddRange(new object[] { 9600, 19200, 38400, 115200 });

            cmbBaudRate.SelectedItem = 9600;

            txtSlaveId.Text = "1";
            txtTargetTemp.Text = "25.0";

            UpdateConnectionButtons();
        }

        private void InitGrid()
        {
            dataGridView.AutoGenerateColumns = true;
            dataGridView.DataSource = _plcDataList;
        }

        private void UpdateConnectionButtons()
        {
            btnConnect.Enabled = !_sessionRunning;
            btnDisconnect.Enabled = _sessionRunning;
            cmbPort.Enabled = !_sessionRunning;
            cmbBaudRate.Enabled = !_sessionRunning;
            txtSlaveId.Enabled = !_sessionRunning;

            btnStart.Enabled = _sessionRunning;
            btnStop.Enabled = _sessionRunning;
            btnSetTemp.Enabled = _sessionRunning;

            if (!_sessionRunning)
                lblConnectionStatus.Text = "断开";
        }

        #endregion

        #region 连接

        private bool TryParseSlaveId(out byte slaveId)
        {
            slaveId = 0;
            if (!byte.TryParse(txtSlaveId.Text.Trim(), out var id) || id < 1 || id > 247)
                return false;
            slaveId = id;
            return true;
        }

        private bool TryGetSelectedBaudRate(out int baudRate)
        {
            baudRate = 0;
            var sel = cmbBaudRate.SelectedItem;
            if (sel is int b)
            {
                baudRate = b;
                return true;
            }

            return int.TryParse(Convert.ToString(sel), out baudRate);
        }

        private async Task StopSession()
        {
            _sessionRunning = false;

            // 停止Poller并等待
            if (_poller != null)
            {
                await _poller.Stop(waitForExit: true, 2000);
                _poller = null;
            }

            // 取消并等待缓冲消费任务
            _bufferConsumeCts?.Cancel();
            if (_bufferConsumeTask != null)
            {
                Task completed = Task.WhenAny(_bufferConsumeTask, Task.Delay(2000));
                if (completed != _bufferConsumeTask)
                {
                    AppendLogSafe("Consume Buffer停止超时！");
                }
            }

            _bufferConsumeTask = null;
            _bufferConsumeCts?.Dispose();
            _bufferConsumeCts = null;

            // 取消订阅并销毁Scheduler
            if (_scheduler != null)
            {
                _scheduler.Log -= AppendLogSafe;
                _scheduler.ConnectionChanged -= OnConnectionChanged;
                await _scheduler.StopAsync(3000);
                _scheduler.Dispose();
                _scheduler = null;
            }

            _transport = null;
            _deviceService = null;

            // 更新_sessionRunning和按钮状态
            if (InvokeRequired)
                BeginInvoke(new Action(UpdateConnectionButtons));
            else
                UpdateConnectionButtons();
        }

        private async void btnConnect_Click(object sender, EventArgs e)
        {
            if (_sessionRunning)
            {
                AppendLogSafe("会话已在运行，请先断开。");
                return;
            }

            if (string.IsNullOrWhiteSpace(cmbPort.Text))
            {
                AppendLogSafe("请选择串口。");
                return;
            }

            if (!TryParseSlaveId(out byte slaveId))
            {
                AppendLogSafe("SlaveId 无效，请输入 1～247。");
                return;
            }

            if (!TryGetSelectedBaudRate(out int baudRate))
            {
                AppendLogSafe("波特率无效。");
                return;
            }

            try
            {
                // 避免重复链接遗留后台任务
                await StopSession();

                _transport = new ModbusRtuTransport(
                    portName: cmbPort.Text.Trim(),
                    baudRate: baudRate,
                    parity: Parity.None,
                    dataBits: 8,
                    stopBits: StopBits.One);

                _scheduler = new ModbusScheduler(_transport);
                _scheduler.Log += AppendLogSafe;
                _scheduler.ConnectionChanged += OnConnectionChanged;

                _deviceService = new PlcService(_transport);

                var devices = new List<DeviceConfig>
                {
                    new DeviceConfig { Name = $"PLC-{slaveId}", SlaveId = slaveId, PollIntervalMs = 100 },
                };

                _poller = new MultiDevicePoller(devices, _scheduler, _deviceService, _buffer);

                _scheduler.Start();
                StartBufferConsumer();
                _poller.Start();
                StartUiTimer();

                // _sessionRunning为真时，禁用【链接】按钮等，启用【断开】、与设温按钮
                _sessionRunning = true;
                UpdateConnectionButtons();

                AppendLogSafe("系统启动成功");
            }
            catch (Exception ex)
            {
                await StopSession();
                AppendLogSafe("连接失败：" + ex.Message);
            }
        }

        private void StartBufferConsumer()
        {
            _bufferConsumeCts = new CancellationTokenSource();
            var token = _bufferConsumeCts.Token;

            _bufferConsumeTask = Task.Run(() =>
            {
                while (!token.IsCancellationRequested)
                {
                    if (_buffer.TryTake(out PlcData data, 100))
                        _latestCache.Update(data);
                }
            }, token);
        }

        private void StartUiTimer()
        {
            if (_uiTimer != null)
                return;

            _uiTimer = new System.Windows.Forms.Timer();
            _uiTimer.Interval = 500;
            _uiTimer.Tick += UiTimer_Tick;
            _uiTimer.Start();
        }

        private void UiTimer_Tick(object sender, EventArgs e)
        {
            var snapshot = _latestCache.GetSnapshot();

            foreach (var data in snapshot)
            {
                var existItem = _plcDataList.FirstOrDefault(o => o.SlaveId == data.SlaveId);

                if (existItem == null)
                {
                    _plcDataList.Add(data);
                }
                else
                {
                    existItem.Pressure = data.Pressure;
                    existItem.Temperature = data.Temperature;
                    existItem.Status = data.Status;
                    existItem.Speed = data.Speed;
                    existItem.UpdateTime = data.UpdateTime;
                }
            }
        }

        private async void btnDisconnect_Click(object sender, EventArgs e)
        {
            try
            {
                await StopSession();
                AppendLogSafe("系统已断开");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void OnConnectionChanged(bool connected)
        {
            AppendLogSafe(connected ? "连接状态：已连接" : "连接状态：断开");

            if (InvokeRequired)
            {
                BeginInvoke(new Action(() =>
                {
                    lblConnectionStatus.Text = connected ? "已连接" : "断开";
                }));
            }
            else
            {
                lblConnectionStatus.Text = connected ? "已连接" : "断开";
            }
        }

        #endregion

        #region 按钮操作（高优先级）

        private void btnStart_Click(object sender, EventArgs e)
        {
            if (!TryParseSlaveId(out byte slaveId))
            {
                AppendLogSafe("SlaveId 无效。");
                return;
            }

            EnqueueCritical("启动设备", slaveId, ct =>
                _deviceService.StartDeviceAsync(slaveId, ct));
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            if (!TryParseSlaveId(out byte slaveId))
            {
                AppendLogSafe("SlaveId 无效。");
                return;
            }

            EnqueueCritical("停止设备", slaveId, ct =>
                _deviceService.StopDeviceAsync(slaveId, ct));
        }

        private void btnSetTemp_Click(object sender, EventArgs e)
        {
            try
            {
                if (!TryParseSlaveId(out byte slaveId))
                {
                    AppendLogSafe("SlaveId 无效。");
                    return;
                }

                double temp = double.Parse(txtTargetTemp.Text);

                _scheduler.Enqueue(new ModbusRequest
                {
                    Name = "设置温度",
                    SlaveId = slaveId,
                    Priority = ModbusPriority.High,
                    ExecuteAsync = ct => _deviceService.SetTemperatureAsync(slaveId, temp, ct)
                });

                AppendLogSafe($"温度设定请求已加入队列：{temp}");
            }
            catch (Exception ex)
            {
                AppendLogSafe("输入错误：" + ex.Message);
            }
        }

        #endregion

        #region 请求封装

        private void EnqueueCritical(string name, byte slaveId, Func<CancellationToken, Task> action)
        {
            if (_scheduler == null || !_sessionRunning)
            {
                AppendLogSafe("未连接，无法发送命令。");
                return;
            }

            _scheduler.Enqueue(new ModbusRequest
            {
                Name = name,
                SlaveId = slaveId,
                Priority = ModbusPriority.Critical,
                ExecuteAsync = action
            });

            AppendLogSafe($"{name} 请求已加入队列（高优先级）");
        }

        #endregion

        #region 日志

        private void AppendLogSafe(string message)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => AppendLogSafe(message)));
                return;
            }

            txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\r\n");

            // 超过800行则截掉前面多余行
            var lines = txtLog.Lines;
            if (lines.Length > MaxLogLines)
            {
                int skip = lines.Length - MaxLogLines;
                txtLog.Lines = lines.Skip(skip).ToArray();
                txtLog.SelectionStart = txtLog.TextLength;
                txtLog.ScrollToCaret();
            }
        }

        #endregion

        #region 关闭

        protected async override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!_isClosing)
            {
                e.Cancel = true;

                try
                {
                    _uiTimer?.Stop();
                    _uiTimer?.Dispose();
                    _uiTimer = null;

                    await StopSession();
                }
                catch (Exception ex)
                {

                }
                finally
                {
                    _isClosing = true;
                    Close();
                }

                return;
            }

            base.OnFormClosing(e);
        }

        #endregion
    }
}
