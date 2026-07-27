namespace backend.shared.Application.Domain;

public class LoginAttempt : Entity<Guid>
{
    public string Username { get; private set; }
    public DateTime AttemptTime { get; private set; }
    public bool IsSuccessful { get; private set; }

    public LoginAttempt(string username, bool isSuccessful)
    {
        Username = username;
        AttemptTime = DateTime.UtcNow;
        IsSuccessful = isSuccessful;
    }

    // Required for Entity Framework Core
    protected LoginAttempt() { }
}
