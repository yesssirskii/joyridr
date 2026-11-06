namespace Joyridr.Common;

public class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>
    /// Resend API key. Never commit it: set it with user-secrets or the Email__ResendApiKey variable.
    /// When it's empty, emails are written to the log instead of being sent (handy on Local).
    /// </summary>
    public string? ResendApiKey { get; set; }

    /// <summary>
    /// Sender shown in the inbox. Until a domain is verified in Resend, only "onboarding@resend.dev" works,
    /// and Resend then only delivers to the address the Resend account was created with.
    /// </summary>
    public string From { get; set; } = "JOYRIDR <onboarding@resend.dev>";
}
