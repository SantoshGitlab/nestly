using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Nestly.Application.Addresses;
using Nestly.Application.Behaviors;

namespace Nestly.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers MediatR handlers, pipeline behaviors, and FluentValidation
    /// validators from the Application assembly.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(configuration =>
        {
            configuration.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly);
            configuration.AddOpenBehavior(typeof(LoggingBehavior<,>));
            configuration.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, includeInternalTypes: true);

        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<ICustomerAddressService, CustomerAddressService>();

        return services;
    }
}
