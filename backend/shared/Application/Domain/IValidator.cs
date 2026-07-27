using FluentValidation.Results;

namespace Nestly.Application
{
    public interface IValidator<T>
    {
        Task<ValidationResult> ValidateAsync(T entity);
    }
}
