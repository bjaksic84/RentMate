// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RentMate.Models;

namespace RentMate.Areas.Identity.Pages.Account.Manage
{
    /// <summary>
    /// Page model for downloading user's personal data as JSON.
    /// </summary>
    public class DownloadPersonalDataModel : BaseIdentityPageModel
    {
        #region Constants

        private const string DownloadFileName = "PersonalData.json";
        private const string JsonContentType = "application/json";

        #endregion

        #region Dependencies

        private readonly ILogger<DownloadPersonalDataModel> _logger;

        #endregion

        #region Constructor

        public DownloadPersonalDataModel(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ILogger<DownloadPersonalDataModel> logger)
            : base(userManager, signInManager)
        {
            _logger = logger;
        }

        #endregion

        #region Page Handlers

        public IActionResult OnGet() => NotFound();

        public async Task<IActionResult> OnPostAsync()
        {
            var (user, errorResult) = await GetCurrentUserOrNotFoundAsync();
            if (errorResult != null) return errorResult;

            _logger.LogInformation("User with ID '{UserId}' asked for their personal data.", GetCurrentUserId());

            var personalData = await CollectPersonalDataAsync(user);
            return CreateJsonFileResult(personalData);
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// Collects all personal data for the user.
        /// </summary>
        private async Task<Dictionary<string, string>> CollectPersonalDataAsync(ApplicationUser user)
        {
            var personalData = new Dictionary<string, string>();

            // Add properties marked with [PersonalData] attribute
            AddPersonalDataProperties(user, personalData);

            // Add external login information
            await AddExternalLoginsAsync(user, personalData);

            // Add authenticator key
            var authenticatorKey = await UserManager.GetAuthenticatorKeyAsync(user);
            personalData.Add("Authenticator Key", authenticatorKey);

            return personalData;
        }

        /// <summary>
        /// Adds properties marked with PersonalData attribute to the dictionary.
        /// </summary>
        private static void AddPersonalDataProperties(ApplicationUser user, Dictionary<string, string> personalData)
        {
            var personalDataProps = typeof(ApplicationUser).GetProperties()
                .Where(prop => Attribute.IsDefined(prop, typeof(PersonalDataAttribute)));

            foreach (var property in personalDataProps)
            {
                var value = property.GetValue(user)?.ToString() ?? "null";
                personalData.Add(property.Name, value);
            }
        }

        /// <summary>
        /// Adds external login provider information to the dictionary.
        /// </summary>
        private async Task AddExternalLoginsAsync(ApplicationUser user, Dictionary<string, string> personalData)
        {
            var logins = await UserManager.GetLoginsAsync(user);
            foreach (var login in logins)
            {
                var key = $"{login.LoginProvider} external login provider key";
                personalData.Add(key, login.ProviderKey);
            }
        }

        /// <summary>
        /// Creates a JSON file download result.
        /// </summary>
        private FileContentResult CreateJsonFileResult(Dictionary<string, string> data)
        {
            Response.Headers.TryAdd("Content-Disposition", $"attachment; filename={DownloadFileName}");
            var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(data);
            return new FileContentResult(jsonBytes, JsonContentType);
        }

        #endregion
    }
}
