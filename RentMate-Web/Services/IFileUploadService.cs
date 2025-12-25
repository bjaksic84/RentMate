using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace RentMate.Services
{
    public interface IFileUploadService
    {
        /// <summary>
        /// Uploads a file to a specific folder (e.g., "profiles", "items").
        /// Returns a secure HTTPS URL to the uploaded image.
        /// </summary>
        /// <param name="file">The file provided via HTTP request.</param>
        /// <param name="folderName">The target subfolder name.</param>
        /// <returns>The secure URL string of the uploaded file.</returns>
        Task<string> UploadFileAsync(IFormFile file, string folderName);

        /// <summary>
        /// Deletes a file from the cloud storage based on its URL.
        /// </summary>
        /// <param name="fileUrl">The full secure URL of the file to be removed.</param>
        void DeleteFile(string fileUrl);
    }
}