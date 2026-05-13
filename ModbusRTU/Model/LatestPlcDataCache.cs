using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModbusRTU.Model
{
    public class LatestPlcDataCache
    {
        private readonly ConcurrentDictionary<byte, PlcData> _cache = new ConcurrentDictionary<byte, PlcData>();

        public void Update(PlcData data)
        {
            _cache[data.SlaveId] = data;
        }

        public List<PlcData> GetSnapshot()
        {
            return _cache.Values.OrderBy(o => o.SlaveId).ToList();
        }
    }
}
