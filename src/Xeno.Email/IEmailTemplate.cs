namespace Xeno.Email
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Email template.
    /// </summary>
    /// <typeparam name="TContext">Template context type.</typeparam>
    public interface IEmailTemplate<TContext>
    {
        /// <summary>
        /// Template identifier.
        /// </summary>
        string TemplateId { get; }

        /// <summary>
        /// Render the template.
        /// </summary>
        /// <param name="context">Template context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Rendered content.</returns>
        Task<Templates.EmailTemplateRenderResult> RenderAsync(TContext context, CancellationToken token = default);
    }
}
