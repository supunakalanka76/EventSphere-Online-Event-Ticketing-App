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
    public class OrganizerController : Controller
    {
        private EventSphereDBEntities db = new EventSphereDBEntities();

        // DASHBOARD
        [HttpGet]
        public ActionResult Dashboard()
        {
            if (!SessionHelper.IsOrganizer())
                return RedirectToAction("Login", "Account");

            int organizerId = SessionHelper.GetUserId().Value;

            var totalEvents = db.Events.Count(e => e.OrganizerID == organizerId);
            var totalBookings = db.Bookings.Count(b => b.Event.OrganizerID == organizerId);
            var totalRevenue = db.Payments
                .Where(p => p.Booking.Event.OrganizerID == organizerId)
                .Sum(p => (decimal?)p.Amount) ?? 0;

            ViewBag.TotalEvents = totalEvents;
            ViewBag.TotalBookings = totalBookings;
            ViewBag.TotalRevenue = totalRevenue;

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
        public ActionResult CreateEvent(Event model, HttpPostedFileBase ImageFile)
        {
            try
            {
                if (!SessionHelper.IsOrganizer())
                    return RedirectToAction("Login", "Account");

                if (ModelState.IsValid)
                {
                    int organizerId = SessionHelper.GetUserId().Value;

                    // Handle image upload
                    if (ImageFile != null && ImageFile.ContentLength > 0)
                    {
                        string folderPath = Server.MapPath("~/Content/Events/");
                        if (!Directory.Exists(folderPath))
                            Directory.CreateDirectory(folderPath);

                        string fileName = DateTime.Now.ToString("yyyyMMddHHmmss") + "_" + Path.GetFileName(ImageFile.FileName);
                        string fullPath = Path.Combine(folderPath, fileName);
                        ImageFile.SaveAs(fullPath);
                        model.EventImage = "/Content/Events/" + fileName;
                    }

                    model.OrganizerID = organizerId;
                    model.CreatedAt = DateTime.Now;
                    model.Status = model.Status ?? "Pending";

                    db.Events.Add(model);
                    db.SaveChanges();

                    TempData["Success"] = "Event created successfully!";
                    return RedirectToAction("ManageEvents");
                }
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Failed to create event: " + ex.Message;
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
        public ActionResult EditEvent(Event model, HttpPostedFileBase ImageFile)
        {
            try
            {
                if (!SessionHelper.IsOrganizer())
                    return RedirectToAction("Login", "Account");

                var ev = db.Events.Find(model.EventID);
                if (ev == null)
                    return HttpNotFound("Event not found.");

                if (ModelState.IsValid)
                {
                    // Update basic fields
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

                    // Image upload
                    if (ImageFile != null && ImageFile.ContentLength > 0)
                    {
                        string folderPath = Server.MapPath("~/Content/Events/");
                        if (!Directory.Exists(folderPath))
                            Directory.CreateDirectory(folderPath);

                        string fileName = DateTime.Now.ToString("yyyyMMddHHmmss") + "_" + Path.GetFileName(ImageFile.FileName);
                        string fullPath = Path.Combine(folderPath, fileName);
                        ImageFile.SaveAs(fullPath);
                        ev.EventImage = "/Content/Events/" + fileName;
                    }

                    db.Entry(ev).State = EntityState.Modified;
                    db.SaveChanges();

                    TempData["Success"] = "Event updated successfully!";
                    return RedirectToAction("ManageEvents");
                }
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Failed to update event: " + ex.Message;
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

        // MANAGE BOOKINGS (ORGANIZER)
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
    }
}
