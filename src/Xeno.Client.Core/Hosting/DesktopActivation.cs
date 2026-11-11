namespace Xeno.Client.Hosting
{
    using System;
    using System.IO;
    using System.Text.Json;
    using Xeno.Client.Services;
    using Xeno.Core.Serialization;

    /// <summary>
    /// Hands activation requests to the running desktop app. Only one desktop app runs per client home (it holds
    /// <see cref="XenoAppLauncher.DesktopLockName"/>), so another process that wants it in front, on a page, or showing
    /// Settings writes a <see cref="DesktopActivationRequest"/> under the client home, and the running app takes it.
    /// Works the same on Linux, macOS, and Windows. Thread safe.
    /// </summary>
    public static class DesktopActivation
    {
        #region Public-Members

        /// <summary>
        /// Requests older than this are ignored, so a request left behind by an app that exited is not acted on later.
        /// </summary>
        public static readonly TimeSpan MaximumAge = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Request file path.
        /// </summary>
        public static string FilePath
        {
            get { return Path.Combine(ClientSettings.SettingsDirectory, "desktop-activation.json"); }
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Write a request for the running desktop app. A newer request replaces an older one that was not taken yet.
        /// </summary>
        /// <param name="request">Request.</param>
        /// <exception cref="ArgumentNullException">request is null.</exception>
        /// <exception cref="IOException">The request could not be written.</exception>
        public static void Post(DesktopActivationRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            request.RequestedUtc = DateTime.UtcNow;
            ClientFileIo.WriteAllTextAtomic(FilePath, JsonUtil.Serialize(request, true));
        }

        /// <summary>
        /// Take the pending request, removing it. Returns null when there is none, it is unreadable, or it is older than
        /// <see cref="MaximumAge"/>.
        /// </summary>
        /// <returns>Request, or null.</returns>
        public static DesktopActivationRequest? Take()
        {
            string path = FilePath;
            if (!File.Exists(path)) return null;
            DesktopActivationRequest? request = null;
            try
            {
                request = JsonUtil.Deserialize<DesktopActivationRequest>(ClientFileIo.ReadAllText(path));
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is JsonException || ex is System.IO.InvalidDataException)
            {
                request = null;
            }

            Clear();
            if (request == null) return null;
            if (DateTime.UtcNow - request.RequestedUtc > MaximumAge) return null;
            return request;
        }

        /// <summary>
        /// Remove any pending request.
        /// </summary>
        public static void Clear()
        {
            try
            {
                if (File.Exists(FilePath)) File.Delete(FilePath);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
            }
        }

        #endregion
    }
}
