using System;
using System.Web;

namespace EventSphere.Helpers
{
    /// Provides helper methods to manage user session data throughout the application.
    public static class SessionHelper
    {
        // SET SESSION VALUES

        /// Sets common session values after a successful login.
        public static void SetUserSession(int userId, string fullName, string role)
        {
            EnsureSessionExists();

            HttpContext.Current.Session["UserID"] = userId;
            HttpContext.Current.Session["UserName"] = fullName;
            HttpContext.Current.Session["UserRole"] = role;
        }

        // GET SESSION VALUES

        /// Retrieves the current user's ID from session.
        /// <returns>Nullable integer representing UserID.</returns>
        public static int? GetUserId()
        {
            EnsureSessionExists();

            if (HttpContext.Current.Session["UserID"] != null)
                return Convert.ToInt32(HttpContext.Current.Session["UserID"]);

            return null;
        }

        /// Retrieves the current user's full name from session.
        public static string GetUserName()
        {
            EnsureSessionExists();
            return HttpContext.Current.Session["UserName"] as string;
        }

        /// Retrieves the current user's role from session.
        public static string GetUserRole()
        {
            EnsureSessionExists();
            return HttpContext.Current.Session["UserRole"] as string;
        }

        // ROLE CHECKING HELPERS

        /// Returns true if the current logged-in user is an Admin.
        public static bool IsAdmin()
        {
            return string.Equals(GetUserRole(), "Admin", StringComparison.OrdinalIgnoreCase);
        }

        /// Returns true if the current logged-in user is an Organizer.
        public static bool IsOrganizer()
        {
            return string.Equals(GetUserRole(), "Organizer", StringComparison.OrdinalIgnoreCase);
        }

        /// Returns true if the current logged-in user is a Customer.
        public static bool IsCustomer()
        {
            return string.Equals(GetUserRole(), "Customer", StringComparison.OrdinalIgnoreCase);
        }

        // SESSION STATE MANAGEMENT

        /// Checks whether any user is currently logged in.
        public static bool IsLoggedIn()
        {
            EnsureSessionExists();
            return GetUserId().HasValue;
        }

        /// Clears all session data (used during logout or timeout).
        public static void ClearSession()
        {
            EnsureSessionExists();
            HttpContext.Current.Session.Clear();
            HttpContext.Current.Session.Abandon();
        }

        // SAFETY CHECK

        /// Ensures session is available before accessing it (avoids null reference errors).
        private static void EnsureSessionExists()
        {
            if (HttpContext.Current == null || HttpContext.Current.Session == null)
                throw new InvalidOperationException("Session is not available in the current context.");
        }
    }
}
