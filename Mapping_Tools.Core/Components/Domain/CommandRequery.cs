using System;

namespace Mapping_Tools.Components.Domain {
    /// <summary>
    /// Connects <see cref="CommandImplementation"/> to the requery mechanism of the host.
    /// WPF sets these to <c>CommandManager</c>. A host with no user interface leaves
    /// them as they are, and then nothing happens.
    /// </summary>
    public static class CommandRequery {
        public static Action<EventHandler> Subscribe { get; set; } = _ => { };
        public static Action<EventHandler> Unsubscribe { get; set; } = _ => { };
        public static Action Invalidate { get; set; } = () => { };
    }
}
