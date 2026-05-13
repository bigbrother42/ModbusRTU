using ModbusRTU.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModbusRTU.Model
{
    public class PlcData : ObservableObject
    {
        /// <summary>
        /// 设备名
        /// </summary>
        public string DeviceName { get; set; }
        /// <summary>
        /// 设备ID
        /// </summary>
        public byte SlaveId { get; set; }

        /// <summary>
        /// 状态
        /// </summary>
        private int _status;
        public int Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }

        /// <summary>
        /// 温度
        /// </summary>
        private double _temperature;
        public double Temperature
        { 
            get => _temperature;
            set => SetProperty(ref _temperature, value);
        }

        /// <summary>
        /// 压力
        /// </summary>
        private double _pressure;
        public double Pressure
        {
            get => _pressure;
            set => SetProperty(ref _pressure, value);
        }

        /// <summary>
        /// 速度
        /// </summary>
        private int _speed;
        public int Speed
        {
            get => _speed;
            set => SetProperty(ref _speed, value);
        }

        /// <summary>
        /// 更新时间
        /// </summary>
        private DateTime _updateTime;
        public DateTime UpdateTime
        {
            get => _updateTime;
            set => SetProperty(ref _updateTime, value);
        }
    }
}
