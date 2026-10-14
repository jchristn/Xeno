namespace Xeno.Desktop.Common
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Avalonia;
    using Avalonia.Controls;
    using Avalonia.Layout;
    using Xeno.Client.Hosting;

    /// <summary>
    /// Message, confirmation, and prompt dialogs. When there is no owner window (for example a command from the tray
    /// menu), the dialog shows unowned and the app appears in the macOS Dock while it is open. Call from the UI thread.
    /// </summary>
    public static class XenoDialogs
    {
        #region Public-Methods

        /// <summary>
        /// Show a message.
        /// </summary>
        /// <param name="owner">Owner window, or null.</param>
        /// <param name="title">Title.</param>
        /// <param name="message">Message.</param>
        /// <param name="tone">Tone.</param>
        /// <returns>Task that completes when the dialog closes.</returns>
        public static async Task MessageAsync(Window? owner, string title, string message, XenoToneEnum tone)
        {
            XenoDialogWindow dialog = BuildDialog(title, message, tone, new[] { "OK" }, 0);
            await ShowModalAsync<int?>(dialog, owner);
        }

        /// <summary>
        /// Ask for confirmation.
        /// </summary>
        /// <param name="owner">Owner window, or null.</param>
        /// <param name="title">Title.</param>
        /// <param name="message">Message.</param>
        /// <param name="confirmText">Confirm button text.</param>
        /// <returns>True when confirmed.</returns>
        public static async Task<bool> ConfirmAsync(Window? owner, string title, string message, string confirmText)
        {
            XenoDialogWindow dialog = BuildDialog(title, message, XenoToneEnum.Warning, new[] { "Cancel", confirmText }, 1);
            int? result = await ShowModalAsync<int?>(dialog, owner);
            return result == 1;
        }

        /// <summary>
        /// Ask for a single value.
        /// </summary>
        /// <param name="owner">Owner window, or null.</param>
        /// <param name="title">Title.</param>
        /// <param name="label">Field label.</param>
        /// <param name="initial">Initial value.</param>
        /// <param name="secret">True to mask the input.</param>
        /// <returns>Value, or null when cancelled.</returns>
        public static async Task<string?> PromptAsync(Window? owner, string title, string label, string initial, bool secret)
        {
            List<XenoFormField> fields = new List<XenoFormField>
            {
                new XenoFormField { Key = "value", Label = label, Value = initial, Kind = secret ? XenoFormFieldKindEnum.Password : XenoFormFieldKindEnum.Text, Required = true }
            };
            Dictionary<string, string>? values = await XenoFormDialog.ShowAsync(owner, title, null, fields, "OK");
            return values == null ? null : values["value"];
        }

        /// <summary>
        /// Show a window modally and return its result. Unowned windows are shown normally and awaited until closed.
        /// </summary>
        /// <typeparam name="T">Result type.</typeparam>
        /// <param name="dialog">Dialog.</param>
        /// <param name="owner">Owner, or null.</param>
        /// <returns>Result passed to Close, or default.</returns>
        /// <exception cref="ArgumentNullException">Dialog is null.</exception>
        public static async Task<T?> ShowModalAsync<T>(XenoDialogWindow dialog, Window? owner)
        {
            ArgumentNullException.ThrowIfNull(dialog);
            if (owner != null && owner.IsVisible)
            {
                return await dialog.ShowDialog<T?>(owner);
            }

            TaskCompletionSource<T?> completion = new TaskCompletionSource<T?>();
            dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            dialog.Closed += (sender, args) =>
            {
                completion.TrySetResult(dialog.Result is T typed ? typed : default);
                HideFromDockWhenNoWindows();
            };
            MacPlatform.ShowInDock();
            dialog.Show();
            dialog.Activate();
            return await completion.Task;
        }

        /// <summary>
        /// Return the app to accessory mode (no Dock icon) when no windows remain open. Used by the tray agent.
        /// </summary>
        public static void HideFromDockWhenNoWindows()
        {
            if (!HideDockWhenIdle) return;
            if (Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            {
                foreach (Window window in desktop.Windows)
                {
                    if (window.IsVisible) return;
                }
            }

            MacPlatform.HideFromDock();
        }

        /// <summary>
        /// True in the tray agent, which hides from the Dock when it has no windows open. Default false.
        /// </summary>
        public static bool HideDockWhenIdle { get; set; } = false;

        #endregion

        #region Private-Methods

        private static XenoDialogWindow BuildDialog(string title, string message, XenoToneEnum tone, string[] buttons, int defaultIndex)
        {
            XenoDialogWindow dialog = new XenoDialogWindow
            {
                Title = title,
                Width = 460,
                SizeToContent = SizeToContent.Height,
                CanResize = false,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Icon = XenoAssets.LoadWindowIcon(),
                Background = XenoTheme.Current.Background
            };

            StackPanel stack = new StackPanel { Spacing = 14, Margin = new Thickness(20) };
            stack.Children.Add(Ui.Heading(title));
            stack.Children.Add(tone == XenoToneEnum.Neutral || tone == XenoToneEnum.Info ? Ui.Body(message) : Ui.Notice(message, tone));
            StackPanel row = Ui.Row();
            row.HorizontalAlignment = HorizontalAlignment.Right;
            for (int i = 0; i < buttons.Length; i++)
            {
                int index = i;
                Button button = i == defaultIndex ? Ui.Primary(buttons[i], () => dialog.CloseWith((int?)index)) : Ui.Button(buttons[i], () => dialog.CloseWith((int?)index));
                if (i == defaultIndex) button.IsDefault = true;
                if (i == 0 && buttons.Length > 1) button.IsCancel = true;
                row.Children.Add(button);
            }

            stack.Children.Add(row);
            dialog.Content = stack;
            return dialog;
        }

        #endregion
    }
}
