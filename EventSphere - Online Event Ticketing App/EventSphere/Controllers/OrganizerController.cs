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

            ViewBag.TotalEvents = db.Events.Count(e => e.OrganizerID == organizerId);
            ViewBag.TotalBookings = db.Bookings.Count(b => b.Event.OrganizerID == organizerId);
            ViewBag.TotalRevenue = db.Payments
                .Where(p => p.Booking.Event.OrganizerID == organizerId)
                .Sum(p => (decimal?)p.Amount) ?? 0;

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
                        string folderPath = Server.MapPath("~/Content/Events/");
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
                    // Update main fields
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

                    // Handle new image upload
                    if (EventImageFile != null && EventImageFile.ContentLength > 0)
                    {
                        string folderPath = Server.MapPath("~/Content/Events/");
                        if (!Directory.Exists(folderPath))
                            Directory.CreateDirectory(folderPath);

                        string extension = Path.GetExtension(EventImageFile.FileName);
                        if (extension != null && (extension.ToLower() == ".jpg" || extension.ToLower() == ".jpeg" || extension.ToLower() == ".png"))
                        {
                            string fileName = $"{Guid.NewGuid()}{extension}";
                            string fullPath = Path.Combine(folderPath, fileName);
                            EventImageFile.SaveAs(fullPath);

                            // Delete old image if exists
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
                // Delete image file from disk
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
    }
}
