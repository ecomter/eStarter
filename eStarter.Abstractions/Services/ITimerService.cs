using System;
using System.ComponentModel;
using System.Windows.Input;

namespace eStarter.Services
{
    /// <summary>
    /// Cross-platform timer abstraction. Replaces WPF DispatcherTimer.
    /// </summary>
    public interface ITimerService
    {
        void Start(TimeSpan interval, Action callback);
        void Stop();
    }
}
