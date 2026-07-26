namespace backend.shared.Application.Domain;

public interface ILoginAttemptRepository : IRepository<LoginAttempt>
{
    Task<bool> ExistsByUsernameAsync(string username);
    Task<IEnumerable<LoginAttempt>> GetAttemptsByUsernameAsync(string username);
}
