namespace Xeno.Client.Services
{
    /// <summary>
    /// Result of a model update: the provider that now owns the model, and the updated model.
    /// </summary>
    public class ModelUpdateResult
    {
        /// <summary>
        /// Provider that owns the model after the update.
        /// </summary>
        public ClientRunnerSettings Provider { get; set; } = new ClientRunnerSettings();

        /// <summary>
        /// Updated model.
        /// </summary>
        public ClientModelSettings Model { get; set; } = new ClientModelSettings();
    }
}
