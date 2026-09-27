using System;
using System.Threading.Tasks;
using System.Windows.Input;
using FrostySdk.Managers;
using FrostySdk.Managers.Entries;

namespace Frosty.Core
{
    public static class App
    {
        public static AssetManager AssetManager { get; set; }
        public static FrostyEditorWindow EditorWindow { get; set; }
        public static FrostyLogger Logger { get; set; }
    }

    public class FrostyEditorWindow
    {
        public Frosty.Core.Controls.FrostyDataExplorer VisibleExplorer { get; set; }
    }

    public class FrostyLogger
    {
        public void Log(string message, params object[] args) { }
        public void LogError(string message) { }
    }

    public abstract class MenuExtension
    {
        public abstract string TopLevelMenuName { get; }
        public abstract string MenuItemName { get; }
        public abstract Frosty.Core.Controls.RelayCommand MenuItemClicked { get; }
    }
}

namespace Frosty.Core.Controls
{
    public class RelayCommand : ICommand
    {
        private readonly Action<object> _execute;
        public RelayCommand(Action<object> execute) { _execute = execute; }
        public bool CanExecute(object parameter) { return true; }
        public void Execute(object parameter) { if (_execute != null) _execute(parameter); }
        public event EventHandler CanExecuteChanged { add { } remove { } }
    }

    public class FrostyDataExplorer
    {
        public string SelectedPath { get; set; }
        public Task SelectAsset(EbxAssetEntry entry, bool open) { return Task.FromResult(0); }
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
