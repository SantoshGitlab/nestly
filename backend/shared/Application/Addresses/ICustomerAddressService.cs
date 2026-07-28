using Nestly.BuildingBlocks.Results;

namespace Nestly.Application.Addresses;

public interface ICustomerAddressService
{
    Task<Result<IReadOnlyList<CustomerAddressResponse>>> ListAsync(Guid customerId);

    Task<Result<CustomerAddressResponse>> AddAsync(Guid customerId, UpsertAddressRequest request);

    Task<Result<CustomerAddressResponse>> UpdateAsync(Guid customerId, Guid addressId, UpsertAddressRequest request);

    Task<Result> DeleteAsync(Guid customerId, Guid addressId);

    Task<Result> SetDefaultAsync(Guid customerId, Guid addressId);
}
