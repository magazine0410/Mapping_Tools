using System;

namespace Mapping_Tools.Components.Domain {
    /// <summary>
    /// The reason a value was refused, in a shape that shows well on a control.
    /// </summary>
    /// <remarks>
    /// Avalonia shows a data validation error by its <c>ToString</c>. A plain exception
    /// puts its type name and its stack trace in front of the message, which the user
    /// does not need. This lives in the core so that a view model can refuse a value
    /// without knowing which host is showing it.
    /// </remarks>
    public sealed class ValidationMessage : Exception {
        public ValidationMessage(string message) : base(message) { }

        public override string ToString() => Message;
    }
}
