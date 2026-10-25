using System.Windows.Input;

namespace CadSemanticAssistant.Presentation.Wpf.ViewModels;

public sealed class AsyncRelayCommand : ICommand
{
    private readonly Func<Task> _execute;

    private bool _isExecuting;

    public AsyncRelayCommand(
        Func<Task> execute)
    {
        _execute =
            execute
            ?? throw new ArgumentNullException(
                nameof(execute));
    }

    public bool CanExecute(
        object? parameter)
    {
        return !_isExecuting;
    }

    public async void Execute(
        object? parameter)
    {
        if (_isExecuting)
        {
            return;
        }

        try
        {
            _isExecuting = true;

            RaiseCanExecuteChanged();

            await _execute();
        }
        finally
        {
            _isExecuting = false;

            RaiseCanExecuteChanged();
        }
    }

    public event EventHandler? CanExecuteChanged;

    private void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(
            this,
            EventArgs.Empty);
    }
}
