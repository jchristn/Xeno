namespace Xeno.Desktop.Common
{
    using System.IO;
    using Avalonia.Controls;
    using Avalonia.Media.Imaging;

    /// <summary>
    /// Embedded Xeno artwork for windows, the tray icon, and the Dock.
    /// </summary>
    public static class XenoAssets
    {
        /// <summary>
        /// Open the Xeno logo PNG.
        /// </summary>
        /// <returns>Stream, or null when missing. The caller disposes it.</returns>
        public static Stream? OpenLogoPng()
        {
            return typeof(XenoAssets).Assembly.GetManifestResourceStream("Xeno.Desktop.Common.logo.png");
        }

        /// <summary>
        /// Load the Xeno logo as a bitmap.
        /// </summary>
        /// <returns>Bitmap, or null when missing.</returns>
        public static Bitmap? LoadLogo()
        {
            using (Stream? stream = OpenLogoPng())
            {
                return stream == null ? null : new Bitmap(stream);
            }
        }

        /// <summary>
        /// Load the Xeno window icon.
        /// </summary>
        /// <returns>Icon, or null when missing.</returns>
        public static WindowIcon? LoadWindowIcon()
        {
            using (Stream? stream = OpenLogoPng())
            {
                return stream == null ? null : new WindowIcon(stream);
            }
        }
    }
}
