using EventSphere.Helpers;
using EventSphere.Models;
using iTextSharp.text;
using iTextSharp.text.pdf;
using System;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Web.Mvc;

namespace EventSphere.Controllers
{
    public class BookingController : BaseController
    {
        private EventSphereDBEntities db = new EventSphereDBEntities();

        // BOOK EVENT (initial booking, unpaid)
        [HttpGet]
        public ActionResult Book(int eventId)
        {
            if (!SessionHelper.IsLoggedIn() || !SessionHelper.IsCustomer())
                return RedirectToAction("Login", "Account");

            var ev = db.Events.Find(eventId);
            if (ev == null)
                return HttpNotFound("Event not found.");

            if (ev.EndDate < DateTime.Now)
            {
                TempData["Error"] = "This event has already ended. Booking is not allowed.";
                return RedirectToAction("Index", "Event");
            }

            if (ev.AvailableTickets <= 0)
            {
                TempData["Error"] = "No tickets available for this event.";
                return RedirectToAction("Index", "Event");
            }

            return View(ev);
        }

        // CONFIRM BOOKING (creates unpaid booking and tickets)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult BookConfirmed(int eventId, int quantity)
        {
            try
            {
                if (!SessionHelper.IsLoggedIn() || !SessionHelper.IsCustomer())
                    return RedirectToAction("Login", "Account");

                var ev = db.Events.Include("Venue").FirstOrDefault(e => e.EventID == eventId);
                if (ev == null)
                    return HttpNotFound("Event not found.");

                if (ev.EndDate < DateTime.Now)
                {
                    TempData["Error"] = "This event has already ended. Booking is not allowed.";
                    return RedirectToAction("Index", "Event");
                }

                if (ev.AvailableTickets < quantity)
                {
                    TempData["Error"] = $"Only {ev.AvailableTickets} tickets are remaining.";
                    return RedirectToAction("Book", new { eventId });
                }

                int userId = SessionHelper.GetUserId().Value;

                var booking = new Booking
                {
                    EventID = ev.EventID,
                    UserID = userId,
                    Quantity = quantity,
                    TotalAmount = ev.TicketPrice * quantity,
                    FinalAmount = ev.TicketPrice * quantity,
                    BookingDate = DateTime.Now,
                    PaymentStatus = "Pending",
                    CheckInStatus = false
                };

                db.Bookings.Add(booking);
                db.SaveChanges();

                // Generate ticket placeholders (QR after payment)
                string bookingCode = booking.BookingID.ToString("D6");
                string datePart = DateTime.Now.ToString("yyyyMMdd");
                string ticketBaseCode = $"EVT-{datePart}-{bookingCode}";

                for (int i = 1; i <= quantity; i++)
                {
                    db.Tickets.Add(new Ticket
                    {
                        BookingID = booking.BookingID,
                        TicketNumber = $"{ticketBaseCode}-{i:D3}",
                        IssueDate = DateTime.Now
                    });
                }
                db.SaveChanges();

                return RedirectToAction("Payment", new { bookingId = booking.BookingID });
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Booking failed: " + ex.Message;
                return RedirectToAction("Book", new { eventId });
            }
        }

        // PAYMENT PAGE (manual input)
        [HttpGet]
        public ActionResult Payment(int bookingId)
        {
            var booking = db.Bookings.Include("Event").FirstOrDefault(b => b.BookingID == bookingId);
            if (booking == null)
                return HttpNotFound("Booking not found.");

            if (booking.PaymentStatus == "Completed")
                return RedirectToAction("Confirmation", new { bookingId });

            ViewBag.LoyaltyPoints = db.Users.Find(booking.UserID)?.LoyaltyPoints ?? 0;
            ViewBag.Promotions = db.Promotions.Where(p => p.IsActive == true && p.EndDate >= DateTime.Now).ToList();
            return View(booking);
        }

