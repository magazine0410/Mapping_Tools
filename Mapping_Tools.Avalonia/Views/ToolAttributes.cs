using System;

namespace Mapping_Tools.Avalonia.Views {
    /// <summary>Keeps a view out of the tool list.</summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public class HiddenToolAttribute : Attribute { }

    /// <summary>Stops the shell from drawing the name of the tool above it.</summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class DontShowTitleAttribute : Attribute { }

    /// <summary>Lets the content of the tool scroll sideways.</summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class HorizontalContentScrollAttribute : Attribute { }

    /// <summary>Lets the content of the tool scroll up and down.</summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class VerticalContentScrollAttribute : Attribute { }
}
