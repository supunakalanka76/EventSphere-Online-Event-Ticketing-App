using EventSphere.Helpers;
using EventSphere.Models;
using System;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Web.Mvc;

namespace EventSphere.Controllers
{
    public class AdminController : BaseController
    {
        private readonly EventSphereDBEntities db = new EventSphereDBEntities();

        // Admin Dashboard
        [HttpGet]
        public ActionResult Dashboard()
        {
            if (!SessionHelper.IsAdmin())
                return RedirectToAction("Login", "Account");

            ViewBag.TotalUsers = db.Users.Count();
            ViewBag.TotalEvents = db.Events.Count();
            ViewBag.TotalBookings = db.Bookings.Count();
            ViewBag.TotalRevenue = db.Payments.Sum(p => (decimal?)p.Amount) ?? 0;

            var recentBookings = db.Bookings
                .Include("Event")
                .Include("User")
                .OrderByDescending(b => b.BookingDate)
                .Take(5)
                .ToList();

            return View(recentBookings);
        }

        // Manage Events
        [HttpGet]
        public ActionResult ManageEvents(int? categoryId)
        {
            if (!SessionHelper.IsAdmin())
                return RedirectToAction("Login", "Account");

            ViewBag.Categories = db.EventCategories.ToList();

            var events = db.Events
                .Include("Venue")
                .Include("User")
                .Include("EventCategory")
                .AsQueryable();

            if (categoryId.HasValue)
                events = events.Where(e => e.CategoryID == categoryId.Value);

            var eventList = events.OrderByDescending(e => e.StartDate).ToList();
            return View(eventList);
        }

        // Manage Users
        [HttpGet]
        public ActionResult ManageUsers()
        {
            if (!SessionHelper.IsAdmin())
                return RedirectToAction("Login", "Account");

            int adminId = SessionHelper.GetUserId().Value;

            var users = db.Users
                .Where(u => u.UserID != adminId)
                .OrderBy(u => u.Role)
                .ThenBy(u => u.FullName)
                .ToList();

            return View(users);
        }

        // Manage Bookings
        [HttpGet]
        public ActionResult ManageBookings()
        {
            if (!SessionHelper.IsAdmin())
                return RedirectToAction("Login", "Account");

            var bookings = db.Bookings
                .Include("User")
                .Include("Event")
                .OrderByDescending(b => b.BookingDate)
                .ToList();

            return View(bookings);
        }

        // Reports Page
        [HttpGet]
        public ActionResult Reports()
        {
            if (!SessionHelper.IsAdmin())
                return RedirectToAction("Login", "Account");

            return View();
        }

        // Generate Reports (PDF/Excel)
        [HttpPost]
        public ActionResult GenerateReport(string reportType, DateTime? startDate, DateTime? endDate, string format = "PDF")
        {
            if (!SessionHelper.IsAdmin())
                return RedirectToAction("Login", "Account");

            try
            {
                if (string.IsNullOrEmpty(reportType))
                {
                    TempData["Error"] = "Please select a report type.";
                    return RedirectToAction("Reports");
                }

                string exportDir = Server.MapPath("~/Reports/Generated/");
                if (!Directory.Exists(exportDir))
                    Directory.CreateDirectory(exportDir);

                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string fileName = $"{reportType}_Report_{timestamp}";
                string fullPath;

                // --- SELECT DATA BASED ON REPORT TYPE ---
                object data;
                switch (reportType)
                {
                    case "Bookings":
                        data = db.Bookings
                            .Include("User").Include("Event")
                            .Where(b => (!startDate.HasValue || b.BookingDate >= startDate)
                                     && (!endDate.HasValue || b.BookingDate <= endDate))
                            .Select(b => new
                            {
                                b.BookingID,
                                Event = b.Event.Title,
                                Customer = b.User.FullName,
                                b.BookingDate,
                                b.TotalAmount,
                                b.PaymentStatus
                            }).ToList();
                        break;

                    case "Payments":
                        data = db.Payments
                            .Include("Booking.Event").Include("Booking.User")
                            .Where(p => (!startDate.HasValue || p.PaymentDate >= startDate)
                                     && (!endDate.HasValue || p.PaymentDate <= endDate))
                            .Select(p => new
                            {
                                p.TransactionID,
                                Event = p.Booking.Event.Title,
                                Customer = p.Booking.User.FullName,
                                p.Amount,
                                p.PaymentGateway,
                                p.PaymentDate,
                                p.Status
                            }).ToList();
                        break;

                    case "Events":
                        data = db.Events
                            .Include("EventCategory").Include("Venue").Include("User")
                            .Where(e => (!startDate.HasValue || e.StartDate >= startDate)
                                     && (!endDate.HasValue || e.StartDate <= endDate))
                            .Select(e => new
                            {
                                e.EventID,
                                e.Title,
                                Category = e.EventCategory.CategoryName,
                                Venue = e.Venue.VenueName,
                                Organizer = e.User.FullName,
                                e.StartDate,
                                e.EndDate,
                                e.Status
                            }).ToList();
                        break;

                    case "Users":
                        data = db.Users
                            .Select(u => new
                            {
                                u.UserID,
                                u.FullName,
                                u.Email,
                                u.Role,
                                u.CreatedAt,
                                u.LoyaltyPoints
                            }).ToList();
                        break;

                    default:
                        TempData["Error"] = "Invalid report type selected.";
                        return RedirectToAction("Reports");
                }

                if (data == null || !((System.Collections.IEnumerable)data).Cast<object>().Any())
                {
                    TempData["Error"] = "No records found for the selected filters.";
                    return RedirectToAction("Reports");
                }

                // --- EXPORT ---
                if (format.ToUpper() == "EXCEL")
                {
                    fullPath = Path.Combine(exportDir, $"{fileName}.xlsx");
                    Type dataType = data.GetType().GetGenericArguments().FirstOrDefault() ?? typeof(object);
                    var exportToExcelMethod = typeof(ExportHelper).GetMethod("ExportToExcel").MakeGenericMethod(dataType);
                    exportToExcelMethod.Invoke(null, new object[] { data, fullPath });
                }
                else
                {
                    fullPath = Path.Combine(exportDir, $"{fileName}.pdf");
                    Type dataType = data.GetType().GetGenericArguments().FirstOrDefault() ?? typeof(object);
                    var exportToPdfMethod = typeof(ExportHelper).GetMethod("ExportToPdf").MakeGenericMethod(dataType);
                    exportToPdfMethod.Invoke(null, new object[] { data, fullPath, $"{reportType} Report" });
                }

                TempData["Success"] = $"{reportType} report generated successfully!";
                TempData["ReportPath"] = "/Reports/Generated/" + Path.GetFileName(fullPath);

                return RedirectToAction("Reports");
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error generating report: " + ex.Message;
                return RedirectToAction("Reports");
            }
        }
    }
}