        // CONFIRM PAYMENT (updates revenue, ticket availability)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult PaymentConfirmed(int bookingId, string promoCode, bool? usePoints, string paymentMethod)
        {
            try
            {
                var booking = db.Bookings
                    .Include("Event")
                    .Include("Tickets")
                    .FirstOrDefault(b => b.BookingID == bookingId);

                if (booking == null)
                    return HttpNotFound("Booking not found.");

                var user = db.Users.Find(booking.UserID);
                if (user == null)
                    return HttpNotFound("User not found.");

                var ev = booking.Event;
                if (ev == null)
                    return HttpNotFound("Event not found.");

                // Check ticket availability again (avoid overselling)
                if (ev.AvailableTickets < booking.Quantity)
                {
                    TempData["Error"] = $"Not enough tickets available. Remaining: {ev.AvailableTickets}";
                    return RedirectToAction("Payment", new { bookingId });
                }

                decimal total = booking.TotalAmount;
                decimal discount = 0;
                decimal pointsUsed = 0;

                // Promotion
                if (!string.IsNullOrEmpty(promoCode))
                {
                    var promo = db.Promotions.FirstOrDefault(p =>
                        p.Code == promoCode &&
                        p.IsActive == true &&
                        p.StartDate <= DateTime.Now &&
                        p.EndDate >= DateTime.Now);

                    if (promo != null)
                    {
                        discount = total * ((promo.DiscountPercentage ?? 0m) / 100m);
                        booking.PromotionID = promo.PromotionID;
                        booking.DiscountApplied = discount;
                    }
                    else
                    {
                        TempData["Error"] = "Invalid or expired promotion code.";
                        return RedirectToAction("Payment", new { bookingId });
                    }
                }

                // Loyalty Points
                if (usePoints == true && (user.LoyaltyPoints ?? 0) > 0)
                {
                    pointsUsed = Math.Min(user.LoyaltyPoints ?? 0m, total - discount);
                    user.LoyaltyPoints -= (int)pointsUsed;

                    db.LoyaltyTransactions.Add(new LoyaltyTransaction
                    {
                        UserID = user.UserID,
                        BookingID = booking.BookingID,
                        Points = (int)-pointsUsed,
                        Type = "Redeem",
                        Description = $"Redeemed {pointsUsed} points for booking {booking.BookingID}",
                        TransactionDate = DateTime.Now
                    });
                }

                // Final Amount
                decimal finalAmount = total - discount - pointsUsed;
                booking.FinalAmount = finalAmount;
                booking.PaymentStatus = "Completed";
                booking.PaymentMethod = paymentMethod;

                // Payment record
                db.Payments.Add(new Payment
                {
                    BookingID = booking.BookingID,
                    Amount = finalAmount,
                    PaymentDate = DateTime.Now,
                    Status = "Completed",
                    PaymentGateway = paymentMethod,
                    ReferenceNo = $"REF-{DateTime.Now:yyyyMMddHHmmss}",
                    InvoiceNumber = $"INV-{DateTime.Now:yyyy}-{booking.BookingID:D6}"
                });

                // Decrease available tickets (important!)
                ev.AvailableTickets -= booking.Quantity;
                if (ev.AvailableTickets < 0)
                    ev.AvailableTickets = 0; // safety clamp

                db.Entry(ev).State = EntityState.Modified;

                // Generate QR codes
                foreach (var ticket in booking.Tickets)
                {
                    ticket.QRCodeImage = QRCodeHelper.GenerateTicketQRCode(
                        ticket.TicketNumber,
                        booking.BookingID,
                        ev.Title,
                        user.FullName,
                        ev.StartDate
                    );
                }

                // Earn points (5%)
                int earnedPoints = (int)(finalAmount * 0.05m);
                if (earnedPoints > 0)
                {
                    user.LoyaltyPoints = (user.LoyaltyPoints ?? 0) + earnedPoints;

                    db.LoyaltyTransactions.Add(new LoyaltyTransaction
                    {
                        UserID = user.UserID,
                        BookingID = booking.BookingID,
                        Points = earnedPoints,
                        Type = "Earn",
                        Description = $"Earned {earnedPoints} points from booking {booking.BookingID}",
                        TransactionDate = DateTime.Now
                    });
                }

                db.SaveChanges();

                TempData["Success"] = "Payment successful! Tickets updated.";
                return RedirectToAction("Confirmation", new { bookingId = booking.BookingID });
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Payment failed: " + ex.Message;
                return RedirectToAction("Payment", new { bookingId });
            }
        }

        // CONFIRMATION PAGE
        [HttpGet]
        public ActionResult Confirmation(int bookingId)
        {
            var booking = db.Bookings
                .Include("Event.Venue")
                .Include("Tickets")
                .Include("Payment")
                .FirstOrDefault(b => b.BookingID == bookingId);

            if (booking == null)
                return HttpNotFound("Booking not found.");

            return View(booking);
        }

        // USER BOOKINGS
        [HttpGet]
        public ActionResult MyBookings()
        {
            if (!SessionHelper.IsLoggedIn() || !SessionHelper.IsCustomer())
                return RedirectToAction("Login", "Account");

            int userId = SessionHelper.GetUserId().Value;

            var bookings = db.Bookings
                .Include("Event")
                .Include("Tickets")
                .Include("Payment")
                .Where(b => b.UserID == userId)
                .OrderByDescending(b => b.BookingDate)
                .ToList();

            ViewBag.TotalSpent = bookings
                .Where(b => b.PaymentStatus == "Completed")
                .Sum(b => (decimal?)b.TotalAmount) ?? 0;

            return View(bookings);
        }
    }
}
