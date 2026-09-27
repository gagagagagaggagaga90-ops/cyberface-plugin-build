using System;
using System.Reflection;
using System.Windows.Input;

[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace Frosty.Core
{
    public class RelayCommand : ICommand
    {
        private readonly Action<object> _execute;
        public RelayCommand(Action<object> execute) { _execute = execute; }
        public RelayCommand(Action<object> execute, Predicate<object> canExecute) { _execute = execute; }
        public bool CanExecute(object parameter) { return true; }
        public void Execute(object parameter) { if (_execute != null) _execute(parameter); }
        public event EventHandler CanExecuteChanged { add { } remove { } }
    }

    public abstract class MenuExtension
    {
        public virtual string TopLevelMenuName { get { return null; } }
        public virtual string SubLevelMenuName { get { return null; } }
        public virtual string MenuItemName { get { return null; } }
        public virtual System.Windows.Media.ImageSource Icon { get { return null; } }
        public virtual RelayCommand MenuItemClicked { get { return null; } }
    }
}

namespace Frosty.Core.Attributes
{
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
    public sealed class PluginDisplayNameAttribute : Attribute
    {
        public PluginDisplayNameAttribute(string value) { }
    }

    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
    public sealed class PluginAuthorAttribute : Attribute
    {
        public PluginAuthorAttribute(string value) { }
    }

    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
    public sealed class PluginVersionAttribute : Attribute
    {
        public PluginVersionAttribute(string value) { }
    }

    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public sealed class RegisterMenuExtensionAttribute : Attribute
    {
        public RegisterMenuExtensionAttribute(Type type) { }
    }
}
