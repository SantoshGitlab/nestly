using System;
using System.Collections.Generic;
using System.Linq;
using backend.shared.Application.Domain;
using backend.shared.Application.Domain.ValueObjects;

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
        public bool IsActive { get; private set; } = true;
        public IReadOnlyCollection<ServiceAddOn> ServiceAddOns => _serviceAddOns.AsReadOnly();
        public IReadOnlyCollection<ServiceFaq> ServiceFaqs => _serviceFaqs.AsReadOnly();
        public IReadOnlyCollection<ServiceMedia> ServiceMedias => _serviceMedias.AsReadOnly();

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
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Service name cannot be null or whitespace.");

            Name = name;
        }

        public void SetDescription(string description)
        {
            Description = description;
        }

        public void SetPrice(decimal price)
        {
            if (price <= 0)
                throw new ArgumentOutOfRangeException(nameof(price), "Service price must be greater than zero.");

            Price = price;
        }

        public void Activate()
        {
            IsActive = true;
        }

        public void Deactivate()
        {
            IsActive = false;
        }

        public void AddServiceAddOn(ServiceAddOn serviceAddOn)
        {
            if (_serviceAddOns.Any(sa => sa.Id == serviceAddOn.Id))
                throw new ArgumentException("Service add-on already exists.");

            _serviceAddOns.Add(serviceAddOn);
        }

        public void RemoveServiceAddOn(Guid serviceAddOnId)
        {
            var serviceAddOn = _serviceAddOns.FirstOrDefault(sa => sa.Id == serviceAddOnId);
            if (serviceAddOn is null)
                throw new ArgumentException("Service add-on not found.");

            _serviceAddOns.Remove(serviceAddOn);
        }

        public void AddServiceFaq(ServiceFaq serviceFaq)
        {
            if (_serviceFaqs.Any(sf => sf.Id == serviceFaq.Id))
                throw new ArgumentException("Service FAQ already exists.");

            _serviceFaqs.Add(serviceFaq);
        }

        public void RemoveServiceFaq(Guid serviceFaqId)
        {
            var serviceFaq = _serviceFaqs.FirstOrDefault(sf => sf.Id == serviceFaqId);
            if (serviceFaq is null)
                throw new ArgumentException("Service FAQ not found.");

            _serviceFaqs.Remove(serviceFaq);
        }

        public void AddServiceMedia(ServiceMedia serviceMedia)
        {
            if (_serviceMedias.Any(sm => sm.Id == serviceMedia.Id))
                throw new ArgumentException("Service media already exists.");

            _serviceMedias.Add(serviceMedia);
        }

        public void RemoveServiceMedia(Guid serviceMediaId)
        {
            var serviceMedia = _serviceMedias.FirstOrDefault(sm => sm.Id == serviceMediaId);
            if (serviceMedia is null)
                throw new ArgumentException("Service media not found.");

            _serviceMedias.Remove(serviceMedia);
        }
    }
}
