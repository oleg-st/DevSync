using System;
using System.Threading;

namespace DevSyncLib
{
    public class ConditionVariable
    {
        private readonly object _syncObject = new object();

        public void Notify()
        {
            lock (_syncObject)
            {
                Monitor.Pulse(_syncObject);
            }
        }

        public void Wait(int timeout = Timeout.Infinite)
        {
            lock (_syncObject)
            {
                Monitor.Wait(_syncObject, timeout);
            }
        }

        public void WaitForCondition(Func<bool> conditionFunc, int timeout = Timeout.Infinite)
        {
            while (!conditionFunc())
            {
                Wait(timeout);
            }
        }
    }
}
