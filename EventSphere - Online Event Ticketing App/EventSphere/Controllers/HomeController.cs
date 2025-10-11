using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using EventSphere.Models;

namespace EventSphere.Controllers
{
    public class HomeController : Controller
    {
        private EventSphereDBEntities db = new EventSphereDBEntities();

        // GET: Home
        public ActionResult Index()
        {
            // Get next 3 upcoming events
            var upcoming = db.Events
                .Where(e => e.StartDate >= DateTime.Now)
                .OrderBy(e => e.StartDate)
                .Take(3)
                .ToList();

            ViewBag.UpcomingEvents = upcoming;

            return View();
        }

        public ActionResult About()
        {
            return View();
        }

        public ActionResult Contact()
        {
            return View();
        }
    }
}