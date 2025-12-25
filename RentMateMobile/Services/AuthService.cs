using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RentMateMobile.Services
{
    public class AuthService
    {
        private const string TokenKey = "auth_token";
        private const string UserIdKey = "user_id";
        private const string UserEmailKey = "user_email";
        private const string UserNameKey = "user_name";

        // Shranimo vse podatke hkrati
        public async Task LoginUser(string token, string userId, string email, string userName)
        {
            await SecureStorage.Default.SetAsync(TokenKey, token);
            await SecureStorage.Default.SetAsync(UserIdKey, userId);
            await SecureStorage.Default.SetAsync(UserEmailKey, email);
            await SecureStorage.Default.SetAsync(UserNameKey, userName);
        }

        public async Task<string?> GetToken() => await SecureStorage.Default.GetAsync(TokenKey);
        public async Task<string?> GetUserId() => await SecureStorage.Default.GetAsync(UserIdKey);
        public async Task<string?> GetUserEmail() => await SecureStorage.Default.GetAsync(UserEmailKey);
        public async Task<string?> GetUserName() => await SecureStorage.Default.GetAsync(UserNameKey);

        public void Logout()
        {
            SecureStorage.Default.RemoveAll();
        }

        public async Task<bool> IsAuthenticated()
        {
            var token = await GetToken();
            return !string.IsNullOrEmpty(token);
        }
    }
}
