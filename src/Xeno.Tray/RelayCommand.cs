namespace Xeno.Tray
{
    using System;
    using System.Windows.Input;

    /// <summary>
    /// Lightweight command implementation for tray menu commands.
    /// </summary>
    public sealed class RelayCommand : ICommand
    {
        private readonly Action<object?> _Execute;
        private readonly Func<object?, bool>? _CanExecute;

        /// <summary>
        /// Raised when command executability may have changed.
        /// </summary>
        public event EventHandler? CanExecuteChanged;

        /// <summary>
        /// Instantiate the command.
        /// </summary>
        /// <param name="execute">Command action.</param>
        /// <param name="canExecute">Optional executability predicate.</param>
        public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
        {
            _Execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _CanExecute = canExecute;
        }

        /// <inheritdoc />
        public bool CanExecute(object? parameter)
        {
            return _CanExecute == null || _CanExecute(parameter);
        }

        /// <inheritdoc />
        public void Execute(object? parameter)
        {
            _Execute(parameter);
        }

        /// <summary>
        /// Raise the CanExecuteChanged event.
        /// </summary>
        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
