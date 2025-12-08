using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using RentMate.Services;
using System;

namespace RentMate.Services
{
    public class CloudinaryFileUploadService : IFileUploadService
    {
        private readonly Cloudinary _cloudinary;

        public CloudinaryFileUploadService(IConfiguration config)
        {
            // Inicializacija Cloudinary računa s podatki iz appsettings.json
            var account = new Account(
                config["Cloudinary:CloudName"],
                config["Cloudinary:ApiKey"],
                config["Cloudinary:ApiSecret"]
            );

            _cloudinary = new Cloudinary(account);
            _cloudinary.Api.Secure = true; // Vedno uporabi HTTPS
        }

        public async Task<string> UploadFileAsync(IFormFile file, string folderName)
        {
            if (file == null || file.Length == 0)
            {
                return null; // Ali pa vrzi izjemo, odvisno od željene logike
            }

            // Odpremo stream za branje datoteke
            using var stream = file.OpenReadStream();

            var uploadParams = new ImageUploadParams
            {
                File = new FileDescription(file.FileName, stream),
                // Organizacija map: rentmate/profiles/slika.jpg
                Folder = $"rentmate/{folderName}", 
                
                // OPTIMIZACIJA:
                // Če uporabnik naloži 4k sliko (10MB), jo Cloudinary avtomatsko zmanjša na max 1920px širine.
                // To prihrani prostor in pospeši nalaganje, kvaliteta pa ostane visoka.
                Transformation = new Transformation().Width(1920).Height(1920).Crop("limit") 
            };

            // Izvedemo upload
            var uploadResult = await _cloudinary.UploadAsync(uploadParams);

            // Vrnemo SecureUrl (https), ki ga shranimo v bazo
            return uploadResult.SecureUrl.ToString();
        }

        public void DeleteFile(string fileUrl)
        {
            if (string.IsNullOrEmpty(fileUrl)) return;

            try
            {
                // Logika za pridobivanje PublicId iz URL-ja
                // URL primer: https://res.cloudinary.com/demo/image/upload/v12345678/rentmate/profiles/mojaslika.jpg
                // PublicId: rentmate/profiles/mojaslika
                
                var uri = new Uri(fileUrl);
                var pathSegments = uri.AbsolutePath.Split('/');
                
                // Iskanje segmenta, kjer se začne naša mapa "rentmate"
                // To je preprosta logika, ki predvideva, da se mapa vedno imenuje "rentmate"
                // Za bolj kompleksne primere se v bazo shranjuje PublicId posebej.
                
                string publicId = "";
                int startIndex = Array.IndexOf(pathSegments, "rentmate");
                
                if (startIndex != -1)
                {
                    // Združimo vse od "rentmate" naprej in odstranimo končnico (.jpg)
                    string fullPath = string.Join("/", pathSegments.Skip(startIndex));
                    publicId = System.IO.Path.ChangeExtension(fullPath, null);
                }

                if (!string.IsNullOrEmpty(publicId))
                {
                    var deletionParams = new DeletionParams(publicId);
                    _cloudinary.Destroy(deletionParams);
                }
            }
            catch 
            {
                // Če brisanje ne uspe (npr. napačen URL), ne ustavimo aplikacije.
                // Logiramo napako v realni produkciji.
                Console.WriteLine($"Napaka pri brisanju slike: {fileUrl}");
            }
        }
    }
}