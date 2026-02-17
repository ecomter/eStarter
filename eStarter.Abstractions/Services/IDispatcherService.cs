using System;

namespace eStarter.Services
{
    /// <summary>
    /// Abstracts UI thread dispatching. WPF, Avalonia, MAUI etc. each provide their own implementation.
    /// </summary>
    public interface IDispatcherService
    {
        void Invoke(Action action);
        void BeginInvoke(Action action);
    }
}
