using System;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using EventSphere.Models;
using EventSphere.Helpers;

namespace EventSphere.Controllers
{
    public class AccountController : BaseController
    {
        private readonly EventSphereDBEntities db = new EventSphereDBEntities();

        // REGISTER (Customer)
        [HttpGet]
        public ActionResult Register()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Register(User model)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    if (db.Users.Any(u => u.Email == model.Email))
                    {
                        ViewBag.Error = "Email already exists.";
                        return View(model);
                    }

                    model.PasswordHash = PasswordHelper.HashPassword(model.PasswordHash);
                    model.Role = "Customer"; // default self-registration role
                    model.AccountStatus = "Active";
                    model.ProfileImage = "/Content/ProfileImages/default-profile.png";
                    model.LoyaltyPoints = 0;
                    model.CreatedAt = DateTime.Now;

                    db.Users.Add(model);
                    db.SaveChanges();

                    TempData["Success"] = "Registration successful! Please log in.";
                    return RedirectToAction("Login");
                }
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Registration failed: " + ex.Message;
            }

            return View(model);
        }

        // LOGIN
        [HttpGet]
        public ActionResult Login()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Login(string email, string password)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
                {
                    ViewBag.Error = "Please enter your email and password.";
                    return View();
                }

                var user = db.Users.FirstOrDefault(u => u.Email == email);
                if (user == null || !PasswordHelper.VerifyPassword(password, user.PasswordHash))
                {
                    ViewBag.Error = "Invalid email or password.";
                    return View();
                }

                if (user.AccountStatus?.ToLower() == "inactive")
                {
                    ViewBag.Error = "Your account is inactive. Please contact support.";
                    return View();
                }

                SessionHelper.SetUserSession(user.UserID, user.FullName, user.Role);

                // Redirect by role
                string role = user.Role?.ToLower() ?? "customer";
                switch (role)
                {
                    case "admin":
                        return RedirectToAction("Dashboard", "Admin");
                    case "organizer":
                        return RedirectToAction("Dashboard", "Organizer");
                    default:
                        return RedirectToAction("Index", "Home");
                }
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Login failed: " + ex.Message;
            }

            return View();
        }

        // LOGOUT
        public ActionResult Logout()
        {
            SessionHelper.ClearSession();
            return RedirectToAction("Login");
        }

        // LOYALTY POINTS PAGE
        [HttpGet]
        public ActionResult Loyalty()
        {
            if (!SessionHelper.IsLoggedIn())
                return RedirectToAction("Login");

            int userId = SessionHelper.GetUserId().Value;
            var user = db.Users.Find(userId);
            if (user == null)
                return RedirectToAction("Login");

            var transactions = db.LoyaltyTransactions
                .Where(t => t.UserID == userId)
                .OrderByDescending(t => t.TransactionDate)
                .ToList();

            ViewBag.Transactions = transactions;
            ViewBag.LayoutPath = GetLayoutForRole(user.Role);

            return View(user);
        }

        // PROFILE (GET)
        [HttpGet]
        public ActionResult MyProfile()
        {
            if (!SessionHelper.IsLoggedIn())
                return RedirectToAction("Login");

            int userId = SessionHelper.GetUserId().Value;
            var user = db.Users.Find(userId);
            if (user == null)
                return RedirectToAction("Login");

            ViewBag.LayoutPath = GetLayoutForRole(user.Role);
            return View(user);
        }

        // PROFILE (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult MyProfile(User updatedUser, HttpPostedFileBase ProfileImageFile)
        {
            if (!SessionHelper.IsLoggedIn())
                return RedirectToAction("Login");

            int userId = SessionHelper.GetUserId().Value;
            var user = db.Users.Find(userId);
            if (user == null)
                return RedirectToAction("Login");

            try
            {
                user.FullName = updatedUser.FullName;
                user.Phone = updatedUser.Phone;
                user.Address = updatedUser.Address;

                // Handle image upload
                if (ProfileImageFile != null && ProfileImageFile.ContentLength > 0)
                {
                    string uploadDir = Server.MapPath("~/Content/ProfileImages/");
                    if (!Directory.Exists(uploadDir))
                        Directory.CreateDirectory(uploadDir);

                    string fileName = $"{Guid.NewGuid()}{Path.GetExtension(ProfileImageFile.FileName)}";
                    string filePath = Path.Combine(uploadDir, fileName);
                    ProfileImageFile.SaveAs(filePath);
                    user.ProfileImage = "/Content/ProfileImages/" + fileName;
                }

                user.UpdatedAt = DateTime.Now;
                db.SaveChanges();

                TempData["Success"] = "Profile updated successfully!";
                return RedirectToAction("MyProfile");
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Error updating profile: " + ex.Message;
                ViewBag.LayoutPath = GetLayoutForRole(user.Role);
                return View(user);
            }
        }

        // PRIVATE HELPER
        private string GetLayoutForRole(string role)
        {
            switch (role)
            {
                case "Admin":
                    return "~/Views/Shared/_LayoutAdmin.cshtml";
                case "Organizer":
                    return "~/Views/Shared/_LayoutOrganizer.cshtml";
                default:
                    return "~/Views/Shared/_LayoutCustomer.cshtml";
            }
        }
    }
}
