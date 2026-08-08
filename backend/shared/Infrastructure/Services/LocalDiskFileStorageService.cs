using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Nestly.Application.Storage;
using Nestly.Infrastructure.Options;

namespace Nestly.Infrastructure.Services;

/// <inheritdoc cref="IFileStorageService"/>
public class LocalDiskFileStorageService : IFileStorageService
{
    private readonly string _uploadsDirectory;
    private readonly string _requestPath;

    public LocalDiskFileStorageService(IHostEnvironment environment, IOptions<FileStorageOptions> options)
    {
        _uploadsDirectory = Path.Combine(environment.ContentRootPath, options.Value.UploadsPath);
        _requestPath = options.Value.RequestPath;
        Directory.CreateDirectory(_uploadsDirectory);
    }

    public async Task<string> SaveAsync(Stream content, string fileNameHint, string contentType, CancellationToken cancellationToken = default)
    {
        // The hint's own extension is trusted only as a display-name detail
        // never as the on-disk name; the caller (JobsController) has already
        // validated contentType against an allowlist before this runs.
        var extension = Path.GetExtension(fileNameHint);
        var fileName = $"{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(_uploadsDirectory, fileName);

        await using var destination = File.Create(fullPath);
        await content.CopyToAsync(destination, cancellationToken);

        return $"{_requestPath}/{fileName}";
    }
}
