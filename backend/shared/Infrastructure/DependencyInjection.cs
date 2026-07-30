using System.Text;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Nestly.Application;
using Nestly.Application.Abstractions.Auditing;
using Nestly.Application.Identity;
using Nestly.Application.Profile;
using Nestly.Application.Bookings;
using Nestly.Application.Catalog;
using Nestly.Application.Coupons;
using Nestly.Application.Geography;
using Nestly.Application.Payments;
using Nestly.Application.Pricing;
using Nestly.Application.Wallet;
using Nestly.Application.Serviceability;
using Nestly.Application.Slots;
using Nestly.Domain;
using Nestly.Infrastructure.Auditing;
using Nestly.Infrastructure.BackgroundJobs;
using Nestly.Infrastructure.Caching;
using Nestly.Infrastructure.Options;
using Nestly.Infrastructure.Persistence;
using Nestly.Infrastructure.Persistence.Interceptors;
using Nestly.Infrastructure.Persistence.Repositories;
using Nestly.Infrastructure.Services;

namespace Nestly.Infrastructure;

public static class DependencyInjection
{
    private const string DatabaseConnectionName = "Database";

    /// <summary>
    /// Registers infrastructure services: persistence, caching (T017),
    /// background jobs (T018), auditing (T020), health checks, and — as each
    /// capability lands — external providers.
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection(DatabaseOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services
            .AddOptions<AccountOptions>()
            .Bind(configuration.GetSection(AccountOptions.SectionName));

        services
            .AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services
            .AddOptions<SandboxGatewayOptions>()
            .Bind(configuration.GetSection(SandboxGatewayOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        string connectionString = configuration.GetConnectionString(DatabaseConnectionName) ??
            throw new InvalidOperationException(
                $"Connection string '{DatabaseConnectionName}' is not configured.");

        services.AddSingleton<AuditableEntityInterceptor>();
        services.AddScoped<DomainEventDispatchInterceptor>();
        services.AddSingleton<NewOwnedChildEntityInterceptor>();

        services.AddDbContext<NestlyDbContext>((serviceProvider, options) =>
            options
                .UseNpgsql(connectionString)
                .UseSnakeCaseNamingConvention()
                .AddInterceptors(
                    serviceProvider.GetRequiredService<AuditableEntityInterceptor>(),
                    serviceProvider.GetRequiredService<DomainEventDispatchInterceptor>(),
                    serviceProvider.GetRequiredService<NewOwnedChildEntityInterceptor>()));

        services
            .AddHealthChecks()
            .AddNpgSql(connectionString, name: "postgres", tags: ["ready"]);

        services.AddCaching(configuration);
        services.AddBackgroundJobs(configuration, connectionString);

        // Application.DependencyInjection.AddApplication() only scans the
        // Application assembly for MediatR handlers, so this second
        // registration is what actually wires up CatalogCacheInvalidationHandler
        // (and any other Infrastructure-layer handler) - without it, domain
        // events would keep dispatching, but nothing in this assembly would
        // receive them.
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));

        // Audit attribution reads the current request; without this accessor
        // every user action would be silently attributed to the system.
        services.AddHttpContextAccessor();
        services.AddScoped<IAuditContextProvider, HttpAuditContextProvider>();
        services.AddScoped<IAuditLogWriter, AuditLogWriter>();

        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IServiceRepository, ServiceRepository>();
        services.AddScoped<IServiceAddOnRepository, ServiceAddOnRepository>();
        services.AddScoped<ISlotBlackoutRepository, SlotBlackoutRepository>();
        services.AddScoped<ISlotBookingPolicyRepository, SlotBookingPolicyRepository>();
        services.AddScoped<ISlotWindowRepository, SlotWindowRepository>();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<ISlotAvailabilityService, SlotAvailabilityService>();
        services.AddScoped<IServiceCityPriceRepository, ServiceCityPriceRepository>();
        services.AddScoped<ICityPricingPolicyRepository, CityPricingPolicyRepository>();
        services.AddScoped<IPriceCalculationService, PriceCalculationService>();
        services.AddScoped<IServiceabilityRepository, ServiceabilityRepository>();
        services.AddScoped<IServiceabilityValidationService, ServiceabilityValidationService>();
        services.AddScoped<IGeographyRepository, GeographyRepository>();
        services.AddScoped<IGeographyQueryService, GeographyQueryService>();
        services.AddScoped<ICategoryQueryService, CategoryQueryService>();
        services.AddScoped<IServiceQueryService, ServiceQueryService>();
        services.AddScoped<ICatalogSearchService, CatalogSearchService>();
        services.AddScoped<ICustomerAddressRepository, CustomerAddressRepository>();
        services.AddScoped<IBookingSummaryService, BookingSummaryService>();
        services.AddScoped<IBookingRepository, BookingRepository>();
        services.AddScoped<IBookingService, BookingService>();
        services.AddScoped<ICustomerAuthIdentityRepository, CustomerAuthIdentityRepository>();
        services.AddScoped<ICustomerSessionRepository, CustomerSessionRepository>();
        services.AddScoped<ILoginAttemptRepository, LoginAttemptRepository>();
        services.AddScoped<IOTPService, OtpService>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<ICustomerRegistrationService, CustomerRegistrationService>();
        services.AddScoped<ICustomerLoginService, CustomerLoginService>();
        services.AddScoped<ICustomerPasswordResetService, CustomerPasswordResetService>();
        services.AddScoped<ICustomerCommunicationPreferenceRepository, CustomerCommunicationPreferenceRepository>();
        services.AddScoped<ICustomerProfileService, CustomerProfileService>();

        // Stateless - depends only on bound Options - so one shared instance
        // safely serves both interfaces (SandboxPaymentGateway implements
        // IPaymentGateway and the sandbox-only ISandboxPaymentSimulator).
        services.AddSingleton<SandboxPaymentGateway>();
        services.AddSingleton<IPaymentGateway>(sp => sp.GetRequiredService<SandboxPaymentGateway>());
        services.AddSingleton<ISandboxPaymentSimulator>(sp => sp.GetRequiredService<SandboxPaymentGateway>());
        services.AddScoped<IPaymentTransactionRepository, PaymentTransactionRepository>();
        services.AddScoped<IPaymentWebhookService, PaymentWebhookService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<ICouponRepository, CouponRepository>();
        services.AddScoped<ICouponRedemptionRepository, CouponRedemptionRepository>();
        services.AddScoped<ICouponService, CouponService>();
        services.AddScoped<IWalletLedgerRepository, WalletLedgerRepository>();
        services.AddScoped<IWalletService, WalletService>();

        // Sandbox in every environment for now (SRS 30.2): no real SMS/email
        // vendor is configured yet. Swap this registration, not the callers,
        // when a production provider lands.
        services.AddScoped<INotificationProvider, SandboxNotificationProvider>();

        return services;
    }

