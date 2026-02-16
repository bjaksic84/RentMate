using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

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
        /// Returns a list of secure HTTPS URLs to the uploaded images in the same order.
        /// </summary>
        /// <param name="files">The files provided via HTTP request.</param>
        /// <param name="folderName">The target subfolder name.</param>
        /// <returns>List of secure URL strings of the uploaded files.</returns>
        Task<List<string>> UploadFilesAsync(IEnumerable<IFormFile> files, string folderName);

        /// <summary>
        /// Deletes a file from the cloud storage based on its URL.
        /// </summary>
        /// <param name="fileUrl">The full secure URL of the file to be removed.</param>
        void DeleteFile(string fileUrl);

        /// <summary>
        /// Deletes multiple files from cloud storage based on their URLs.
        /// </summary>
        /// <param name="fileUrls">The full secure URLs of the files to be removed.</param>
        void DeleteFiles(IEnumerable<string> fileUrls);
    }
}
