using System.Collections.Generic;

namespace backend.shared.Application.Domain
{
    public class ValidationException : Exception
    {
        public IEnumerable<ValidationFailure> Errors { get; }

        public ValidationException(IEnumerable<ValidationFailure> errors)
            : base("One or more validation errors occurred.")
        {
            Errors = errors;
        }
    }
}
