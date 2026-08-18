namespace Mapping_Tools.Classes.SystemTools.Platform {
    /// <summary>
    /// Shows a short message that does not stop the user.
    /// This replaces the message queue of the main window.
    /// </summary>
    public interface INotificationService {
        void Notify(string message);
    }
}
