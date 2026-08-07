namespace Nestly.BuildingBlocks.Privacy;

/// <summary>
/// The one masking convention for contact details that leave the system -
/// log lines, stored audit recipients, and API responses that must show a
/// customer who to expect without handing out a reachable number.
///
/// Promoted here (task 275) from three byte-identical private copies in
/// <c>NotificationDispatchService</c>, <c>SandboxNotificationProvider</c> and
/// <c>SandboxPushNotificationProvider</c>, whose own doc comments already
/// pointed at each other to say "same masking convention as...". A fourth
/// copy was about to be written for the live tracking response; masking is
/// exactly the kind of rule that must not be allowed to drift between the
/// place a number is logged and the place it is served.
///
/// Deliberately in BuildingBlocks and not in Domain: this is a presentation
/// rule about what may cross a boundary, not a fact about any aggregate.
/// </summary>
public static class ContactMasking
{
    /// <summary>
    /// How many trailing characters stay readable. Four is the industry-wide
    /// "last four digits" convention - enough for a customer to recognise the
    /// number their phone is ringing from, useless to anyone harvesting the
    /// response, and it survives the +91/+1 country-code prefixes this system
    /// stores inline because the prefix is at the other end of the string.
    /// </summary>
    private const int VisibleSuffixLength = 4;

    /// <summary>
    /// Masks all but the last <see cref="VisibleSuffixLength"/> characters.
    /// A value at or under that length is masked entirely rather than left
    /// readable - the short-input case is the one where "show the last four"
    /// silently degrades into "show everything".
    /// </summary>
    public static string Mask(string value)
    {
        if (value.Length <= VisibleSuffixLength)
        {
            return new string('*', value.Length);
        }

        return new string('*', value.Length - VisibleSuffixLength) + value[^VisibleSuffixLength..];
    }

    /// <summary>
    /// The nullable-in, nullable-out form for optional contacts. Null and
    /// blank both become null rather than a string of asterisks: a response
    /// field saying "we have a number and it ends ****" is a different claim
    /// from "we have no number", and a UI needs to tell them apart.
    /// </summary>
    public static string? MaskOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : Mask(value);
}
