using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using EventSphere.Models;
using EventSphere.Helpers;

namespace EventSphere.Controllers
{
    public class OrganizerController : BaseController
    {
        private readonly EventSphereDBEntities db = new EventSphereDBEntities();

        // DASHBOARD
        [HttpGet]
        public ActionResult Dashboard()
        {
            if (!SessionHelper.IsOrganizer())
                return RedirectToAction("Login", "Account");

            int organizerId = SessionHelper.GetUserId().Value;

            // Core Metrics
            ViewBag.TotalEvents = db.Events.Count(e => e.OrganizerID == organizerId);
            ViewBag.TotalBookings = db.Bookings.Count(b => b.Event.OrganizerID == organizerId);
            ViewBag.TotalRevenue = db.Payments
                .Where(p => p.Booking.Event.OrganizerID == organizerId)
                .Sum(p => (decimal?)p.Amount) ?? 0;

            // Ticket Metrics
            var ticketsSold = db.Bookings
                .Where(b => b.Event.OrganizerID == organizerId && b.PaymentStatus == "Completed")
                .Sum(b => (int?)b.Quantity) ?? 0;

            var totalTickets = db.Events
                .Where(e => e.OrganizerID == organizerId)
                .Sum(e => (int?)e.TotalTickets) ?? 0;

            var ticketsAvailable = db.Events
                .Where(e => e.OrganizerID == organizerId)
                .Sum(e => (int?)e.AvailableTickets) ?? 0;

            ViewBag.TotalTicketsSold = ticketsSold;
            ViewBag.TotalTicketsAvailable = ticketsAvailable;
            ViewBag.SalesProgress = totalTickets > 0 ? (ticketsSold * 100 / totalTickets) : 0;

            // Top Event (by revenue)
            var topEvent = db.Payments
                .Where(p => p.Booking.Event.OrganizerID == organizerId)
                .GroupBy(p => p.Booking.Event.Title)
                .Select(g => new
                {
                    EventTitle = g.Key,
                    TotalRevenue = g.Sum(p => p.Amount)
                })
                .OrderByDescending(x => x.TotalRevenue)
                .FirstOrDefault();

            ViewBag.TopEventName = topEvent?.EventTitle ?? "No Data";
            ViewBag.TopEventRevenue = topEvent?.TotalRevenue ?? 0m;

            // Monthly Sales Chart Data (last 6 months)
            var last6Months = DateTime.Now.AddMonths(-5);
            var monthlySales = db.Payments
                .Where(p => p.Booking.Event.OrganizerID == organizerId && p.PaymentDate >= last6Months)
                .GroupBy(p => new { Month = p.PaymentDate.Value.Month, Year = p.PaymentDate.Value.Year })
                .Select(g => new
                {
                    Month = g.Key.Month,
                    Year = g.Key.Year,
                    Total = g.Sum(x => x.Amount)
                })
                .ToList()
                .OrderBy(g => new DateTime(g.Year, g.Month, 1))
                .Select(g => new
                {
                    Label = new DateTime(g.Year, g.Month, 1).ToString("MMM yyyy"),
                    Value = g.Total
                })
                .ToList();

            ViewBag.MonthlySalesData = monthlySales;

            // Recent Bookings
            var recentBookings = db.Bookings
                .Include("Event")
                .Include("User")
                .Where(b => b.Event.OrganizerID == organizerId)
                .OrderByDescending(b => b.BookingDate)
                .Take(5)
                .ToList();

            return View(recentBookings);
        }

        // MANAGE EVENTS
        [HttpGet]
        public ActionResult ManageEvents()
        {
            if (!SessionHelper.IsOrganizer())
                return RedirectToAction("Login", "Account");

            int organizerId = SessionHelper.GetUserId().Value;

            var events = db.Events
                .Include("Venue")
                .Include("EventCategory")
                .Where(e => e.OrganizerID == organizerId)
                .OrderByDescending(e => e.StartDate)
                .ToList();

            return View(events);
        }

        // CREATE EVENT (GET)
        [HttpGet]
        public ActionResult CreateEvent()
        {
            if (!SessionHelper.IsOrganizer())
                return RedirectToAction("Login", "Account");

            ViewBag.Categories = db.EventCategories.ToList();
            ViewBag.Venues = db.Venues.ToList();
            return View();
        }

