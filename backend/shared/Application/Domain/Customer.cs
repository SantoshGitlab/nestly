using System;
using System.Collections.Generic;

namespace backend.shared.Application.Domain
{
    public class Customer : Entity<Guid>
    {
        public string Mobile { get; private set; }
        public string Email { get; private set; }
        public string Name { get; private set; }
        public DateTime DateOfBirth { get; private set; }
        public string Address { get; private set; }
        public string City { get; private set; }
        public string State { get; private set; }
        public string Pincode { get; private set; }
        public string Country { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime UpdatedAt { get; private set; }
        public CustomerStatus Status { get; private set; }

        public Customer(
            Guid id,
            string mobile,
            string email,
            string name,
            DateTime dateOfBirth,
            string address,
            string city,
            string state,
            string pincode,
            string country,
            CustomerStatus status)
        {
            Id = id;
            Mobile = mobile;
            Email = email;
            Name = name;
            DateOfBirth = dateOfBirth;
            Address = address;
            City = city;
            State = state;
            Pincode = pincode;
            Country = country;
            Status = status;
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }

        public void UpdateStatus(CustomerStatus newStatus)
        {
            if (newStatus != Status)
            {
                Status = newStatus;
                UpdatedAt = DateTime.UtcNow;
            }
        }

        public override bool Equals(object? obj)
        {
            return obj is Customer other && Id == other.Id;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Id);
        }
    }

    public enum CustomerStatus
    {
        Active,
        Blocked,
        Unverified,
        SoftDeleted
    }
}
