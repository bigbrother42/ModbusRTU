using System;
using System.Collections.Generic;

namespace ModbusRTU.Communication.Base
{
    /// <summary>
    /// 在请求队列中增加锁lock机制，是为了保证同一时刻只有一个请求访问串口。
    /// 队列有上限：满时按优先级丢弃最旧请求（低优先级先丢），避免异常设备把内存撑爆。
    /// </summary>
    public class ModbusRequestQueue
    {
        private readonly object _lock = new object();
        private readonly List<ModbusRequest> _requests = new List<ModbusRequest>();
        private readonly int _maxLength;

        public event Action<string> RequestDropped;

        public ModbusRequestQueue(int maxLength = 256)
        {
            if (maxLength < 8) maxLength = 8;

            _maxLength = maxLength;
        }

        public void Enqueue(ModbusRequest request)
        {
            if (request == null)
                return;

            lock (_lock)
            {
                while (_requests.Count >= _maxLength)
                {
                    var victim = FindLowestPriorityOldest();
                    if (victim == null) break;

                    _requests.Remove(victim);
                    RequestDropped?.Invoke($"队列已满（上限 {_maxLength}），丢弃：{victim.Name}（{victim.Priority}）");
                }

                if (_requests.Count >= _maxLength)
                {
                    RequestDropped?.Invoke($"队列已满，跳过入队：{request.Name}（{request.Priority}）");
                    return;
                }

                _requests.Add(request);
            }
        }

        private ModbusRequest FindLowestPriorityOldest()
        {
            ModbusRequest best = null;
            foreach (var r in _requests)
            {
                if (r.Priority == Constants.ModbusPriority.Critical) continue;
                if (r.Priority == Constants.ModbusPriority.High) continue;
                if (r.Priority == Constants.ModbusPriority.Normal) continue;

                if (best == null
                    || r.Priority < best.Priority
                    || (r.Priority == best.Priority && r.CreateTime < best.CreateTime))
                {
                    best = r;
                }
            }
            return best;
        }

        public bool TryDequeue(out ModbusRequest request)
        {
            lock (_lock)
            {
                request = null;

                if (_requests.Count == 0) return false;

                ModbusRequest best = null;
                foreach (var r in _requests)
                {
                    if (best == null
                        || r.Priority > best.Priority
                        || (r.Priority == best.Priority && r.CreateTime < best.CreateTime))
                    {
                        best = r;
                    }
                }

                request = best;
                _requests.Remove(best);
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
