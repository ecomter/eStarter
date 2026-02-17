using System;
using System.Windows;
using System.Windows.Threading;

namespace eStarter.Services
{
    /// <summary>
    /// WPF implementation of IDispatcherService using Application.Current.Dispatcher.
    /// </summary>
    public sealed class WpfDispatcherService : IDispatcherService
    {
        public void Invoke(Action action)
        {
            if (Application.Current?.Dispatcher is Dispatcher d)
                d.Invoke(action);
            else
                action();
        }

        public void BeginInvoke(Action action)
        {
            if (Application.Current?.Dispatcher is Dispatcher d)
                d.BeginInvoke(action);
            else
                action();
        }
    }
}
