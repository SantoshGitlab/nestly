using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain;

public class ServiceMedia : Entity<Guid>
{
    public Guid ServiceId { get; private set; }
    public string Url { get; private set; } = string.Empty;

    protected ServiceMedia() { }

    public ServiceMedia(Guid id, Guid serviceId, string url) : base(id)
    {
        ServiceId = serviceId;
        Url = url;
    }

    public void SetServiceId(Guid serviceId) => ServiceId = serviceId;
}
