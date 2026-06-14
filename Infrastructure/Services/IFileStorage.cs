using Microsoft.AspNetCore.Http;

namespace Infrastructure.Services;

/// <summary>
/// Abstract file storage – could be local disk, cloud bucket, etc.
/// Only a minimal contract needed for the current feature.
/// </summary>
public interface IFileStorage
{
    /// <summary>
    /// Uploads the given file and returns the public URL (relative to the web root).
    /// </summary>
    Task<string> UploadAsync(IFormFile file);

    /// <summary>
    /// Deletes a previously uploaded file given its relative URL.
    /// </summary>
    Task DeleteAsync(string relativeUrl);
}
