using System.Security.Cryptography;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nestly.Domain;
using Nestly.Infrastructure;
using Nestly.Infrastructure.Services;
using Xunit;

namespace Nestly.Catalog.Tests;

/// <summary>
/// Covers task 307's configuration-driven selection through the real
/// <c>AddInfrastructure</c> graph, mirroring <see cref="RouteEstimateRegistrationTests"/>
/// - the one part of this feature that a compiler cannot check, because
/// picking the wrong implementation (or failing to construct it at all) only
/// shows up when the container resolves it. No push is ever sent: resolving
/// the provider parses a credential locally, it does not call Firebase.
/// </summary>
public sealed class PushNotificationRegistrationTests
{
    private static ServiceProvider BuildContainer(params (string Key, string Value)[] settings)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
            [
                new("ConnectionStrings:Database", "Host=localhost;Database=nestly_wiring_test;Username=nestly;Password=nestly"),
                // Registration only - nothing here opens a connection.
                new("BackgroundJobs:ServerEnabled", "false"),
                new("BackgroundJobs:DashboardEnabled", "false"),
                .. settings.Select(setting => new KeyValuePair<string, string?>(setting.Key, setting.Value))
            ])
            .Build();

        return new ServiceCollection()
            .AddLogging()
            .AddInfrastructure(configuration)
            .BuildServiceProvider();
    }

    [Fact]
    public void Resolves_the_sandbox_provider_when_no_key_path_is_configured()
    {
        // The default posture: the system is runnable with no Firebase
        // project at all.
        using var container = BuildContainer();

        container.GetRequiredService<IPushNotificationProvider>()
            .Should().BeOfType<SandboxPushNotificationProvider>();
    }

    [Fact]
    public void Resolves_the_sandbox_provider_when_the_configured_key_file_does_not_exist()
    {
        using var container = BuildContainer(
            ("Firebase:ServiceAccountKeyPath", @"C:\nowhere\does-not-exist.json"));

        container.GetRequiredService<IPushNotificationProvider>()
            .Should().BeOfType<SandboxPushNotificationProvider>();
    }

    [Fact]
    public void Resolves_the_sandbox_provider_when_the_kill_switch_is_off_despite_a_path()
    {
        // The lever ops pulls without deleting the credential from the
        // secret store - same convention as GoogleMaps:Enabled.
        using var container = BuildContainer(
            ("Firebase:ServiceAccountKeyPath", @"C:\nowhere\does-not-exist.json"),
            ("Firebase:Enabled", "false"));

        container.GetRequiredService<IPushNotificationProvider>()
            .Should().BeOfType<SandboxPushNotificationProvider>();
    }

    [Fact]
    public void Resolves_firebase_when_a_valid_service_account_key_file_is_configured()
    {
        var keyPath = WriteFakeServiceAccountKeyFile();
        try
        {
            using var container = BuildContainer(("Firebase:ServiceAccountKeyPath", keyPath));

            container.GetRequiredService<IPushNotificationProvider>()
                .Should().BeOfType<FirebasePushNotificationProvider>();
        }
        finally
        {
            File.Delete(keyPath);
        }
    }

    /// <summary>
    /// A structurally real (freshly generated, throwaway) service-account
    /// JSON file, in the exact shape Firebase Console's "Generate new
    /// private key" download uses - <c>CredentialFactory.FromFile&lt;ServiceAccountCredential&gt;</c>
    /// parses the PEM private key on load, so a placeholder string fails
    /// where a real (if never-registered-with-Google) RSA key succeeds.
    /// </summary>
    private static string WriteFakeServiceAccountKeyFile()
    {
        using var rsa = RSA.Create(2048);
        var privateKeyPem = rsa.ExportPkcs8PrivateKeyPem();

        var json = JsonSerializer.Serialize(new
        {
            type = "service_account",
            project_id = "nestly-wiring-test",
            private_key_id = "test-key-id",
            private_key = privateKeyPem,
            client_email = "test@nestly-wiring-test.iam.gserviceaccount.com",
            client_id = "000000000000000000000",
            auth_uri = "https://accounts.google.com/o/oauth2/auth",
            token_uri = "https://oauth2.googleapis.com/token",
            auth_provider_x509_cert_url = "https://www.googleapis.com/oauth2/v1/certs",
            client_x509_cert_url = "https://www.googleapis.com/robot/v1/metadata/x509/test%40nestly-wiring-test.iam.gserviceaccount.com",
            universe_domain = "googleapis.com",
        });

        var path = Path.Combine(Path.GetTempPath(), $"nestly-test-service-account-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, json);
        return path;
    }
}
