using System;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using EventSphere.Models;
using EventSphere.Helpers;

namespace EventSphere.Controllers
{
    public class AccountController : Controller
    {
        private EventSphereDBEntities db = new EventSphereDBEntities();

        // REGISTER (CUSTOMER)
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
                    // Check if email already exists
                    if (db.Users.Any(u => u.Email == model.Email))
                    {
                        ViewBag.Error = "Email already exists.";
                        return View(model);
                    }

                    // Hash password
                    model.PasswordHash = PasswordHelper.HashPassword(model.PasswordHash);
                    model.Role = "Customer"; // Default role for self-registration
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
                var user = db.Users.FirstOrDefault(u => u.Email == email);

                if (user == null)
                {
                    ViewBag.Error = "Invalid email or password.";
                    return View();
                }

                bool isValid = PasswordHelper.VerifyPassword(password, user.PasswordHash);

                if (!isValid)
                {
                    ViewBag.Error = "Invalid email or password.";
                    return View();
                }

                // Set session
                SessionHelper.SetUserSession(user.UserID, user.FullName, user.Role);

                // Redirect based on role
                switch (user.Role.ToLower())
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

            return View(user);
        }

        // PROFILE (POST) - Update Profile
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
                // Update basic profile fields
                user.FullName = updatedUser.FullName;
                user.Phone = updatedUser.Phone;
                user.Address = updatedUser.Address;

                // Handle profile image upload
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

                db.SaveChanges();
                TempData["Success"] = "Profile updated successfully!";
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Error updating profile: " + ex.Message;
            }

            return View(user);
        }
    }
}
