namespace Xeno.Tui.Shell
{
    using System;
    using System.Collections.Generic;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Theming;
    using TUIKit.Widgets;

    /// <summary>
    /// Shows a page dropdown that sits in a one-row pane. A one-row pane has no room for the dropdown's own list, so a
    /// click, Enter, Space, F4, or Alt+Down asks <see cref="Open"/> to show the choices as a popup instead. Up, Down,
    /// Home, End, and the mouse wheel still step through the choices in place.
    /// </summary>
    internal sealed class DropdownPicker : IWidget, IFocusable, IFocusAware, IMouseAware, IThemeable, IKeyHintSource, IEnableable
    {
        #region Public-Members

        /// <summary>
        /// Wrapped dropdown, which holds the choices and the selection.
        /// </summary>
        public Dropdown<string> Dropdown { get; }

        /// <summary>
        /// Shows the choices as a popup. Set by the page that owns the picker.
        /// </summary>
        public Action<DropdownPicker>? Open { get; set; } = null;

        /// <summary>
        /// True when the dropdown takes input.
        /// </summary>
        public bool IsEnabled
        {
            get { return Dropdown.IsEnabled; }
            set { Dropdown.IsEnabled = value; }
        }

        #endregion

        #region Private-Members

        private static readonly IReadOnlyList<KeyHint> PickerHints = new List<KeyHint>
        {
            new KeyHint("Enter", "Choose"),
            new KeyHint("↑↓", "Change")
        };

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Wrap a dropdown.
        /// </summary>
        /// <param name="dropdown">Dropdown.</param>
        /// <exception cref="ArgumentNullException">Dropdown is null.</exception>
        public DropdownPicker(Dropdown<string> dropdown)
        {
            Dropdown = dropdown ?? throw new ArgumentNullException(nameof(dropdown));
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Measure: one row.
        /// </summary>
        /// <param name="available">Available size.</param>
        /// <returns>Desired size.</returns>
        public Size Measure(Size available)
        {
            return new Size(available.Width, Math.Min(1, available.Height));
        }

        /// <summary>
        /// Render the closed dropdown.
        /// </summary>
        /// <param name="surface">Surface.</param>
        public void Render(ISurface surface)
        {
            if (Dropdown.IsOpen) Dropdown.Close();
            Dropdown.Render(surface);
        }

        /// <summary>
        /// Open the popup on Enter, Space, F4, or Alt+Down; step through choices on the arrow keys.
        /// </summary>
        /// <param name="key">Key.</param>
        /// <returns>True when handled.</returns>
        public bool HandleKey(KeyEvent key)
        {
            if (!Dropdown.IsEnabled) return false;
            bool opener = (key.Code == KeyCode.Enter && key.Modifiers == KeyModifiers.None)
                || (key.Code == KeyCode.Character && key.Rune == ' ' && key.Modifiers == KeyModifiers.None)
                || key.Code == KeyCode.F4
                || (key.Code == KeyCode.Down && key.Modifiers == KeyModifiers.Alt);
            if (opener)
            {
                RequestOpen();
                return true;
            }

            bool handled = Dropdown.HandleKey(key);
            if (Dropdown.IsOpen) Dropdown.Close();
            return handled;
        }

        /// <summary>
        /// Open the popup on a left click; step through choices on the wheel.
        /// </summary>
        /// <param name="mouse">Mouse event.</param>
        /// <returns>True when handled.</returns>
        public bool HandleMouse(MouseEvent mouse)
        {
            if (mouse == null || !Dropdown.IsEnabled) return false;
            if (mouse.Kind == MouseEventKind.Press && mouse.Button == MouseButton.Left)
            {
                RequestOpen();
                return true;
            }

            if (mouse.Kind == MouseEventKind.Wheel)
            {
                int count = Dropdown.Items.Count;
                if (count == 0) return true;
                int step = mouse.Button == MouseButton.WheelUp ? -1 : 1;
                Dropdown.SelectedIndex = Math.Clamp(Dropdown.SelectedIndex + step, 0, count - 1);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Forward focus changes.
        /// </summary>
        /// <param name="focused">True when focused.</param>
        public void OnFocusChanged(bool focused)
        {
            Dropdown.OnFocusChanged(focused);
        }

        /// <summary>
        /// Apply a theme.
        /// </summary>
        /// <param name="theme">Theme.</param>
        public void ApplyTheme(Theme theme)
        {
            Dropdown.ApplyTheme(theme);
        }

        /// <summary>
        /// Key hints.
        /// </summary>
        /// <returns>Hints.</returns>
        public IReadOnlyList<KeyHint>? GetKeyHints()
        {
            return PickerHints;
        }

        #endregion

        #region Private-Methods

        private void RequestOpen()
        {
            if (Dropdown.Items.Count == 0) return;
            if (Open != null) Open(this);
            else Dropdown.Open();
        }

        #endregion
    }
}
