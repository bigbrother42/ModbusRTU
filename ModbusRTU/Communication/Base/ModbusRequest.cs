using ModbusRTU.Constants;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ModbusRTU.Communication.Base
{
    public class ModbusRequest
    {
        /// <summary>
        /// 设备名字
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 设备ID
        /// </summary>
        public byte SlaveId { get; set; }

        /// <summary>
        /// 优先级
        /// </summary>
        public ModbusPriority Priority { get; set; }

        /// <summary>
        /// 请求的执行动作（读/写）
        /// </summary>
        public Func<CancellationToken, Task> ExecuteAsync { get; set; }

        /// <summary>
        /// 请求创建时间
        /// </summary>
        public DateTime CreateTime { get; set; } = DateTime.Now;
    }
}
