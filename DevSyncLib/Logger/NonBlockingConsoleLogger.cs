using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace DevSyncLib.Logger
{
    public class NonBlockingConsoleLogger : BaseLogger
    {
        private readonly struct Item
        {
            public readonly string Text;
            public readonly LogLevel Level;

            public static readonly Item EndMarker = new(null, LogLevel.Info);

            public bool IsEndMarker => Text == null;

            public Item(string text, LogLevel level)
            {
                Text = text;
                Level = level;
            }
        }

        private static readonly BlockingCollection<Item> Queue = new();

        private Task _task;

        public NonBlockingConsoleLogger(LogLevel level = DefaultLevel) : base(level)
        {
            _task = Task.Factory.StartNew(Run, TaskCreationOptions.LongRunning);
        }

        private void Run()
        {
            while (true)
            {
                var item = Queue.Take();
                if (item.IsEndMarker)
                {
                    break;
                }

                var toLog = $"[{DateTime.Now:dd.MM.yyyy HH:mm:ss}] {item.Level}: {item.Text}";

                if (item.Level == LogLevel.Error)
                {
                    Console.Error.WriteLine(toLog);
                }
                else
                {
                    Console.WriteLine(toLog);
                }
            }
        }

        protected override void AddLog(string text, LogLevel level)
        {
            Queue.Add(new Item(text, level));
        }

        public override void Dispose()
        {
            base.Dispose();

            if (_task != null)
            {
                Queue.Add(Item.EndMarker);
                _task.Wait();
                _task = null;
            }
        }
    }
}
