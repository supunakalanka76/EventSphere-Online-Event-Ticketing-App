using EventSphere.Helpers;
using EventSphere.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using CrystalDecisions.CrystalReports.Engine;
using CrystalDecisions.Shared;

namespace EventSphere.Controllers
{
    public class AdminController : Controller
    {
        private EventSphereDBEntities db = new EventSphereDBEntities();

        // Dashboard (Admin Overview)
        [HttpGet]
        public ActionResult Dashboard()
        {
            if (!SessionHelper.IsAdmin())
                return RedirectToAction("Login", "Account");

            var totalUsers = db.Users.Count();
            var totalEvents = db.Events.Count();
            var totalBookings = db.Bookings.Count();
            var totalRevenue = db.Payments.Sum(p => (decimal?)p.Amount) ?? 0;

            ViewBag.TotalUsers = totalUsers;
            ViewBag.TotalEvents = totalEvents;
            ViewBag.TotalBookings = totalBookings;
            ViewBag.TotalRevenue = totalRevenue;

            // Get recent 5 bookings
            var recentBookings = db.Bookings
                .OrderByDescending(b => b.BookingDate)
                .Take(5)
                .Include("Event")
                .Include("User")
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
                .OrderByDescending(e => e.StartDate);

            if (categoryId.HasValue)
                events = events.Where(e => e.CategoryID == categoryId.Value)
                               .OrderByDescending(e => e.StartDate);

            return View(events.ToList());

        }

        // Manage Users
        [HttpGet]
        public ActionResult ManageUsers()
        {
            if (!SessionHelper.IsAdmin())
                return RedirectToAction("Login", "Account");

            int currentAdminId = SessionHelper.GetUserId().Value;

            var users = db.Users
                .Where(u => u.UserID != currentAdminId)
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

        // Reports (Crystal Reports placeholder)
        [HttpGet]
        public ActionResult Reports()
        {
            if (!SessionHelper.IsAdmin())
                return RedirectToAction("Login", "Account");

            return View();
        }

        [HttpGet]
        public ActionResult GenerateReport(string reportType, DateTime? startDate, DateTime? endDate)
        {
            if (!SessionHelper.IsAdmin())
                return RedirectToAction("Login", "Account");

            if (string.IsNullOrEmpty(reportType))
            {
                ViewBag.Error = "Please select a report type.";
                return View("Reports");
            }

            try
            {
                // Report path
                string reportFile = Server.MapPath($"~/Reports/{reportType}Report.rpt");
                if (!System.IO.File.Exists(reportFile))
                {
                    ViewBag.Error = $"Report file not found for {reportType}.";
                    return View("Reports");
                }

                // Load the report
                ReportDocument reportDoc = new ReportDocument();
                reportDoc.Load(reportFile);

                // Database connection (if needed)
                reportDoc.SetDatabaseLogon("your_db_username", "your_db_password", "your_server_name", "your_database_name");

                // Optional parameters for date filtering
                if (startDate.HasValue && endDate.HasValue)
                {
                    reportDoc.SetParameterValue("StartDate", startDate.Value);
                    reportDoc.SetParameterValue("EndDate", endDate.Value);
                }

                // Export to PDF
                string exportDir = Server.MapPath("~/Reports/Generated/");
                if (!Directory.Exists(exportDir))
                    Directory.CreateDirectory(exportDir);

                string exportFile = Path.Combine(exportDir, $"{reportType}_Report_{DateTime.Now:yyyyMMddHHmmss}.pdf");
                reportDoc.ExportToDisk(ExportFormatType.PortableDocFormat, exportFile);

                // Dispose
                reportDoc.Close();
                reportDoc.Dispose();

                // Send to view
                ViewBag.ReportPath = "~/Reports/Generated/" + Path.GetFileName(exportFile);
                ViewBag.Success = $"{reportType} Report generated successfully!";
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Error generating report: " + ex.Message;
            }

            return View("Reports");
        }
    }
}