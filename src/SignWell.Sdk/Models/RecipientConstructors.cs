using SignWell.Sdk.Client;

namespace SignWell.Sdk.Models;

public partial class RecipientsInner
{
    /// <summary>Preserves the email-first constructor used before SMS delivery was added.</summary>
    public RecipientsInner(string id, string email, Option<string?> name = default,
        Option<string?> passcode = default, Option<string?> subject = default,
        Option<string?> message = default, Option<bool?> sendEmail = default,
        Option<int?> sendEmailDelay = default)
        : this(id: id, email: new Option<string?>(email), name: name, passcode: passcode,
            subject: subject, message: message, sendEmail: sendEmail, sendEmailDelay: sendEmailDelay)
    { }
}

public partial class TemplateRecipientsInner
{
    /// <summary>Preserves the email-first constructor used before SMS delivery was added.</summary>
    public TemplateRecipientsInner(string id, string email, Option<string?> name = default,
        Option<string?> placeholderName = default, Option<string?> passcode = default,
        Option<string?> subject = default, Option<string?> message = default,
        Option<bool?> sendEmail = default, Option<int?> sendEmailDelay = default)
        : this(id: id, email: new Option<string?>(email), name: name, placeholderName: placeholderName,
            passcode: passcode, subject: subject, message: message, sendEmail: sendEmail,
            sendEmailDelay: sendEmailDelay)
    { }
}
