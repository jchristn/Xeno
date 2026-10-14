namespace Xeno.Desktop
{
    using System;
    using System.Windows.Input;

    /// <summary>
    /// Minimal ICommand over a delegate, used for window key bindings.
    /// </summary>
    public sealed class DelegateCommand : ICommand
    {
        private readonly Action _Execute;
        private readonly Func<bool> _CanExecute;

        /// <summary>
        /// Raised when the result of <see cref="CanExecute(object?)"/> may have changed.
        /// </summary>
        public event EventHandler? CanExecuteChanged;

        /// <summary>
        /// Create a command.
        /// </summary>
        /// <param name="execute">Action.</param>
        /// <param name="canExecute">Enablement check, or null for always enabled.</param>
        /// <exception cref="ArgumentNullException">Action is null.</exception>
        public DelegateCommand(Action execute, Func<bool>? canExecute = null)
        {
            _Execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _CanExecute = canExecute ?? (() => true);
        }

        /// <summary>
        /// Determine whether the command can run.
        /// </summary>
        /// <param name="parameter">Ignored.</param>
        /// <returns>True when enabled.</returns>
        public bool CanExecute(object? parameter)
        {
            return _CanExecute();
        }

        /// <summary>
        /// Run the command.
        /// </summary>
        /// <param name="parameter">Ignored.</param>
        public void Execute(object? parameter)
        {
            if (_CanExecute()) _Execute();
        }

        /// <summary>
        /// Raise <see cref="CanExecuteChanged"/>.
        /// </summary>
        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
