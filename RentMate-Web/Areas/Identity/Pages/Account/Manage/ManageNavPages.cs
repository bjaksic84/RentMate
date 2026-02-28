// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using Microsoft.AspNetCore.Mvc.Rendering;

namespace RentMate.Areas.Identity.Pages.Account.Manage
{
    /// <summary>
    /// Helper class for managing navigation page states in the account management section.
    /// Provides page name constants and active state detection for navigation styling.
    /// </summary>
    public static class ManageNavPages
    {
        #region Page Name Constants

        public static string Index => "Index";
        public static string Email => "Email";
        public static string ChangePassword => "ChangePassword";
        public static string DownloadPersonalData => "DownloadPersonalData";
        public static string DeletePersonalData => "DeletePersonalData";
        public static string ExternalLogins => "ExternalLogins";
        public static string PersonalData => "PersonalData";
        public static string TwoFactorAuthentication => "TwoFactorAuthentication";
        public static string PaymentMethods => "PaymentMethods";

        #endregion

        #region Navigation Class Helpers

        /// <summary>Returns "active" CSS class if Index page is currently displayed.</summary>
        public static string IndexNavClass(ViewContext viewContext) => PageNavClass(viewContext, Index);

        /// <summary>Returns "active" CSS class if Email page is currently displayed.</summary>
        public static string EmailNavClass(ViewContext viewContext) => PageNavClass(viewContext, Email);

        /// <summary>Returns "active" CSS class if ChangePassword page is currently displayed.</summary>
        public static string ChangePasswordNavClass(ViewContext viewContext) => PageNavClass(viewContext, ChangePassword);

        /// <summary>Returns "active" CSS class if DownloadPersonalData page is currently displayed.</summary>
        public static string DownloadPersonalDataNavClass(ViewContext viewContext) => PageNavClass(viewContext, DownloadPersonalData);

        /// <summary>Returns "active" CSS class if DeletePersonalData page is currently displayed.</summary>
        public static string DeletePersonalDataNavClass(ViewContext viewContext) => PageNavClass(viewContext, DeletePersonalData);

        /// <summary>Returns "active" CSS class if ExternalLogins page is currently displayed.</summary>
        public static string ExternalLoginsNavClass(ViewContext viewContext) => PageNavClass(viewContext, ExternalLogins);

        /// <summary>Returns "active" CSS class if PersonalData page is currently displayed.</summary>
        public static string PersonalDataNavClass(ViewContext viewContext) => PageNavClass(viewContext, PersonalData);

        /// <summary>Returns "active" CSS class if TwoFactorAuthentication page is currently displayed.</summary>
        public static string TwoFactorAuthenticationNavClass(ViewContext viewContext) => PageNavClass(viewContext, TwoFactorAuthentication);

        /// <summary>Returns "active" CSS class if PaymentMethods page is currently displayed.</summary>
        public static string PaymentMethodsNavClass(ViewContext viewContext) => PageNavClass(viewContext, PaymentMethods);

        #endregion

        #region Core Logic

        /// <summary>
        /// Determines if the specified page is the currently active page.
        /// </summary>
        /// <param name="viewContext">The current view context.</param>
        /// <param name="page">The page name to check.</param>
        /// <returns>"active" if the page is current, null otherwise.</returns>
        public static string PageNavClass(ViewContext viewContext, string page)
        {
            var activePage = viewContext.ViewData["ActivePage"] as string
                ?? System.IO.Path.GetFileNameWithoutExtension(viewContext.ActionDescriptor.DisplayName);
            
            return string.Equals(activePage, page, StringComparison.OrdinalIgnoreCase) ? "active" : null;
        }

        #endregion
    }
}
