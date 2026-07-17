namespace Xeno.Client.LocalInference
{
    using System;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Runtime manager for the local inference server.
    /// </summary>
    public static class LocalInferenceServerRuntime
    {
        private static LocalInferenceServerService? _Service;

        /// <summary>
        /// Start the local inference server when enabled.
        /// </summary>
        /// <param name="settings">Client settings.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public static async Task StartAsync(ClientSettings settings, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(settings);
            token.ThrowIfCancellationRequested();
            if (!settings.LocalInferenceServer.Enabled)
            {
                ClientConnectionState.MarkLocalInferenceDisabled(settings);
                return;
            }

            string endpoint = settings.LocalInferenceServer.Host + ":" + settings.LocalInferenceServer.Port.ToString(System.Globalization.CultureInfo.InvariantCulture);
            try
            {
                ClientConnectionState.MarkLocalInferenceStarting(settings);
                Console.WriteLine("Starting local inference server on " + endpoint + ".");
                LocalInferenceServerService service = new LocalInferenceServerService(settings);
                await service.StartAsync(token).ConfigureAwait(false);
                _Service = service;
                ClientConnectionState.MarkLocalInferenceListening(settings, service.Url);
                Console.WriteLine("Local inference server listening at " + service.Url + ".");
            }
            catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException || ex is SocketException)
            {
                string message = BuildStartupFailureMessage(settings, ex);
                ClientConnectionState.MarkLocalInferenceError(settings, message);
                Console.Error.WriteLine(message);
            }
        }

        /// <summary>
        /// Stop the local inference server.
        /// </summary>
        /// <param name="settings">Client settings.</param>
        public static void Stop(ClientSettings settings)
        {
            LocalInferenceServerService? service = _Service;
            if (service != null)
            {
                service.Dispose();
                _Service = null;
                Console.WriteLine("Local inference server stopped.");
            }

            ClientConnectionState.MarkLocalInferenceStopped(settings);
        }

        private static string BuildStartupFailureMessage(ClientSettings settings, Exception ex)
        {
            string endpoint = settings.LocalInferenceServer.Host + ":" + settings.LocalInferenceServer.Port.ToString(System.Globalization.CultureInfo.InvariantCulture);
            string message = "Local inference server failed to start on " + endpoint + " (" + ex.GetType().Name + "): " + ex.Message;
            if (IsPortConflict(ex))
            {
                message += " The port appears to be in use. Change LocalInferenceServer.Port in " + ClientSettings.SettingsFile + ".";
            }

            return message;
        }

        private static bool IsPortConflict(Exception ex)
        {
            SocketException? socketException = ex as SocketException;
            if (socketException != null)
            {
                return socketException.SocketErrorCode == SocketError.AddressAlreadyInUse;
            }

            if (ex.InnerException != null) return IsPortConflict(ex.InnerException);
            return ex.Message.Contains("address already in use", StringComparison.OrdinalIgnoreCase) ||
                   ex.Message.Contains("only one usage of each socket address", StringComparison.OrdinalIgnoreCase) ||
                   ex.Message.Contains("already in use", StringComparison.OrdinalIgnoreCase);
        }
    }
}
