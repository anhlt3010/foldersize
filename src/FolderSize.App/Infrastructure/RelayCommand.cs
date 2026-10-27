using System.Windows.Input;
namespace FolderSize.App.Infrastructure;
public sealed class RelayCommand(Func<object?, Task> execute, Func<object?, bool>? canExecute = null) : ICommand
{
    public event EventHandler? CanExecuteChanged { add => CommandManager.RequerySuggested += value; remove => CommandManager.RequerySuggested -= value; }
    public bool CanExecute(object? parameter) => canExecute?.Invoke(parameter) ?? true;
    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter)) return;
        try { await execute(parameter); }
        catch (Exception e) { System.Windows.MessageBox.Show(e.Message, "Không thể thực hiện", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error); }
    }
}
