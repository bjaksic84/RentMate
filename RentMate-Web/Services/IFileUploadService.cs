using Microsoft.AspNetCore.Http;

namespace RentMate.Services
{
    public interface IFileUploadService
    {
        /// <summary>
        /// Naloži datoteko v določeno mapo (npr. "profiles", "items").
        /// Vrne varen HTTPS URL do slike.
        /// </summary>
        Task<string> UploadFileAsync(IFormFile file, string folderName);

        /// <summary>
        /// Izbriše datoteko iz oblačne shrambe na podlagi URL-ja.
        /// </summary>
        void DeleteFile(string fileUrl);
    }
}