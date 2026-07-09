namespace Xeno.Email.Providers
{
    using System;
    using Xeno.Core.Enums;
    using Xeno.Core.Settings;

    /// <summary>
    /// Email provider factory.
    /// </summary>
    public static class EmailProviderFactory
    {
        /// <summary>
        /// Create an email provider.
        /// </summary>
        /// <param name="settings">Email settings.</param>
        /// <returns>Email provider.</returns>
        public static IEmailProvider Create(EmailSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (!settings.Enabled) return new DisabledEmailProvider();

            if (settings.Provider == EmailProviderTypeEnum.Disabled) return new DisabledEmailProvider();
            if (settings.Provider == EmailProviderTypeEnum.File) return new FileEmailProvider(settings.File ?? new EmailFileSettings());
            if (settings.Provider == EmailProviderTypeEnum.Mailgun) return new MailgunEmailProvider(settings.Mailgun ?? new MailgunEmailSettings());

            throw new ArgumentOutOfRangeException(nameof(settings), "Unsupported email provider '" + settings.Provider + "'.");
        }
    }
}
