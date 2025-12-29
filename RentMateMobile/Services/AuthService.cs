using System;
using System.Threading.Tasks;
using Microsoft.Maui.Storage;

namespace RentMateMobile.Services
{
    public class AuthService
    {
        private const string TokenKey = "auth_token";
        private const string UserIdKey = "user_id";
        private const string UserEmailKey = "user_email";
        private const string UserNameKey = "user_name";

        public UserModel? CurrentUser { get; private set; }
        
        // Dodamo TaskCompletionSource, da lahko komponente počakajo na inicializacijo
        private Task? _initializeTask;

        public async Task LoginUser(string token, string userId, string email, string userName)
        {
            await SecureStorage.Default.SetAsync(TokenKey, token);
            await SecureStorage.Default.SetAsync(UserIdKey, userId);
            await SecureStorage.Default.SetAsync(UserEmailKey, email);
            await SecureStorage.Default.SetAsync(UserNameKey, userName);

            CurrentUser = new UserModel
            {
                Token = token,
                Id = userId,
                Email = email,
                Name = userName
            };
        }

        // Varno pridobivanje ID-ja, ki ga uporabiš v Marketplace.razor
        public async Task<string?> GetUserIdAsync()
        {
            if (CurrentUser?.Id != null) return CurrentUser.Id;

            // Če CurrentUser še ni v pomnilniku, ga poskusimo naložiti
            await LoadCurrentUserAsync();
            return CurrentUser?.Id;
        }

        public async Task<string?> GetToken() => await SecureStorage.Default.GetAsync(TokenKey);

        public void Logout()
        {
            SecureStorage.Default.RemoveAll();
            CurrentUser = null;
            _initializeTask = null; // Ponastavimo nalogo ob odjavi
        }

        public async Task<bool> IsAuthenticated()
        {
            var token = await GetToken();
            return !string.IsNullOrEmpty(token);
        }

        // Glavna metoda za nalaganje ob zagonu aplikacije
        public Task LoadCurrentUserAsync()
        {
            // Če se nalaganje že izvaja, vrnemo obstoječo nalogo (Singleton vzorec)
            if (_initializeTask != null) return _initializeTask;

            _initializeTask = InternalLoadAsync();
            return _initializeTask;
        }

        private async Task InternalLoadAsync()
        {
            var token = await GetToken();
            if (string.IsNullOrEmpty(token))
            {
                CurrentUser = null;
                return;
            }

            CurrentUser = new UserModel
            {
                Token = token,
                Id = await SecureStorage.Default.GetAsync(UserIdKey),
                Email = await SecureStorage.Default.GetAsync(UserEmailKey),
                Name = await SecureStorage.Default.GetAsync(UserNameKey)
            };
        }
    }

    public class UserModel
    {
        public string? Id { get; set; }
        public string? Email { get; set; }
        public string? Name { get; set; }
        public string? Token { get; set; }
    }
}