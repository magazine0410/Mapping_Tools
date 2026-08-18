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
        /// <returns>True if the user accepted every message.</returns>
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
