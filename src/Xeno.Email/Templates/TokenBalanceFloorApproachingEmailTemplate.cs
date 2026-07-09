namespace Xeno.Email.Templates
{
    using System;
    using System.Globalization;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Core.Settings;

    /// <summary>
    /// Token balance floor warning template.
    /// </summary>
    public class TokenBalanceFloorApproachingEmailTemplate : EmailTemplateBase<TokenBalanceFloorApproachingEmailTemplateContext>
    {
        /// <summary>
        /// Instantiate the template.
        /// </summary>
        /// <param name="settings">Email settings.</param>
        public TokenBalanceFloorApproachingEmailTemplate(EmailSettings settings) : base(settings)
        {
        }

        /// <summary>
        /// Template identifier.
        /// </summary>
        public override string TemplateId => "usage.token-balance-floor-approaching.v1";

        /// <summary>
        /// Render the template.
        /// </summary>
        /// <param name="context">Template context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Rendered content.</returns>
        public override Task<EmailTemplateRenderResult> RenderAsync(TokenBalanceFloorApproachingEmailTemplateContext context, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            if (context == null) throw new ArgumentNullException(nameof(context));

            EmailTemplateField[] fields = new EmailTemplateField[]
            {
                EmailTemplateField.Create("Tenant", context.TenantName),
                EmailTemplateField.Create("Current balance", context.CurrentBalance.ToString(CultureInfo.InvariantCulture)),
                EmailTemplateField.Create("Floor", context.Floor.ToString(CultureInfo.InvariantCulture))
            };
            string bodyHtml =
                "<h1 style=\"font-size:20px;line-height:1.3;margin:0 0 12px;\">Token balance is approaching its floor</h1>" +
                "<p style=\"margin:0 0 12px;\">Your configured token balance warning threshold is close.</p>" +
                RenderKeyValueTable(fields);
            string bodyText = "Token balance is approaching its floor" + Environment.NewLine + Environment.NewLine + RenderKeyValueText(fields);
            EmailTemplateRenderResult result = RenderLayout("Token balance warning", "Your token balance is approaching its configured floor.", bodyHtml, bodyText);
            return Task.FromResult(result);
        }
    }
}
