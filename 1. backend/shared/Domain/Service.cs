namespace backend.shared.Application.Domain
{
    public class Service : Entity<Guid>
    {
        private readonly List<ServiceAddOn> _serviceAddOns = new();
        private readonly List<ServiceFaq> _serviceFaqs = new();
        private readonly List<ServiceMedia> _serviceMedias = new();

        public ServiceId Id { get; private set; }
        public CategoryId CategoryId { get; private set; }
        public string Name { get; private set; }
        public string Description { get; private set; }
        public decimal Price { get; private set; }
        public bool IsActive { get; private set; }
        public DateTimeOffset CreatedAt { get; private set; }
        public DateTimeOffset UpdatedAt { get; private set; }

        public IReadOnlyCollection<ServiceAddOn> ServiceAddOns => _serviceAddOns;
        public IReadOnlyCollection<ServiceFaq> ServiceFaqs => _serviceFaqs;
        public IReadOnlyCollection<ServiceMedia> ServiceMedias => _serviceMedias;

        private void SetId(ServiceId id)
        {
            Id = id;
        }

        private void SetCategoryId(CategoryId categoryId)
        {
            CategoryId = categoryId;
        }

        public void SetName(string name)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        public void SetDescription(string description)
        {
            Description = description ?? string.Empty;
        }

        public void SetPrice(decimal price)
        {
            Price = price > 0 ? price : throw new ArgumentOutOfRangeException(nameof(price));
        }

        public void Activate()
        {
            IsActive = true;
            UpdatedAt = DateTimeOffset.UtcNow;
        }

        public void Deactivate()
        {
            IsActive = false;
            UpdatedAt = DateTimeOffset.UtcNow;
        }

        public void AddServiceAddOn(ServiceAddOn serviceAddOn)
        {
            _serviceAddOns.Add(serviceAddOn);
            serviceAddOn.SetServiceId(Id);
        }

        public void RemoveServiceAddOn(Guid serviceAddOnId)
        {
            var serviceAddOn = _serviceAddOns.FirstOrDefault(sa => sa.Id == serviceAddOnId);
            if (serviceAddOn != null)
            {
                _serviceAddOns.Remove(serviceAddOn);
                serviceAddOn.SetServiceId(Guid.Empty);
            }
        }

        public void AddServiceFaq(ServiceFaq serviceFaq)
        {
            _serviceFaqs.Add(serviceFaq);
            serviceFaq.SetServiceId(Id);
        }

        public void RemoveServiceFaq(Guid serviceFaqId)
        {
            var serviceFaq = _serviceFaqs.FirstOrDefault(sf => sf.Id == serviceFaqId);
            if (serviceFaq != null)
            {
                _serviceFaqs.Remove(serviceFaq);
                serviceFaq.SetServiceId(Guid.Empty);
            }
        }

        public void AddServiceMedia(ServiceMedia serviceMedia)
        {
            _serviceMedias.Add(serviceMedia);
            serviceMedia.SetServiceId(Id);
        }

        public void RemoveServiceMedia(Guid serviceMediaId)
        {
            var serviceMedia = _serviceMedias.FirstOrDefault(sm => sm.Id == serviceMediaId);
            if (serviceMedia != null)
            {
                _serviceMedias.Remove(serviceMedia);
                serviceMedia.SetServiceId(Guid.Empty);
            }
        }
    }
}
