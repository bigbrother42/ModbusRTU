using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModbusRTU.Model
{
    public class PlcDataBuffer
    {
        private readonly BlockingCollection<PlcData> _queue;

        public PlcDataBuffer(int capacity = 100)
        {
            _queue = new BlockingCollection<PlcData>(new ConcurrentQueue<PlcData>(), capacity);
        }

        public void AddLatest(PlcData data)
        {
            while (!_queue.TryAdd(data))
            {
                // 队列满了就丢弃旧的一条
                _queue.TryTake(out _);
            }
        }

        public bool TryTake(out PlcData data)
        {
            return _queue.TryTake(out data);
        }
    }
}
