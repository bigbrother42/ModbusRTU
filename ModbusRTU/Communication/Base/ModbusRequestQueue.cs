using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModbusRTU.Communication.Base
{
    /// <summary>
    /// 在请求队列中增加锁lock机制，是为了保证同一时刻只有一个请求访问串口
    /// </summary>
    public class ModbusRequestQueue
    {
        private readonly object _lock = new object();
        private readonly List<ModbusRequest> _requests = new List<ModbusRequest>();

        public void Enqueue(ModbusRequest request)
        {
            lock (_lock)
            {
                _requests.Add(request);
            }
        }

        public bool TryDequeue(out ModbusRequest request)
        {
            lock (_lock)
            {
                request = null;

                if (_requests.Count == 0)
                    return false;

                request = _requests
                    .OrderByDescending(x => x.Priority)
                    .ThenBy(x => x.CreateTime)
                    .First();

                _requests.Remove(request);
                return true;
            }
        }

        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return _requests.Count;
                }
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _requests.Clear();
            }
        }
    }
}
