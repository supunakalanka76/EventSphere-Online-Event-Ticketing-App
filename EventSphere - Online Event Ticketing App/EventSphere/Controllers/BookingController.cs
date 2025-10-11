using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using EventSphere.Models;
using EventSphere.Helpers;

namespace EventSphere.Controllers
{
    public class BookingController : Controller
    {
        private EventSphereDBEntities db = new EventSphereDBEntities();

        // BOOKING PAGE (CONFIRMATION)
        [HttpGet]
        public ActionResult Book(int eventId)
        {
            // Ensure user is logged in
            if (!SessionHelper.IsLoggedIn() || !SessionHelper.IsCustomer())
                return RedirectToAction("Login", "Account");

            var ev = db.Events.Find(eventId);
            if (ev == null)
                return HttpNotFound("Event not found.");

            return View(ev);
        }

        // BOOKING SUBMISSION
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult BookConfirmed(int eventId, int quantity)
        {
            try
            {
                // Ensure user logged in
                if (!SessionHelper.IsLoggedIn() || !SessionHelper.IsCustomer())
                    return RedirectToAction("Login", "Account");

                var ev = db.Events.Find(eventId);
                if (ev == null)
                    return HttpNotFound("Event not found.");

                int userId = SessionHelper.GetUserId().Value;

                // Create a Booking Record
                var booking = new Booking
                {
                    EventID = ev.EventID,
                    UserID = userId,
                    Quantity = quantity,
                    TotalAmount = ev.TicketPrice * quantity,
                    BookingDate = DateTime.Now,
                    CheckInStatus = true,
                };

                db.Bookings.Add(booking);
                db.SaveChanges();

                // Generate unique identifiers
                string bookingCode = booking.BookingID.ToString("D6"); // 6-digit padded
                string datePart = DateTime.Now.ToString("yyyyMMdd");
                string ticketBaseCode = $"EVT-{datePart}-{bookingCode}";

                // Generate tickets
                for (int i = 1; i <= quantity; i++)
                {
                    string ticketCode = $"{ticketBaseCode}-{i:D3}";

                    // Generate QR code image path using helper
                    string qrPath = QRCodeHelper.GenerateQRCode(ticketCode, "Tickets");

                    var ticket = new Ticket
                    {
                        BookingID = booking.BookingID,
                        TicketNumber = ticketCode,
                        QRCodeImage = qrPath,
                        IssueDate = DateTime.Now
                    };

                    db.Tickets.Add(ticket);
                }

                // Create Transaction
                string txnCode = $"TXN{DateTime.Now:yyyyMMddHHmmss}{Guid.NewGuid().ToString("N").Substring(0, 6).ToUpper()}";
                string invoiceCode = $"INV-{DateTime.Now:yyyy}-{booking.BookingID:D6}";

                var transaction = new Payment
                {
                    BookingID = booking.BookingID,
                    TransactionID = txnCode,
                    InvoiceNumber = invoiceCode,
                    PaymentDate = DateTime.Now,
                    Amount = booking.TotalAmount,
                    Status = "Completed",
                    PaymentGateway = "Online"
                };

                db.Payments.Add(transaction);
                db.SaveChanges();

                // Redirect to confirmation
                TempData["Success"] = "Booking successful!";
                return RedirectToAction("Confirmation", new { bookingId = booking.BookingID });
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Booking failed: " + ex.Message;
                return RedirectToAction("Index", "Event");
            }
        }

        // BOOKING CONFIRMATION PAGE
        [HttpGet]
        public ActionResult Confirmation(int bookingId)
        {
            try
            {
                var booking = db.Bookings
                    .Include("Event")
                    .Include("Tickets")
                    .Include("Payment")
                    .FirstOrDefault(b => b.BookingID == bookingId);

                if (booking == null)
                    return HttpNotFound("Booking not found.");

                return View(booking);
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Failed to load confirmation: " + ex.Message;
                return RedirectToAction("Index", "Event");
            }
        }

        // USER BOOKINGS LIST
        [HttpGet]
        public ActionResult MyBookings()
        {
            if (!SessionHelper.IsLoggedIn() || !SessionHelper.IsCustomer())
                return RedirectToAction("Login", "Account");

            int userId = SessionHelper.GetUserId().Value;

            var bookings = db.Bookings
                .Include("Event")
                .Include("Tickets")
                .Where(b => b.UserID == userId)
                .OrderByDescending(b => b.BookingDate)
                .ToList();

            return View(bookings);
        }
    }
}