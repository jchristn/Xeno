namespace Xeno.Client.Services
{
    using System;

    /// <summary>
    /// Snapshot of one background model pull. Instances returned by <see cref="ModelPullCoordinator"/> are copies.
    /// </summary>
    public sealed class ModelPullJob
    {
        /// <summary>
        /// Job id.
        /// </summary>
        public string Id { get; set; } = String.Empty;

        /// <summary>
        /// Provider the model is pulled onto.
        /// </summary>
        public string Provider { get; set; } = String.Empty;

        /// <summary>
        /// Model being pulled.
        /// </summary>
        public string Model { get; set; } = String.Empty;

        /// <summary>
        /// Lifecycle state.
        /// </summary>
        public ModelPullStateEnum State { get; set; } = ModelPullStateEnum.Queued;

        /// <summary>
        /// Latest status text from the provider, for example "pulling manifest".
        /// </summary>
        public string Status { get; set; } = "queued";

        /// <summary>
        /// Bytes downloaded across every layer seen so far.
        /// </summary>
        public long CompletedBytes { get; set; } = 0;

        /// <summary>
        /// Total bytes across every layer seen so far. Grows as the provider announces layers.
        /// </summary>
        public long TotalBytes { get; set; } = 0;

        /// <summary>
        /// Percent complete from 0 to 100, or null before the provider reports sizes.
        /// </summary>
        public double? Percent { get; set; } = null;

        /// <summary>
        /// Why the pull failed, when it failed.
        /// </summary>
        public string? Error { get; set; } = null;

        /// <summary>
        /// Advertise the model once the pull succeeds.
        /// </summary>
        public bool AdvertiseOnSuccess { get; set; } = false;

        /// <summary>
        /// True once the model was advertised after a successful pull.
        /// </summary>
        public bool Advertised { get; set; } = false;

        /// <summary>
        /// UTC time the job was created.
        /// </summary>
        public DateTime StartedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// UTC time of the latest progress.
        /// </summary>
        public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// UTC time the job ended, or null while it runs.
        /// </summary>
        public DateTime? FinishedUtc { get; set; } = null;

        /// <summary>
        /// True while the job is queued or running.
        /// </summary>
        public bool Active => State == ModelPullStateEnum.Queued || State == ModelPullStateEnum.Running;

        /// <summary>
        /// Copy the snapshot.
        /// </summary>
        /// <returns>Copy.</returns>
        public ModelPullJob Clone()
        {
            return (ModelPullJob)MemberwiseClone();
        }
    }
}
