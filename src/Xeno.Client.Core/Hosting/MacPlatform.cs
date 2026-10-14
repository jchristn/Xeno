namespace Xeno.Client.Hosting
{
    using System;
    using System.IO;
    using System.Runtime.InteropServices;

    /// <summary>
    /// macOS integration for the desktop app and the tray agent through the Objective-C runtime: Dock visibility
    /// (activation policy) and the Dock icon when running outside an .app bundle. Every method is a no-op that returns
    /// false on other platforms and never throws.
    /// </summary>
    public static class MacPlatform
    {
        #region Private-Members

        private const string ObjCLibrary = "/usr/lib/libobjc.A.dylib";
        private const long PolicyRegular = 0;
        private const long PolicyAccessory = 1;

        #endregion

        #region Public-Methods

        /// <summary>
        /// Show the application in the Dock and the app switcher (NSApplicationActivationPolicyRegular).
        /// </summary>
        /// <returns>True when applied.</returns>
        public static bool ShowInDock()
        {
            return SetActivationPolicy(PolicyRegular);
        }

        /// <summary>
        /// Hide the application from the Dock while keeping its menu bar extras (NSApplicationActivationPolicyAccessory).
        /// </summary>
        /// <returns>True when applied.</returns>
        public static bool HideFromDock()
        {
            return SetActivationPolicy(PolicyAccessory);
        }

        /// <summary>
        /// Determine whether the process runs from inside a macOS .app bundle.
        /// </summary>
        /// <returns>True inside a bundle.</returns>
        public static bool IsRunningFromBundle()
        {
            string? path = Environment.ProcessPath;
            return OperatingSystem.IsMacOS() && !String.IsNullOrEmpty(path) && path.Contains(".app/Contents/MacOS/", StringComparison.Ordinal);
        }

        /// <summary>
        /// Set the Dock icon from PNG data. Used when running outside a bundle, where macOS would otherwise show the
        /// generic icon of the dotnet host.
        /// </summary>
        /// <param name="png">PNG stream, or null.</param>
        /// <returns>True when applied.</returns>
        public static bool TryApplyDockIcon(Stream? png)
        {
            if (!OperatingSystem.IsMacOS() || png == null) return false;
            try
            {
                byte[] bytes;
                using (MemoryStream buffer = new MemoryStream())
                {
                    png.CopyTo(buffer);
                    bytes = buffer.ToArray();
                }

                if (bytes.Length < 1) return false;
                IntPtr nsData;
                GCHandle pinned = GCHandle.Alloc(bytes, GCHandleType.Pinned);
                try
                {
                    nsData = SendPointerLength(objc_getClass("NSData"), sel_registerName("dataWithBytes:length:"), pinned.AddrOfPinnedObject(), (nuint)bytes.Length);
                }
                finally
                {
                    pinned.Free();
                }

                if (nsData == IntPtr.Zero) return false;
                IntPtr image = SendPointer(Send(objc_getClass("NSImage"), sel_registerName("alloc")), sel_registerName("initWithData:"), nsData);
                IntPtr application = Send(objc_getClass("NSApplication"), sel_registerName("sharedApplication"));
                if (image == IntPtr.Zero || application == IntPtr.Zero) return false;
                SendPointer(application, sel_registerName("setApplicationIconImage:"), image);
                return true;
            }
            catch (Exception ex) when (ex is DllNotFoundException || ex is EntryPointNotFoundException || ex is IOException || ex is MarshalDirectiveException)
            {
                return false;
            }
        }

        #endregion

        #region Private-Methods

        private static bool SetActivationPolicy(long policy)
        {
            if (!OperatingSystem.IsMacOS()) return false;
            try
            {
                IntPtr application = Send(objc_getClass("NSApplication"), sel_registerName("sharedApplication"));
                return application != IntPtr.Zero && SendLong(application, sel_registerName("setActivationPolicy:"), policy);
            }
            catch (Exception ex) when (ex is DllNotFoundException || ex is EntryPointNotFoundException || ex is MarshalDirectiveException)
            {
                return false;
            }
        }

        [DllImport(ObjCLibrary)]
        private static extern IntPtr objc_getClass(string name);

        [DllImport(ObjCLibrary)]
        private static extern IntPtr sel_registerName(string name);

        [DllImport(ObjCLibrary, EntryPoint = "objc_msgSend")]
        private static extern IntPtr Send(IntPtr receiver, IntPtr selector);

        [DllImport(ObjCLibrary, EntryPoint = "objc_msgSend")]
        private static extern IntPtr SendPointer(IntPtr receiver, IntPtr selector, IntPtr argument);

        [DllImport(ObjCLibrary, EntryPoint = "objc_msgSend")]
        private static extern IntPtr SendPointerLength(IntPtr receiver, IntPtr selector, IntPtr pointer, nuint length);

        [DllImport(ObjCLibrary, EntryPoint = "objc_msgSend")]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool SendLong(IntPtr receiver, IntPtr selector, long argument);

        #endregion
    }
}