        // CREATE EVENT (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateEvent(Event model, HttpPostedFileBase EventImageFile)
        {
            if (!SessionHelper.IsOrganizer())
                return RedirectToAction("Login", "Account");

            try
            {
                if (ModelState.IsValid)
                {
                    int organizerId = SessionHelper.GetUserId().Value;

                    // Handle Image Upload
                    if (EventImageFile != null && EventImageFile.ContentLength > 0)
                    {
                        string folderPath = Server.MapPath("/Content/Events/");
                        if (!Directory.Exists(folderPath))
                            Directory.CreateDirectory(folderPath);

                        string extension = Path.GetExtension(EventImageFile.FileName);
                        if (extension != null && (extension.ToLower() == ".jpg" || extension.ToLower() == ".jpeg" || extension.ToLower() == ".png"))
                        {
                            string fileName = $"{Guid.NewGuid()}{extension}";
                            string fullPath = Path.Combine(folderPath, fileName);
                            EventImageFile.SaveAs(fullPath);
                            model.EventImage = "/Content/Events/" + fileName;
                        }
                        else
                        {
                            TempData["Error"] = "Invalid image format. Please upload a JPG or PNG file.";
                            return RedirectToAction("CreateEvent");
                        }
                    }
                    else
                    {
                        TempData["Error"] = "Please upload an event image.";
                        return RedirectToAction("CreateEvent");
                    }

                    // Set event details
                    model.OrganizerID = organizerId;
                    model.CreatedAt = DateTime.Now;
                    model.Status = model.Status ?? "Pending";
                    model.AvailableTickets = model.TotalTickets; // initialize available tickets

                    db.Events.Add(model);
                    db.SaveChanges();

                    TempData["Success"] = "Event created successfully!";
                    return RedirectToAction("ManageEvents");
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Failed to create event: " + ex.Message;
            }

            ViewBag.Categories = db.EventCategories.ToList();
            ViewBag.Venues = db.Venues.ToList();
            return View(model);
        }

        // EDIT EVENT (GET)
        [HttpGet]
        public ActionResult EditEvent(int id)
        {
            if (!SessionHelper.IsOrganizer())
                return RedirectToAction("Login", "Account");

            var ev = db.Events.Find(id);
            if (ev == null)
                return HttpNotFound("Event not found.");

            ViewBag.Categories = db.EventCategories.ToList();
            ViewBag.Venues = db.Venues.ToList();
            return View(ev);
        }

        // EDIT EVENT (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditEvent(Event model, HttpPostedFileBase EventImageFile)
        {
            if (!SessionHelper.IsOrganizer())
                return RedirectToAction("Login", "Account");

            try
            {
                var ev = db.Events.Find(model.EventID);
                if (ev == null)
                    return HttpNotFound("Event not found.");

                if (ModelState.IsValid)
                {
                    ev.Title = model.Title;
                    ev.Description = model.Description;
                    ev.CategoryID = model.CategoryID;
                    ev.VenueID = model.VenueID;
                    ev.StartDate = model.StartDate;
                    ev.EndDate = model.EndDate;
                    ev.TicketPrice = model.TicketPrice;
                    ev.TotalTickets = model.TotalTickets;
                    ev.Status = model.Status;
                    ev.UpdatedAt = DateTime.Now;

                    // recalculate available tickets if total increased
                    if (model.TotalTickets > ev.TotalTickets)
                        ev.AvailableTickets += (model.TotalTickets - ev.TotalTickets);

                    if (EventImageFile != null && EventImageFile.ContentLength > 0)
                    {
                        string folderPath = Server.MapPath("/Content/Events/");
                        if (!Directory.Exists(folderPath))
                            Directory.CreateDirectory(folderPath);

                        string extension = Path.GetExtension(EventImageFile.FileName);
                        if (extension != null && (extension.ToLower() == ".jpg" || extension.ToLower() == ".jpeg" || extension.ToLower() == ".png"))
                        {
                            string fileName = $"{Guid.NewGuid()}{extension}";
                            string fullPath = Path.Combine(folderPath, fileName);
                            EventImageFile.SaveAs(fullPath);

                            if (!string.IsNullOrEmpty(ev.EventImage))
                            {
                                string oldPath = Server.MapPath(ev.EventImage);
                                if (System.IO.File.Exists(oldPath))
                                    System.IO.File.Delete(oldPath);
                            }

                            ev.EventImage = "/Content/Events/" + fileName;
                        }
                        else
                        {
                            TempData["Error"] = "Invalid image format. Please upload JPG or PNG only.";
                            return RedirectToAction("EditEvent", new { id = ev.EventID });
                        }
                    }

                    db.Entry(ev).State = EntityState.Modified;
                    db.SaveChanges();

                    TempData["Success"] = "Event updated successfully!";
                    return RedirectToAction("ManageEvents");
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Failed to update event: " + ex.Message;
            }

            ViewBag.Categories = db.EventCategories.ToList();
            ViewBag.Venues = db.Venues.ToList();
            return View(model);
        }

        // DELETE EVENT
        [HttpGet]
        public ActionResult DeleteEvent(int id)
        {
            if (!SessionHelper.IsOrganizer())
                return RedirectToAction("Login", "Account");

            var ev = db.Events.Find(id);
            if (ev == null)
                return HttpNotFound("Event not found.");

            try
            {
                if (!string.IsNullOrEmpty(ev.EventImage))
                {
                    string path = Server.MapPath(ev.EventImage);
                    if (System.IO.File.Exists(path))
                        System.IO.File.Delete(path);
                }

                db.Events.Remove(ev);
                db.SaveChanges();
                TempData["Success"] = "Event deleted successfully!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Failed to delete event: " + ex.Message;
            }

            return RedirectToAction("ManageEvents");
        }

        // MANAGE BOOKINGS
        [HttpGet]
        public ActionResult ManageBookings()
        {
            if (!SessionHelper.IsOrganizer())
                return RedirectToAction("Login", "Account");

            int organizerId = SessionHelper.GetUserId().Value;

            var bookings = db.Bookings
                .Include("User")
                .Include("Event")
                .Include("Payment")
                .Where(b => b.Event.OrganizerID == organizerId)
                .OrderByDescending(b => b.BookingDate)
                .ToList();

            return View(bookings);
        }

        // ORGANIZER PROFILE
        [HttpGet]
        public ActionResult OrganizerProfile()
        {
            if (!SessionHelper.IsOrganizer())
                return RedirectToAction("Login", "Account");

            int userId = SessionHelper.GetUserId().Value;
            var user = db.Users.Find(userId);
            return View(user);
        }

        // ORGANIZER REPORTS
        [HttpGet]
        public ActionResult Reports()
        {
            if (!SessionHelper.IsOrganizer())
                return RedirectToAction("Login", "Account");

            int organizerId = SessionHelper.GetUserId().Value;

            var reportData = db.Bookings
                .Include("Event")
                .Include("User")
                .Where(b => b.Event.OrganizerID == organizerId && b.PaymentStatus == "Completed")
                .Select(b => new
                {
                    EventTitle = b.Event.Title,
                    CustomerName = b.User.FullName,
                    Tickets = b.Quantity,
                    TotalAmount = b.TotalAmount,
                    PaymentDate = b.Payment != null ? b.Payment.PaymentDate : null,
                    PaymentMethod = b.Payment != null ? b.Payment.PaymentGateway : null
                })
                .OrderByDescending(b => b.PaymentDate)
                .ToList();

            ViewBag.TotalSales = reportData.Sum(r => r.TotalAmount);
            ViewBag.TotalTickets = reportData.Sum(r => r.Tickets);
            ViewBag.TotalEvents = reportData.Select(r => r.EventTitle).Distinct().Count();

            return View(reportData);
        }

        // EXPORT TO EXCEL
        [HttpGet]
        public ActionResult ExportExcel()
        {
            try
            {
                int organizerId = SessionHelper.GetUserId().Value;

                var reportData = db.Bookings
                    .Include("Event")
                    .Include("User")
                    .Where(b => b.Event.OrganizerID == organizerId && b.PaymentStatus == "Completed")
                    .Select(b => new
                    {
                        EventTitle = b.Event.Title,
                        CustomerName = b.User.FullName,
                        Tickets = b.Quantity,
                        TotalAmount = b.TotalAmount,
                        PaymentDate = b.Payment != null ? b.Payment.PaymentDate : null,
                        PaymentMethod = b.Payment != null ? b.Payment.PaymentGateway : null
                    })
                    .ToList();

                if (!reportData.Any())
                {
                    TempData["Error"] = "No data available to export.";
                    return RedirectToAction("Reports");
                }

                string folder = Server.MapPath("~/Reports/");
                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                string filePath = Path.Combine(folder, $"OrganizerReport_{DateTime.Now:yyyyMMddHHmmss}.xlsx");

                ExportHelper.ExportToExcel(reportData, filePath);

                return File(filePath, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    Path.GetFileName(filePath));
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error exporting Excel: " + ex.Message;
                return RedirectToAction("Reports");
            }
        }

        // EXPORT TO PDF
        [HttpGet]
        public ActionResult ExportPdf()
        {
            try
            {
                int organizerId = SessionHelper.GetUserId().Value;

                var reportData = db.Bookings
                    .Include("Event")
                    .Include("User")
                    .Where(b => b.Event.OrganizerID == organizerId && b.PaymentStatus == "Completed")
                    .Select(b => new
                    {
                        EventTitle = b.Event.Title,
                        CustomerName = b.User.FullName,
                        Tickets = b.Quantity,
                        TotalAmount = b.TotalAmount,
                        PaymentDate = b.Payment != null ? b.Payment.PaymentDate : null,
                        PaymentMethod = b.Payment != null ? b.Payment.PaymentGateway : null
                    })
                    .ToList();

                if (!reportData.Any())
                {
                    TempData["Error"] = "No data available to export.";
                    return RedirectToAction("Reports");
                }

                string folder = Server.MapPath("~/Reports/");
                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                string filePath = Path.Combine(folder, $"OrganizerReport_{DateTime.Now:yyyyMMddHHmmss}.pdf");

                ExportHelper.ExportToPdf(reportData, filePath, "EventSphere - Organizer Report");

                return File(filePath, "application/pdf", Path.GetFileName(filePath));
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error exporting PDF: " + ex.Message;
                return RedirectToAction("Reports");
            }
        }


    }
}
