namespace backend.shared.Application.Domain
{
    public class ServiceMedia : Entity<Guid>
    {
        private readonly Guid _serviceId;

        public ServiceMedia()
        {
            _serviceId = Guid.Empty;
        }

        public void SetServiceId(Guid serviceId)
        {
            _serviceId = serviceId;
        }

        public Guid ServiceId => _serviceId;
    }
}
