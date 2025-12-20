using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RentMateMobile.Services
{
    // RentMateMobile/Services/AuthService.cs
    public class AuthService
    {
        private const string TokenKey = "auth_token";

        public async Task SaveToken(string token)
        {
            await SecureStorage.Default.SetAsync(TokenKey, token);
        }

        public async Task<string?> GetToken()
        {
            return await SecureStorage.Default.GetAsync(TokenKey);
        }

        public void Logout()
        {
            SecureStorage.Default.Remove(TokenKey);
        }

        public async Task<bool> IsAuthenticated()
        {
            var token = await GetToken();
            return !string.IsNullOrEmpty(token);
        }
    }
}
