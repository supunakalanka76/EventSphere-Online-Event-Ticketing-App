using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Data.Entity;
using EventSphere.Models;

namespace EventSphere.Controllers
{
    public class EventController : Controller
    {
        private EventSphereDBEntities db = new EventSphereDBEntities();

        // EVENT LISTING PAGE
        public ActionResult Index(int? categoryId, string search)
        {
            try
            {
                // Load all categories for dropdown filter
                ViewBag.Categories = db.EventCategories.ToList();

                // Base query including related data (Venue, Category, User/Organizer)
                var events = db.Events
                    .Include(e => e.Venue)
                    .Include(e => e.EventCategory)
                    .Include(e => e.User) // Include Organizer (User)
                    .AsQueryable();

                // Apply category filter
                if (categoryId.HasValue && categoryId.Value > 0)
                {
                    events = events.Where(e => e.CategoryID == categoryId.Value);
                    ViewBag.SelectedCategory = categoryId;
                }

                // Apply search filter (by name, description, or venue)
                if (!string.IsNullOrEmpty(search))
                {
                    events = events.Where(e =>
                        e.Title.Contains(search) ||
                        e.Description.Contains(search) ||
                        e.Venue.VenueName.Contains(search)
                    );
                    ViewBag.SearchTerm = search;
                }

                // Order by date
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
                    .Include(e => e.User) // Include Organizer
                    .FirstOrDefault(e => e.EventID == id);

                if (ev == null)
                    return HttpNotFound("Event not found.");

                // Verify the related user has Role = "Organizer"
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
    }
}
