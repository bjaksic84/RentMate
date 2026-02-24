using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using RentMate.Models.Dto;

namespace RentMate.Services.Interfaces
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
        /// Uploads multiple files to a specific folder.
        /// If any upload fails, all successfully uploaded files are cleaned up automatically.
        /// </summary>
        /// <param name="files">The files provided via HTTP request.</param>
        /// <param name="folderName">The target subfolder name.</param>
        /// <returns>A result containing successful URLs and any failed file names.</returns>
        Task<FileUploadResult> UploadFilesAsync(IEnumerable<IFormFile> files, string folderName);

        /// <summary>
        /// Deletes a file from the cloud storage based on its URL.
        /// </summary>
        /// <param name="fileUrl">The full secure URL of the file to be removed.</param>
        /// <returns>True if the file was successfully deleted or didn't exist.</returns>
        Task<bool> DeleteFileAsync(string fileUrl);

        /// <summary>
        /// Deletes multiple files from cloud storage based on their URLs.
        /// </summary>
        /// <param name="fileUrls">The full secure URLs of the files to be removed.</param>
        Task DeleteFilesAsync(IEnumerable<string> fileUrls);
    }
}
