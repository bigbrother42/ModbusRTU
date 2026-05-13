using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModbusRTU.Constants
{
    public class PlcAddress
    {
        // Holding Registers
        public const ushort Status = 0;          // 40001
        public const ushort CurrentTemp = 1;     // 40002
        public const ushort TargetTemp = 2;      // 40003
        public const ushort Pressure = 3;        // 40004
        public const ushort Speed = 4;           // 40005
        public const ushort AlarmCode = 5;       // 40006

        public const ushort TotalCount = 9;      // 40010~40011
        public const ushort Flow = 19;           // 40020~40021

        // Coils
        public const ushort Start = 0;           // 00001
        public const ushort Stop = 1;            // 00002
        public const ushort ResetAlarm = 2;      // 00003
    }
}
