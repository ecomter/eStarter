using System;
using System.Windows;
using System.Windows.Threading;

namespace eStarter.Services
{
    /// <summary>
    /// WPF implementation of ITimerService using DispatcherTimer.
    /// </summary>
    public sealed class WpfTimerService : ITimerService
    {
        private DispatcherTimer? _timer;

        public void Start(TimeSpan interval, Action callback)
        {
            Stop();
            _timer = new DispatcherTimer { Interval = interval };
            _timer.Tick += (s, e) => callback();
            _timer.Start();
        }

        public void Stop()
        {
            _timer?.Stop();
            _timer = null;
        }
    }
}
