using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using RentMate.Models.Domain;
using RentMate.Services.Interfaces;

namespace RentMate.Areas.Identity.Pages.Account.Manage
{
    /// <summary>
    /// Page model for managing saved payment methods via Stripe SetupIntents.
    /// Users can add cards and remove existing ones.
    /// </summary>
    public class PaymentMethodsModel : BaseIdentityPageModel
    {
        private readonly IPaymentService _paymentService;
        private readonly IConfiguration _configuration;

        public PaymentMethodsModel(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IPaymentService paymentService,
            IConfiguration configuration)
            : base(userManager, signInManager)
        {
            _paymentService = paymentService;
            _configuration = configuration;
        }

        /// <summary>Saved cards to display.</summary>
        public IReadOnlyList<SavedPaymentMethod> SavedCards { get; set; } = Array.Empty<SavedPaymentMethod>();

        /// <summary>Stripe publishable key for Stripe.js.</summary>
        public string PublishableKey { get; set; } = string.Empty;

        /// <summary>SetupIntent client secret for adding a new card.</summary>
        public string? SetupClientSecret { get; set; }

        /// <summary>Whether this user has at least one saved payment method.</summary>
        public bool HasPaymentMethod => SavedCards.Count > 0;

        public async Task<IActionResult> OnGetAsync()
        {
            var (user, errorResult) = await GetCurrentUserOrNotFoundAsync();
            if (errorResult != null) return errorResult;

            PublishableKey = _configuration["Stripe:PublishableKey"] ?? "";

            await LoadSavedCardsAsync(user!);

            return Page();
        }

        /// <summary>
        /// Creates a SetupIntent and returns its client secret via AJAX.
        /// Called from the front-end when the user clicks "Add card".
        /// </summary>
        public async Task<IActionResult> OnPostCreateSetupIntentAsync()
        {
            var (user, errorResult) = await GetCurrentUserOrNotFoundAsync();
            if (errorResult != null) return errorResult;

            // Ensure a Stripe customer exists
            var customerId = await _paymentService.GetOrCreateCustomerAsync(
                user!.Id, user.Email!, $"{user.FirstName} {user.LastName}".Trim());

            var result = await _paymentService.CreateSetupIntentAsync(user.Id);
            if (!result.Success)
            {
                return new JsonResult(new { success = false, error = result.ErrorMessage });
            }

            return new JsonResult(new { success = true, clientSecret = result.ClientSecret });
        }

        /// <summary>
        /// Called after Stripe confirms the SetupIntent on the front-end.
        /// Marks the user as having a payment method and refreshes the page.
        /// </summary>
        public async Task<IActionResult> OnPostConfirmSetupAsync()
        {
            var (user, errorResult) = await GetCurrentUserOrNotFoundAsync();
            if (errorResult != null) return errorResult;

            user!.HasPaymentMethodAdded = true;
            await UserManager.UpdateAsync(user);

            SetSuccessMessage("Payment method added successfully.");
            return RedirectToPage();
        }

        /// <summary>
        /// Removes a saved payment method.
        /// </summary>
        public async Task<IActionResult> OnPostRemoveAsync(string paymentMethodId)
        {
            var (user, errorResult) = await GetCurrentUserOrNotFoundAsync();
            if (errorResult != null) return errorResult;

            if (string.IsNullOrEmpty(paymentMethodId))
            {
                SetErrorMessage("Invalid payment method.");
                return RedirectToPage();
            }

            var result = await _paymentService.RemovePaymentMethodAsync(paymentMethodId);
            if (!result.Success)
            {
                SetErrorMessage($"Failed to remove card: {result.ErrorMessage}");
                return RedirectToPage();
            }

            // Check if user still has any cards left
            var customerId = await _paymentService.GetOrCreateCustomerAsync(
                user!.Id, user.Email!, $"{user.FirstName} {user.LastName}".Trim());
            var remaining = await _paymentService.ListPaymentMethodsAsync(customerId);

            user.HasPaymentMethodAdded = remaining.Count > 0;
            await UserManager.UpdateAsync(user);

            SetSuccessMessage("Payment method removed.");
            return RedirectToPage();
        }

        private async Task LoadSavedCardsAsync(ApplicationUser user)
        {
            try
            {
                var customerId = await _paymentService.GetOrCreateCustomerAsync(
                    user.Id, user.Email!, $"{user.FirstName} {user.LastName}".Trim());
                SavedCards = await _paymentService.ListPaymentMethodsAsync(customerId);
            }
            catch
            {
                // If Stripe is not configured, just show empty list
                SavedCards = Array.Empty<SavedPaymentMethod>();
            }
        }
    }
}
