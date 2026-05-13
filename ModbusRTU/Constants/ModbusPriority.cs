using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModbusRTU.Constants
{
    public enum ModbusPriority
    {
        Low = 0,      // 周期轮询
        Normal = 1,   // 普通读取
        High = 2,     // 写入设定值
        Critical = 3  // 急停、复位、启动停止
    }
}
