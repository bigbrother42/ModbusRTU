using ModbusRTU.Communication.Base;
using ModbusRTU.Communication.Base.Serial;
using ModbusRTU.Constants;
using ModbusRTU.Model;
using ModbusRTU.Service;
using NModbus;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ModbusRTU.View
{
    public partial class FormMain : Form
    {
        private ModbusRtuTransport _transport;
        private ModbusScheduler _scheduler;
        private PlcService _deviceService;
        private MultiDevicePoller _poller;

        private readonly PlcDataBuffer _buffer= new PlcDataBuffer(100);
        private readonly LatestPlcDataCache _latestCache = new LatestPlcDataCache();

        private CancellationTokenSource _bufferConsumeCts;

        private BindingList<PlcData> _plcDataList = new BindingList<PlcData>();

        private System.Windows.Forms.Timer _uiTimer;

        public FormMain()
        {
            InitializeComponent();

            InitUi();

            InitGrid();
        }

        #region 初始化

        private void InitUi()
        {
            // 打开双缓冲
            this.DoubleBuffered = true;

            cmbPort.Items.AddRange(SerialPort.GetPortNames());

            if (cmbPort.Items.Count > 0)
                cmbPort.SelectedIndex = 0;

            cmbBaudRate.Items.AddRange(new object[]{ 9600, 19200, 38400, 115200 });

            cmbBaudRate.SelectedItem = 9600;

            txtSlaveId.Text = "1";
            txtTargetTemp.Text = "25.0";
        }

        private void InitGrid()
        {
            dataGridView.AutoGenerateColumns = true;
            dataGridView.DataSource = _plcDataList;
        }

        #endregion

        #region 连接

        private void btnTestRead_Click(object sender, EventArgs e)
        {
            try
            {
                var port = new SerialPort("COM6")
                {
                    BaudRate = 9600,
                    DataBits = 8,
                    Parity = Parity.None,
                    StopBits = StopBits.One,
                    ReadTimeout = 3000,
                    WriteTimeout = 3000,
                    RtsEnable = true,
                    DtrEnable = true,
                };

                Console.WriteLine(port.IsOpen);

                port.Open();

                var adapter = new SerialPortAdapter(port);
                var factory = new NModbus.ModbusFactory();
                var master = factory.CreateRtuMaster(adapter);

                master.Transport.ReadTimeout = 3000;
                master.Transport.WriteTimeout = 3000;
                master.Transport.Retries = 0;

                ushort[] values = master.ReadHoldingRegisters(1, 0, 5);

                MessageBox.Show(string.Join(",", values));

                master.Dispose();
                port.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private void btnConnect_Click(object sender, EventArgs e)
        {
            try
            {
                // 1. Transport
                _transport = new ModbusRtuTransport(
                    portName: cmbPort.Text,
                    baudRate: Convert.ToInt32(cmbBaudRate.SelectedItem),
                    parity: Parity.None,
                    dataBits: 8,
                    stopBits: StopBits.One);

                // 2. Scheduler
                _scheduler = new ModbusScheduler(_transport);
                _scheduler.Log += AppendLogSafe;
                _scheduler.ConnectionChanged += OnConnectionChanged;

                // 3. Service
                _deviceService = new PlcService(_transport);

                // 4. 多设备配置
                var devices = new List<DeviceConfig>
                {
                    new DeviceConfig { Name = "PLC-1", SlaveId = 1, PollIntervalMs = 100 },
                    //new DeviceConfig { Name = "温控表-2", SlaveId = 2, PollIntervalMs = 1000 },
                    //new DeviceConfig { Name = "变频器-3", SlaveId = 3, PollIntervalMs = 800 }
                };

                // 5. Poller
                _poller = new MultiDevicePoller(devices, _scheduler, _deviceService, _buffer);

                // 6. 启动
                _scheduler.Start();
                StartBufferConsumer();
                _poller.Start();
                StartUiTimer();

                AppendLogSafe("系统启动成功");
            }
            catch (Exception ex)
            {
                AppendLogSafe("连接失败：" + ex.Message);
            }
        }

        private void StartBufferConsumer()
        {
            _bufferConsumeCts = new CancellationTokenSource();

            Task.Run(() =>
            {
                while (!_bufferConsumeCts.Token.IsCancellationRequested)
                {
                    if (_buffer.TryTake(out PlcData data))
                    {
                        _latestCache.Update(data);
                    }
                    else
                    {
                        Thread.Sleep(2);
                    }
                }
            });
        }

        private void StartUiTimer()
        {
            if (_uiTimer != null) return;

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

        private void btnDisconnect_Click(object sender, EventArgs e)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() =>
                {
                    lblConnectionStatus.Text = "断开";
                }));
            }
            else
            {
                lblConnectionStatus.Text = "断开";
            }

            _poller?.Stop();
            _scheduler?.Dispose();

            AppendLogSafe("系统已断开");
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
            EnqueueCritical("启动设备", ct =>
                _deviceService.StartDeviceAsync(1, ct));
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            EnqueueCritical("停止设备", ct =>
                _deviceService.StopDeviceAsync(1, ct));
        }

        private void btnSetTemp_Click(object sender, EventArgs e)
        {
            try
            {
                double temp = double.Parse(txtTargetTemp.Text);

                _scheduler.Enqueue(new ModbusRequest
                {
                    Name = "设置温度",
                    SlaveId = 1,
                    Priority = ModbusPriority.High,
                    ExecuteAsync = ct => _deviceService.SetTemperatureAsync(1, temp, ct)
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

        private void EnqueueCritical(string name, Func<System.Threading.CancellationToken, System.Threading.Tasks.Task> action)
        {
            _scheduler.Enqueue(new ModbusRequest
            {
                Name = name,
                SlaveId = 1,
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
        }

        #endregion

        #region 关闭

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _uiTimer?.Stop();
            _uiTimer = null;

            _poller?.Stop();
            _poller = null;

            _bufferConsumeCts?.Cancel();
            _bufferConsumeCts = null;

            _scheduler?.Dispose();
            _scheduler = null;

            base.OnFormClosing(e);
        }

        #endregion
    }
}
