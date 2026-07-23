using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace backend.shared.Application.Domain
{
    public class ServiceAddOn : Entity<Guid>
    {
        private Guid _serviceId;
        private Guid _addOnId;

        public void SetServiceId(Guid serviceId) => this._serviceId = serviceId;
        public void SetAddOnId(Guid addOnId) => this._addOnId = addOnId;

        public async Task<Service> GetServiceAsync(DbContext context)
        {
            return await context.Set<Service>()
                .Include(s => s.ServiceAddOns)
                .FirstOrDefaultAsync(s => s.Id == this._serviceId);
        }
    }
}
