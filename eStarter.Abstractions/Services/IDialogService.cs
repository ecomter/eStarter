using System.Threading.Tasks;

namespace eStarter.Services
{
    public enum DialogButton { OK, OKCancel, YesNo }
    public enum DialogResult { None, OK, Cancel, Yes, No }

    /// <summary>
    /// Abstracts modal dialogs. Removes dependency on WPF MessageBox / ModernMsgBox.
    /// </summary>
    public interface IDialogService
    {
        Task<DialogResult> ShowMessageAsync(string message, string title, DialogButton buttons = DialogButton.OK);
        Task<bool> ShowPermissionRequestAsync(string appId, string permissionName, string appName);
    }
}
