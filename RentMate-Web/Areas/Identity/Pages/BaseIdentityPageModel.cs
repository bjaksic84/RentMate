using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RentMate.Models.Domain;

namespace RentMate.Areas.Identity.Pages
{
    /// <summary>
    /// Base page model providing common functionality for Identity pages.
    /// Reduces code duplication across account management pages.
    /// </summary>
    public abstract class BaseIdentityPageModel : PageModel
    {
        #region Dependencies

        protected readonly UserManager<ApplicationUser> UserManager;
        protected readonly SignInManager<ApplicationUser> SignInManager;

        #endregion

        #region Constructor

        protected BaseIdentityPageModel(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager)
        {
            UserManager = userManager;
            SignInManager = signInManager;
        }

        #endregion

        #region User Helpers

        /// <summary>
        /// Gets the current authenticated user.
        /// </summary>
        protected Task<ApplicationUser?> GetCurrentUserAsync()
        {
            return UserManager.GetUserAsync(User);
        }

        /// <summary>
        /// Gets the current user's ID.
        /// </summary>
        protected string? GetCurrentUserId()
        {
            return UserManager.GetUserId(User);
        }

        /// <summary>
        /// Gets the current user or returns a NotFound result if not found.
        /// </summary>
        protected async Task<(ApplicationUser? User, IActionResult? ErrorResult)> GetCurrentUserOrNotFoundAsync()
        {
            var user = await GetCurrentUserAsync();
            if (user == null)
            {
                var userId = GetCurrentUserId();
                return (null, NotFound($"Unable to load user with ID '{userId}'."));
            }
            return (user, null);
        }

        /// <summary>
        /// Refreshes the user's sign-in cookie after changes.
        /// </summary>
        protected Task RefreshSignInAsync(ApplicationUser user)
        {
            return SignInManager.RefreshSignInAsync(user);
        }

        #endregion

        #region Status Message

        /// <summary>
        /// Status message to display to the user.
        /// </summary>
        [TempData]
        public string? StatusMessage { get; set; }

        /// <summary>
        /// Sets a success status message.
        /// </summary>
        protected void SetSuccessMessage(string message)
        {
            StatusMessage = message;
        }

        /// <summary>
        /// Sets an error status message.
        /// </summary>
        protected void SetErrorMessage(string message)
        {
            StatusMessage = $"Error: {message}";
        }

        #endregion

        #region Validation Helpers

        /// <summary>
        /// Adds identity errors to the model state.
        /// </summary>
        protected void AddIdentityErrors(IdentityResult result)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
        }

        /// <summary>
        /// Checks if the model state is valid and returns the page if not.
        /// </summary>
        protected bool IsModelStateInvalid()
        {
            return !ModelState.IsValid;
        }

        #endregion

        #region User Factory Helpers

        /// <summary>
        /// Creates a new ApplicationUser instance.
        /// </summary>
        protected static ApplicationUser CreateUserInstance()
        {
            try
            {
                return Activator.CreateInstance<ApplicationUser>();
            }
            catch
            {
                throw new InvalidOperationException(
                    $"Can't create an instance of '{nameof(ApplicationUser)}'. " +
                    $"Ensure that '{nameof(ApplicationUser)}' is not an abstract class and has a parameterless constructor.");
            }
        }

        /// <summary>
        /// Gets the email store from the user store.
        /// </summary>
        protected IUserEmailStore<ApplicationUser> GetEmailStore(IUserStore<ApplicationUser> userStore)
        {
            if (!UserManager.SupportsUserEmail)
            {
                throw new NotSupportedException("The default UI requires a user store with email support.");
            }
            return (IUserEmailStore<ApplicationUser>)userStore;
        }

        #endregion
    }
}
