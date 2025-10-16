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
            HttpContext.Current.Session["UserRole"] = role?.Trim();
        }

        // GET SESSION VALUES
        public static int? GetUserId()
        {
            EnsureSessionExists();

            var id = HttpContext.Current.Session["UserID"];
            return id != null ? Convert.ToInt32(id) : (int?)null;
        }

        public static string GetUserName()
        {
            EnsureSessionExists();
            return HttpContext.Current.Session["UserName"] as string ?? "Guest";
        }

        public static string GetUserRole()
        {
            EnsureSessionExists();
            return HttpContext.Current.Session["UserRole"] as string ?? "Guest";
        }

        // PROFILE IMAGE HANDLING
        public static string GetUserProfileImage()
        {
            return HttpContext.Current.Session["UserProfileImage"]?.ToString() ?? "/Content/ProfileImages/default-profile.png";
        }

        // Overloaded method to set profile image along with other session values
        public static void SetUserSession(int userId, string fullName, string role, string profileImage = null)
        {
            HttpContext.Current.Session["UserId"] = userId;
            HttpContext.Current.Session["UserName"] = fullName;
            HttpContext.Current.Session["UserRole"] = role;
            HttpContext.Current.Session["UserProfileImage"] = profileImage ?? "/Content/ProfileImages/default-profile.png";
        }


        // ROLE CHECKING HELPERS
        public static bool IsAdmin()
        {
            return string.Equals(GetUserRole(), "Admin", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsOrganizer()
        {
            return string.Equals(GetUserRole(), "Organizer", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsCustomer()
        {
            return string.Equals(GetUserRole(), "Customer", StringComparison.OrdinalIgnoreCase);
        }

        // SESSION STATE MANAGEMENT
        public static bool IsLoggedIn()
        {
            EnsureSessionExists();
            return HttpContext.Current.Session["UserID"] != null;
        }

        public static void ClearSession()
        {
            EnsureSessionExists();
            HttpContext.Current.Session.Clear();
            HttpContext.Current.Session.Abandon();
        }

        // SAFETY CHECK
        private static void EnsureSessionExists()
        {
            if (HttpContext.Current == null)
                throw new InvalidOperationException("HttpContext is not available.");

            if (HttpContext.Current.Session == null)
                throw new InvalidOperationException("Session is not available in the current context.");
        }
    }
}
