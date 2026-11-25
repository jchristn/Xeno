namespace Xeno.Client.Services
{
    using System;

    /// <summary>
    /// Start pulling a model onto a provider.
    /// </summary>
    public sealed class ModelPullRequest
    {
        /// <summary>
        /// Model to pull, for example "llama3.2:3b".
        /// </summary>
        public string Model { get; set; } = String.Empty;

        /// <summary>
        /// Advertise the model to the Xeno network once the pull succeeds.
        /// </summary>
        public bool Advertise { get; set; } = false;
    }
}
