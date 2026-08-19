using System;
using System.Collections.ObjectModel;
using System.Linq;
using Mapping_Tools.Classes.SystemTools.Platform;

namespace Mapping_Tools.Classes {
    public static class GenericExtensions
    {
        public static int RemoveAll<T>(this ObservableCollection<T> coll, Func<T, bool> condition) {
            var itemsToRemove = coll.Where(condition).ToList();

            foreach (var itemToRemove in itemsToRemove) {
                coll.Remove(itemToRemove);
            }

            return itemsToRemove.Count;
        }

        /// <summary>
        /// Shows the exception, and then each inner exception, until the user cancels.
        /// </summary>
        /// <returns>
        /// False if the user cancelled the <em>first</em> message. True in every other
        /// case, including a cancel of an inner message. This matches the behaviour
        /// before the core split, where the caller only acted on the first answer.
        /// </returns>
        public static bool Show(this Exception exception) {
            if (!CorePlatform.Dialogs.ShowOkCancel(exception.MessageStackTrace(), "Error")) return false;
            var ex = exception.InnerException;
            while (ex != null) {
                ex = CorePlatform.Dialogs.ShowOkCancel(ex.MessageStackTrace(), "Inner exception")
                    ? ex.InnerException : null;
            }

            return true;
        }

        public static string MessageStackTrace(this Exception exception) {
            return exception.Message + "\n\n" + exception.StackTrace;
        }
    }
}
