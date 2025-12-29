using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Maui.Storage;

namespace RentMateMobile.Services // DODAJ TO VRSTICO
{
    public interface IImageService
    {
        Task<string> PickAndUploadImageAsync();
    }

    public class ImageService : IImageService
    {
        private readonly Cloudinary _cloudinary;

        public ImageService()
        {
            // Za Unsigned upload SDK potrebuje le CloudName. 
            // API Key in Secret sta lahko poljubna niza (npr. "prazno").
            var account = new Account("dojrvp8sj", "prazno", "prazno");
            _cloudinary = new Cloudinary(account);
        }

        public async Task<string> PickAndUploadImageAsync()
        {
            try
            {
                var photo = await MediaPicker.Default.PickPhotoAsync();
                if (photo == null) return null;

                using var stream = await photo.OpenReadAsync();

                var uploadParams = new ImageUploadParams()
                {
                    File = new FileDescription(photo.FileName, stream),
                    UploadPreset = "ml_default", // Tvoj preset iz screenshot-a
                    Folder = "rentmate/items",
                    Unsigned = true // IZRECNO določimo unsigned način
                };

                var uploadResult = await _cloudinary.UploadAsync(uploadParams);

                if (uploadResult.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    return uploadResult.SecureUrl.ToString();
                }
                else
                {
                    Console.WriteLine($"Cloudinary SDK Error: {uploadResult.Error?.Message}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SDK Napaka: {ex.Message}");
            }
            return null;
        }
    }
}