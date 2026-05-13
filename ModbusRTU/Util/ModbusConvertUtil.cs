using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace ModbusRTU.Util
{
    public class ModbusConvertUtil
    {
        public static int ToInt32_ABCD(ushort high, ushort low)
        {
            // 用两个16位数字拼成一个32位数字。
            // high = 前半部分; low  = 后半部分
            // high << 16   <=>   high * 2^16
            return (high << 16) | low;
        }

        public static float ToFloat_ABCD(ushort high, ushort low)
        {
            byte[] bytes = new byte[4];

            // 注意：Windows 是小端，所以这里要反过来组装
            bytes[0] = (byte)(low & 0xFF);
            bytes[1] = (byte)(low >> 8);
            bytes[2] = (byte)(high & 0xFF);
            bytes[3] = (byte)(high >> 8);

            return BitConverter.ToSingle(bytes, 0);
        }

        public static ushort[] FromFloat_ABCD(float value)
        {
            // C#小端（低字节放前面）内存：79 E9 F6 42
            byte[] bytes = BitConverter.GetBytes(value);

            ushort low = (ushort)(bytes[0] | (bytes[1] << 8));
            ushort high = (ushort)(bytes[2] | (bytes[3] << 8));

            // PLC真实数据：42 F6 E9 79
            return new[] { high, low };
        }
    }
}
