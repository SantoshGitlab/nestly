namespace backend.shared.Application.Domain
{
    public class ServiceFaq : Entity<Guid>
    {
        private readonly Guid _serviceId;

        public ServiceFaq()
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
