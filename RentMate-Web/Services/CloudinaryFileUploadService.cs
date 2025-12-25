using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using RentMate.Services;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace RentMate.Services
{
    public class CloudinaryFileUploadService : IFileUploadService
    {
        private readonly Cloudinary _cloudinary;

        public CloudinaryFileUploadService(IConfiguration config)
        {
            // Initialize Cloudinary account with credentials from appsettings.json
            var account = new Account(
                config["Cloudinary:CloudName"],
                config["Cloudinary:ApiKey"],
                config["Cloudinary:ApiSecret"]
            );

            _cloudinary = new Cloudinary(account);
            _cloudinary.Api.Secure = true; // Always use HTTPS
        }

        public async Task<string> UploadFileAsync(IFormFile file, string folderName)
        {
            if (file == null || file.Length == 0)
            {
                return null;
            }

            // Open stream to read the file
            using var stream = file.OpenReadStream();

            var uploadParams = new ImageUploadParams
            {
                File = new FileDescription(file.FileName, stream),
                // Folder organization: rentmate/profiles/image.jpg
                Folder = $"rentmate/{folderName}", 
                
                // OPTIMIZATION:
                // If a user uploads a high-resolution image (e.g., 4k), Cloudinary automatically
                // resizes it to a maximum of 1920px width/height while maintaining aspect ratio.
                // This saves storage and speeds up loading while keeping high quality.
                Transformation = new Transformation().Width(1920).Height(1920).Crop("limit") 
            };

            // Execute upload
            var uploadResult = await _cloudinary.UploadAsync(uploadParams);

            // Return the SecureUrl (https) to be stored in the database
            return uploadResult.SecureUrl.ToString();
        }

        public void DeleteFile(string fileUrl)
        {
            if (string.IsNullOrEmpty(fileUrl)) return;

            try
            {
                // Logic for extracting PublicId from the URL
                // URL example: https://res.cloudinary.com/demo/image/upload/v12345678/rentmate/profiles/myimage.jpg
                // PublicId: rentmate/profiles/myimage
                
                var uri = new Uri(fileUrl);
                var pathSegments = uri.AbsolutePath.Split('/');
                
                // Find the segment where our "rentmate" root folder starts
                // This logic assumes the root folder is always named "rentmate"
                string publicId = "";
                int startIndex = Array.IndexOf(pathSegments, "rentmate");
                
                if (startIndex != -1)
                {
                    // Combine all segments starting from "rentmate" and remove the file extension
                    string fullPath = string.Join("/", pathSegments.Skip(startIndex));
                    publicId = System.IO.Path.ChangeExtension(fullPath, null);
                }

                if (!string.IsNullOrEmpty(publicId))
                {
                    var deletionParams = new DeletionParams(publicId);
                    _cloudinary.Destroy(deletionParams);
                }
            }
            catch (Exception ex)
            {
                // If deletion fails (e.g., malformed URL), we don't stop the application flow.
                // In production, log the error to a logging service.
                Console.WriteLine($"Error deleting image: {fileUrl}. Exception: {ex.Message}");
            }
        }
    }
}