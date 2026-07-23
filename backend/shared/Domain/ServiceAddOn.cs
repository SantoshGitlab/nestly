using Microsoft.EntityFrameworkCore;
using backend.shared.Application.Domain;

namespace backend.shared.Application.Domain
{
    public class ServiceAddOn : Entity<Guid>
    {
        private Guid _serviceId;
        // ... other properties and methods

        public void SetServiceId(Guid serviceId) => this._serviceId = serviceId;

        public async Task<Service> GetServiceAsync(DbContext context)
        {
            return await context.Set<Service>().FindAsync(this._serviceId);
        }
    }
}
