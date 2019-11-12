using System;
using System.Threading;

namespace DevSyncLib
{
    public class ConditionVariable
    {
        public void Notify()
        {
            lock (this)
            {
                Monitor.Pulse(this);
            }
        }

        // use lock(this) before call
        public void Wait(int timeout = Timeout.Infinite)
        {
            Monitor.Wait(this, timeout);
        }

        // change condition in lock(this)
        public void WaitForCondition(Func<bool> conditionFunc, int timeout = Timeout.Infinite)
        {
            while (!conditionFunc())
            {
                lock (this)
                {
                    if (!conditionFunc())
                    {
                        Monitor.Wait(this, timeout);
                    }
                }
            }
        }
    }
}
