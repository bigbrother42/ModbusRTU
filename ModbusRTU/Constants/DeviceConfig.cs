using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModbusRTU.Constants
{
    public class DeviceConfig
    {
        /// <summary>
        /// 设备名
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 从设备ID
        /// </summary>
        public byte SlaveId { get; set; }

        /// <summary>
        /// 设备轮询间隔（毫秒）
        /// </summary>
        public int PollIntervalMs { get; set; }

        /// <summary>
        /// 最后查询的时间
        /// </summary>
        public DateTime LastPollTime { get; set; } = DateTime.MinValue;
    }
}
