namespace RentMate.Helpers;

/// <summary>
/// Centralized constants for Views to ensure consistency and DRY compliance.
/// </summary>
public static class ViewConstants
{
    #region Categories
    
    /// <summary>
    /// Item categories for listings (Create/Edit forms).
    /// </summary>
    public static readonly string[] ItemCategories = 
    {
        "Uncategorized", "Electronics", "Sports", "Tools", "Vehicles", "Home", "Garden", "Events", "Other"
    };
    
    /// <summary>
    /// Category options for marketplace filtering with icons.
    /// </summary>
    public static readonly (string Name, string Icon)[] MarketplaceCategories =
    {
        ("Tools", "bi-tools"),
        ("Electronics", "bi-laptop"),
        ("Sports", "bi-bicycle"),
        ("Home & Garden", "bi-house"),
        ("Fun", "bi-controller"),
        ("Other", "bi-three-dots")
    };
    
    /// <summary>
    /// Filter categories for the Rentals/Marketplace view.
    /// </summary>
    public static readonly string[] FilterCategories = 
    {
        "Tools", "Electronics", "Sports", "Home & Garden", "Fun", "Other"
    };
    
    #endregion
    
    #region Icons
    
    /// <summary>
    /// Common icon mappings for consistent UI.
    /// </summary>
    public static class Icons
    {
        public const string Search = "bi-search";
        public const string Location = "bi-geo-alt";
        public const string Calendar = "bi-calendar";
        public const string User = "bi-person";
        public const string Heart = "bi-heart";
        public const string HeartFill = "bi-heart-fill";
        public const string Star = "bi-star";
        public const string StarFill = "bi-star-fill";
        public const string Edit = "bi-pencil";
        public const string Delete = "bi-trash";
        public const string View = "bi-eye";
        public const string Hide = "bi-eye-slash";
        public const string Add = "bi-plus-lg";
        public const string Back = "bi-arrow-left";
        public const string Forward = "bi-arrow-right";
        public const string Close = "bi-x-lg";
        public const string Check = "bi-check-lg";
        public const string Warning = "bi-exclamation-triangle";
        public const string Info = "bi-info-circle";
        public const string Image = "bi-image";
        public const string Camera = "bi-camera";
        public const string Upload = "bi-upload";
    }
    
    #endregion
    
    #region CSS Classes
    
    /// <summary>
    /// Common button style classes.
    /// </summary>
    public static class ButtonStyles
    {
        public const string Primary = "px-6 py-3 bg-gradient-to-r from-blue-600 to-blue-500 hover:from-blue-700 hover:to-blue-600 text-white font-semibold rounded-xl shadow-sm transition-all";
        public const string Secondary = "px-6 py-3 border border-slate-200 text-slate-600 hover:bg-slate-50 rounded-xl font-medium transition-colors";
        public const string Danger = "px-6 py-3 bg-red-600 hover:bg-red-700 text-white font-semibold rounded-xl transition-colors";
        public const string Ghost = "px-4 py-2 text-slate-500 hover:text-blue-600 hover:bg-blue-50 rounded-lg transition-colors";
    }
    
    /// <summary>
    /// Common input field classes.
    /// </summary>
    public const string InputClass = "w-full px-4 py-3 border border-slate-300 rounded-xl focus:ring-2 focus:ring-blue-500 focus:border-blue-500 transition-colors";
    
    /// <summary>
    /// Common card container classes.
    /// </summary>
    public const string CardClass = "bg-white rounded-2xl border border-slate-200 p-6";
    
    #endregion
    
    #region Pagination
    
    public const int DefaultPageSize = 12;
    public const int DefaultMaxPageButtons = 5;
    
    #endregion
    
    #region Status Mappings
    
    /// <summary>
    /// Maps rental/item status strings to badge types for _StatusBadge partial.
    /// </summary>
    public static string GetStatusType(string status) => status?.ToLower() switch
    {
        "active" => "active",
        "pending" => "pending",
        "completed" => "completed",
        "cancelled" or "canceled" => "cancelled",
        "banned" => "banned",
        "hidden" => "hidden",
        _ => "default"
    };
    
    #endregion
    
    #region Z-Index Scale
    
    /// <summary>
    /// Z-Index scale for consistent layering across the application.
    /// Use CSS variables: var(--z-{level}) in views.
    /// 
    /// Hierarchy (lowest to highest):
    /// - Base (1): Default content layer
    /// - Dropdown (100): Dropdowns, select menus
    /// - Sticky (200): Sticky sidebars, headers
    /// - Fixed (300): Fixed elements (not navbar)
    /// - MobileDrawer (400): Mobile navigation drawer
    /// - Navbar (900): Navigation bar
    /// - ModalBackdrop (1100): Modal overlay/backdrop (above Leaflet 1000)
    /// - Modal (1200): Modal dialogs
    /// - Popover (1300): Popovers, calendar panels
    /// - Tooltip (1400): Tooltips
    /// - Toast (1500): Toast notifications (always on top)
    /// </summary>
    public static class ZIndex
    {
        public const string Base = "var(--z-base, 1)";
        public const string Dropdown = "var(--z-dropdown, 100)";
        public const string Sticky = "var(--z-sticky, 200)";
        public const string Fixed = "var(--z-fixed, 300)";
        public const string MobileDrawer = "var(--z-mobile-drawer, 400)";
        public const string Navbar = "var(--z-navbar, 900)";
        public const string ModalBackdrop = "var(--z-modal-backdrop, 1100)";
        public const string Modal = "var(--z-modal, 1200)";
        public const string Popover = "var(--z-popover, 1300)";
        public const string Tooltip = "var(--z-tooltip, 1400)";
        public const string Toast = "var(--z-toast, 1500)";
    }
    
    #endregion
}
