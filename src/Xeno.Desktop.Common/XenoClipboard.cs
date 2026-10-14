namespace Xeno.Desktop.Common
{
    using System;
    using System.Threading.Tasks;
    using Avalonia.Controls;
    using Avalonia.Input.Platform;

    /// <summary>
    /// Copies text to the system clipboard through a window's clipboard. Failures are ignored.
    /// </summary>
    public static class XenoClipboard
    {
        /// <summary>
        /// Copy text.
        /// </summary>
        /// <param name="owner">Window or control whose top level provides the clipboard.</param>
        /// <param name="text">Text.</param>
        /// <returns>True when copied.</returns>
        public static async Task<bool> CopyAsync(Control? owner, string? text)
        {
            if (owner == null || String.IsNullOrEmpty(text)) return false;
            try
            {
                IClipboard? clipboard = TopLevel.GetTopLevel(owner)?.Clipboard;
                if (clipboard == null) return false;
                await clipboard.SetTextAsync(text);
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }
    }
}
