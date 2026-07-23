using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using backend.shared.Application.Domain;
using backend.shared.Application.Domain.Entities;

namespace backend.shared.Application.Domain.Repositories
{
    public class ServiceAddOnRepository : IRepository<ServiceAddOn>
    {
        private readonly IUnitOfWork _unitOfWork;

        public ServiceAddOnRepository(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task AddAsync(ServiceAddOn entity)
        {
            await _unitOfWork.Services.AddAsync(entity);
            await _unitOfWork.SaveChangesAsync();
        }

        public async Task UpdateAsync(ServiceAddOn entity)
        {
            var existingEntity = await _unitOfWork.FindByIdAsync<ServiceAddOn>(entity.Id);
            if (existingEntity == null)
                throw new EntityNotFoundException<ServiceAddOn>(entity.Id);

            existingEntity.Name = entity.Name;
            existingEntity.Description = entity.Description;
            existingEntity.Price = entity.Price;

            await _unitOfWork.SaveChangesAsync();
        }

        public async Task DeleteAsync(ServiceAddOn entity)
        {
            var existingEntity = await _unitOfWork.FindByIdAsync<ServiceAddOn>(entity.Id);
            if (existingEntity == null)
                throw new EntityNotFoundException<ServiceAddOn>(entity.Id);

            _unitOfWork.Services.Remove(existingEntity);
            await _unitOfWork.SaveChangesAsync();
        }

        public async Task<ServiceAddOn> GetByIdAsync(Guid id)
        {
            return await _unitOfWork.FindByIdAsync<ServiceAddOn>(id);
        }

        public async Task<IEnumerable<ServiceAddOn>> GetAllAsync()
        {
            return await _unitOfWork.Services.GetAllAsync<ServiceAddOn>();
        }

        public async Task<bool> ExistsAsync(Guid id)
        {
            return await _unitOfWork.Services.ExistsAsync<ServiceAddOn>(id);
        }
    }
}