    /// <summary>
    /// JWT bearer authentication (SRS 11.2.2). Separate from
    /// <see cref="AddInfrastructure"/> because <c>AddAuthentication</c> sets
    /// the process-wide default scheme — each API's Program.cs calls this
    /// explicitly rather than getting it silently bundled in.
    /// </summary>
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtSection = configuration.GetSection(JwtOptions.SectionName);
        var signingKey = jwtSection[nameof(JwtOptions.SigningKey)] ??
            throw new InvalidOperationException($"Configuration section '{JwtOptions.SectionName}:{nameof(JwtOptions.SigningKey)}' is not configured.");
        var issuer = jwtSection[nameof(JwtOptions.Issuer)] ?? "Nestly";
        var audience = jwtSection[nameof(JwtOptions.Audience)] ?? "Nestly.Customers";

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // Without this, the default inbound claim mapping silently
                // renames "sub" to ClaimTypes.NameIdentifier, so every
                // controller reading the customer id would have to know that
                // translation. Keep claim types exactly as TokenService issued
                // them (JwtRegisteredClaimNames.Sub, "mobile", ...jti).
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = issuer,
                    ValidateAudience = true,
                    ValidAudience = audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Convert.FromBase64String(signingKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30)
                };
            });

        services.AddAuthorization();

        return services;
    }
}
