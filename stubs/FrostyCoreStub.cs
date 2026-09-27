using System;
using System.Windows.Input;
using FrostySdk.Managers;

namespace Frosty.Core
{
    public static class App
    {
        public static AssetManager AssetManager { get; set; }
        public static object EditorWindow { get; set; }
        public static object Logger { get; set; }
    }

    public sealed class RelayCommand : ICommand
    {
        private readonly Action<object> _execute;
        public RelayCommand(Action<object> execute) { _execute = execute; }
        public bool CanExecute(object parameter) { return true; }
        public void Execute(object parameter) { if (_execute != null) _execute(parameter); }
        public event EventHandler CanExecuteChanged { add { } remove { } }
    }

    public abstract class MenuExtension
    {
        public abstract string TopLevelMenuName { get; }
        public abstract string MenuItemName { get; }
        public abstract RelayCommand MenuItemClicked { get; }
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
