using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using RentMate.Models.Dto;
using RentMate.Services.Interfaces;

namespace RentMate.Services.Implementations;

/// <summary>
/// Cloudinary-based file upload service for managing images.
/// Handles upload validation, transformation, and deletion.
/// </summary>
public class CloudinaryFileUploadService : IFileUploadService
{
    #region Constants

    private const long MaxFileSizeBytes = 20 * 1024 * 1024; // 20MB
    private const int MaxImageDimension = 1920;
    private const string RootFolderName = "rentmate";

    private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".gif", ".webp"];
    private static readonly string[] AllowedContentTypes = ["image/jpeg", "image/png", "image/gif", "image/webp"];

    /// <summary>
    /// Known image file signatures (magic bytes) for server-side content validation.
    /// Prevents Content-Type spoofing attacks where non-image files are disguised as images.
    /// </summary>
    private static readonly byte[][] ImageSignatures =
    [
        [0xFF, 0xD8, 0xFF],                   // JPEG
        [0x89, 0x50, 0x4E, 0x47],             // PNG
        [0x47, 0x49, 0x46, 0x38],             // GIF (GIF87a / GIF89a)
        [0x52, 0x49, 0x46, 0x46],             // WebP (RIFF container)
    ];

    #endregion

    #region Fields

    private readonly Cloudinary _cloudinary;
    private readonly ILogger<CloudinaryFileUploadService> _logger;

    #endregion

    #region Constructor

    public CloudinaryFileUploadService(IConfiguration config, ILogger<CloudinaryFileUploadService> logger)
    {
        _logger = logger;

        var account = new Account(
            config["Cloudinary:CloudName"],
            config["Cloudinary:ApiKey"],
            config["Cloudinary:ApiSecret"]
        );

        _cloudinary = new Cloudinary(account);
        _cloudinary.Api.Secure = true;
    }

    #endregion

    #region Public Methods

    /// <inheritdoc/>
    public async Task<string> UploadFileAsync(IFormFile file, string folderName)
    {
        if (file == null || file.Length == 0)
            return string.Empty;

        ValidateFile(file);

        using var stream = file.OpenReadStream();
        var uploadParams = CreateUploadParams(file.FileName, stream, folderName);
        var uploadResult = await _cloudinary.UploadAsync(uploadParams);

        if (uploadResult.Error != null)
            throw new InvalidOperationException($"Cloudinary upload failed: {uploadResult.Error.Message}");

        return uploadResult.SecureUrl.ToString();
    }

    /// <inheritdoc/>
    public async Task<FileUploadResult> UploadFilesAsync(IEnumerable<IFormFile> files, string folderName)
    {
        var result = new FileUploadResult();

        foreach (var file in files)
        {
            if (file == null || file.Length == 0)
                continue;

            try
            {
                var url = await UploadFileAsync(file, folderName);
                if (!string.IsNullOrEmpty(url))
                    result.SuccessfulUrls.Add(url);
                else
                    result.FailedFileNames.Add(file.FileName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to upload file: {FileName}", file.FileName);
                result.FailedFileNames.Add(file.FileName);
            }
        }

        // If any failed, clean up the ones that succeeded to avoid orphans
        if (!result.AllSucceeded && result.SuccessfulUrls.Any())
        {
            _logger.LogWarning("Partial upload failure — cleaning up {Count} successfully uploaded files", result.SuccessfulUrls.Count);
            await DeleteFilesAsync(result.SuccessfulUrls);
            result.SuccessfulUrls.Clear();
        }

        return result;
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteFileAsync(string fileUrl)
    {
        if (string.IsNullOrEmpty(fileUrl))
            return true;

        try
        {
            var publicId = ExtractPublicIdFromUrl(fileUrl);
            if (!string.IsNullOrEmpty(publicId))
            {
                var deletionParams = new DeletionParams(publicId);
                var result = await _cloudinary.DestroyAsync(deletionParams);
                if (result.Result == "ok" || result.Result == "not found")
                    return true;

                _logger.LogWarning("Cloudinary delete returned: {Result} for {FileUrl}", result.Result, fileUrl);
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error deleting image from Cloudinary: {FileUrl}", fileUrl);
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task DeleteFilesAsync(IEnumerable<string> fileUrls)
    {
        foreach (var fileUrl in fileUrls)
        {
            await DeleteFileAsync(fileUrl);
        }
    }

    #endregion

    #region Private Helpers

    /// <summary>
    /// Validates file size, extension, content type, and magic bytes.
    /// Magic byte validation prevents Content-Type spoofing where non-image files
    /// are disguised as images (e.g., HTML with JS renamed to .jpg).
    /// </summary>
    private static void ValidateFile(IFormFile file)
    {
        if (file.Length > MaxFileSizeBytes)
            throw new InvalidOperationException("File size exceeds maximum allowed (20MB).");

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
            throw new InvalidOperationException("File type not allowed. Only images (jpg, png, gif, webp) are permitted.");

        if (!AllowedContentTypes.Contains(file.ContentType.ToLowerInvariant()))
            throw new InvalidOperationException("Invalid file content type.");

        // Verify actual file content via magic bytes (server-side, not spoofable)
        using var stream = file.OpenReadStream();
        var header = new byte[4];
        var bytesRead = stream.Read(header, 0, header.Length);

        if (bytesRead < 3 || !ImageSignatures.Any(sig =>
                header.Length >= sig.Length &&
                header.Take(sig.Length).SequenceEqual(sig)))
        {
            throw new InvalidOperationException("File content does not match a valid image format.");
        }
    }

    /// <summary>
    /// Creates Cloudinary upload parameters with optimization transformation.
    /// </summary>
    private static ImageUploadParams CreateUploadParams(string fileName, Stream stream, string folderName)
    {
        return new ImageUploadParams
        {
            File = new FileDescription(fileName, stream),
            Folder = $"{RootFolderName}/{folderName}",
            // Auto-resize high-res images to max 1920px while maintaining aspect ratio
            Transformation = new Transformation()
                .Width(MaxImageDimension)
                .Height(MaxImageDimension)
                .Crop("limit")
        };
    }

    /// <summary>
    /// Extracts Cloudinary public ID from a secure URL.
    /// </summary>
    /// <example>
    /// URL: https://res.cloudinary.com/demo/image/upload/v12345/rentmate/profiles/myimage.jpg
    /// Returns: rentmate/profiles/myimage
    /// </example>
    private static string ExtractPublicIdFromUrl(string fileUrl)
    {
        var uri = new Uri(fileUrl);
        var pathSegments = uri.AbsolutePath.Split('/');

        int startIndex = Array.IndexOf(pathSegments, RootFolderName);
        if (startIndex == -1)
            return string.Empty;

        var fullPath = string.Join("/", pathSegments.Skip(startIndex));
        return Path.ChangeExtension(fullPath, null);
    }

    #endregion
}
