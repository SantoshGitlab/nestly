namespace Nestly.Domain;

/// <summary>Who authored a <see cref="ChatMessage"/> (task 189/191).</summary>
public enum ChatSenderType
{
    Customer,
    Admin,

    /// <summary>The provider app/portal reply view (task 193, PROVIDER.md) - provider-api's <c>ChatController</c>/<c>IProviderChatService</c>.</summary>
    Provider
}
