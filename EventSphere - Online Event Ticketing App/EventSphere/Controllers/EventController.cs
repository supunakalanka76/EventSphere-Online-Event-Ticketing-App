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
    public class EventController : BaseController
    {
        private readonly EventSphereDBEntities db = new EventSphereDBEntities();

        // EVENT LISTING PAGE
        public ActionResult Index(int? categoryId, string search)
        {
            try
            {
                ViewBag.Categories = db.EventCategories.ToList();

                var events = db.Events
                    .Include(e => e.Venue)
                    .Include(e => e.EventCategory)
                    .Include(e => e.User)
                    .AsQueryable();

                if (categoryId.HasValue && categoryId.Value > 0)
                {
                    events = events.Where(e => e.CategoryID == categoryId.Value);
                    ViewBag.SelectedCategory = categoryId;
                }

                if (!string.IsNullOrEmpty(search))
                {
                    events = events.Where(e =>
                        e.Title.Contains(search) ||
                        e.Description.Contains(search) ||
                        e.Venue.VenueName.Contains(search)
                    );
                    ViewBag.SearchTerm = search;
                }

                // Show only upcoming events to customers
                events = events.Where(e => e.EndDate >= DateTime.Now);

                var eventList = events.OrderBy(e => e.StartDate).ToList();
                return View(eventList);
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Failed to load events: " + ex.Message;
                return View(new List<Event>());
            }
        }

        // EVENT DETAILS PAGE
        public ActionResult Details(int id)
        {
            try
            {
                var ev = db.Events
                    .Include(e => e.Venue)
                    .Include(e => e.EventCategory)
                    .Include(e => e.User)
                    .FirstOrDefault(e => e.EventID == id);

                if (ev == null)
                    return HttpNotFound("Event not found.");

                if (ev.User == null || ev.User.Role != "Organizer")
                {
                    ViewBag.Warning = "Organizer information is not available.";
                }

                return View(ev);
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Unable to load event details: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        // ORGANIZER: EDIT EVENT (GET)
        [HttpGet]
        public ActionResult Edit(int id)
        {
            var ev = db.Events.Find(id);
            if (ev == null)
                return HttpNotFound();

            ViewBag.Categories = db.EventCategories.ToList();
            ViewBag.Venues = db.Venues.ToList();

            return View(ev);
        }

        // ORGANIZER: EDIT EVENT (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(Event model, HttpPostedFileBase EventImageFile)
        {
            try
            {
                var ev = db.Events.Find(model.EventID);
                if (ev == null)
                    return HttpNotFound("Event not found.");

                // Update fields
                ev.Title = model.Title;
                ev.Description = model.Description;
                ev.StartDate = model.StartDate;
                ev.EndDate = model.EndDate;
                ev.TicketPrice = model.TicketPrice;
                ev.TotalTickets = model.TotalTickets;
                ev.AvailableTickets = model.AvailableTickets;
                ev.CategoryID = model.CategoryID;
                ev.VenueID = model.VenueID;

                // Handle new image safely
                if (EventImageFile != null && EventImageFile.ContentLength > 0)
                {
                    string folderPath = Server.MapPath("~/Content/EventImages/");
                    if (!Directory.Exists(folderPath))
                        Directory.CreateDirectory(folderPath);

                    string fileName = $"{Guid.NewGuid()}{Path.GetExtension(EventImageFile.FileName)}";
                    string filePath = Path.Combine(folderPath, fileName);

                    EventImageFile.SaveAs(filePath);

                    ev.EventImage = "/Content/EventImages/" + fileName;
                }

                db.SaveChanges();
                TempData["Success"] = "Event updated successfully!";
                return RedirectToAction("ManageEvents", "Organizer");
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error updating event: " + ex.Message;
                return View(model);
            }
        }

        // OPTIONAL: CREATE EVENT (Organizer)
        [HttpGet]
        public ActionResult Create()
        {
            ViewBag.Categories = db.EventCategories.ToList();
            ViewBag.Venues = db.Venues.ToList();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(Event model, HttpPostedFileBase EventImageFile)
        {
            try
            {
                if (SessionHelper.IsOrganizer())
                {
                    model.OrganizerID = SessionHelper.GetUserId().Value;
                }

                model.Status = "Active";
                model.CreatedAt = DateTime.Now;
                model.AvailableTickets = model.TotalTickets;

                if (EventImageFile != null && EventImageFile.ContentLength > 0)
                {
                    string folderPath = Server.MapPath("~/Content/EventImages/");
                    if (!Directory.Exists(folderPath))
                        Directory.CreateDirectory(folderPath);

                    string fileName = $"{Guid.NewGuid()}{Path.GetExtension(EventImageFile.FileName)}";
                    string filePath = Path.Combine(folderPath, fileName);
                    EventImageFile.SaveAs(filePath);

                    model.EventImage = "/Content/EventImages/" + fileName;
                }
                else
                {
                    model.EventImage = "/Content/EventImages/default-event.png";
                }

                db.Events.Add(model);
                db.SaveChanges();

                TempData["Success"] = "Event created successfully!";
                return RedirectToAction("ManageEvents", "Organizer");
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error creating event: " + ex.Message;
                ViewBag.Categories = db.EventCategories.ToList();
                ViewBag.Venues = db.Venues.ToList();
                return View(model);
            }
        }
    }
}
