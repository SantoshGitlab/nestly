using Nesty;

namespace Nesty.Domain;

public class ServiceFaq : Entity<Guid>
{
    public Guid ServiceId { get; private set; }
    public string Question { get; private set; } = string.Empty;
    public string Answer { get; private set; } = string.Empty;

    protected ServiceFaq() { }

    public ServiceFaq(Guid id, Guid serviceId, string question, string answer) : base(id)
    {
        ServiceId = serviceId;
        Question = question ?? throw new ArgumentNullException(nameof(question));
        Answer = answer ?? string.Empty;
    }

    public void SetServiceId(Guid serviceId) => ServiceId = serviceId;
}
