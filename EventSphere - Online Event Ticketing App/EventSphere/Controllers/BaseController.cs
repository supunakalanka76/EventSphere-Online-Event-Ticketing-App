using EventSphere.Helpers;
using System;
using System.Web.Mvc;

namespace EventSphere.Controllers
{
    public class BaseController : Controller
    {
        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            try
            {
                // 🔹 Default layout (public)
                string layoutPath = "~/Views/Shared/_Layout.cshtml";

                if (SessionHelper.IsLoggedIn())
                {
                    string role = (SessionHelper.GetUserRole() ?? "").ToLower();

                    switch (role)
                    {
                        case "admin":
                            layoutPath = "~/Views/Shared/_LayoutAdmin.cshtml";
                            break;

                        case "organizer":
                            layoutPath = "~/Views/Shared/_LayoutOrganizer.cshtml";
                            break;

                        case "customer":
                            layoutPath = "~/Views/Shared/_LayoutCustomer.cshtml";
                            break;
                    }

                    // Optional helper info for navbar/user panel
                    ViewBag.CurrentUserName = SessionHelper.GetUserName();
                    ViewBag.CurrentUserRole = role;
                }

                ViewBag.LayoutPath = layoutPath;
            }
            catch (Exception)
            {
                // Safety fallback — never crash the pipeline
                ViewBag.LayoutPath = "~/Views/Shared/_Layout.cshtml";
            }

            base.OnActionExecuting(filterContext);
        }
    }
}
