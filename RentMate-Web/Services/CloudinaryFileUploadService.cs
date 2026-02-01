using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace RentMate.Services;

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

        return uploadResult.SecureUrl.ToString();
    }

    /// <inheritdoc/>
    public void DeleteFile(string fileUrl)
    {
        if (string.IsNullOrEmpty(fileUrl))
            return;

        try
        {
            var publicId = ExtractPublicIdFromUrl(fileUrl);
            if (!string.IsNullOrEmpty(publicId))
            {
                var deletionParams = new DeletionParams(publicId);
                _cloudinary.Destroy(deletionParams);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error deleting image from Cloudinary: {FileUrl}", fileUrl);
        }
    }

    #endregion

    #region Private Helpers

    /// <summary>
    /// Validates file size, extension, and content type.
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