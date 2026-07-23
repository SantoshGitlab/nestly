namespace backend.shared.Application.Domain
{
    public class ServiceAddOn : Entity<Guid>
    {
        private readonly Guid _serviceId;

        public ServiceAddOn()
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
