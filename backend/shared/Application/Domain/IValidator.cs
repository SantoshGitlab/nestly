using FluentValidation.Results;

namespace backend.shared.Application.Domain
{
    public interface IValidator<T>
    {
        Task<ValidationResult> ValidateAsync(T entity);
    }
}
