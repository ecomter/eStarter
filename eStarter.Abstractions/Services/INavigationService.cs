namespace eStarter.Services
{
    /// <summary>
    /// Abstracts page/view navigation. Each UI platform provides its own implementation.
    /// </summary>
    public interface INavigationService
    {
        void NavigateTo(string pageKey);
        void GoBack();
        bool CanGoBack { get; }
    }
}
