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
                    var ticket = new Ticket
                    {
                        BookingID = booking.BookingID,
                        TicketNumber = $"{ticketBaseCode}-{i:D3}",
                        IssueDate = DateTime.Now
                    };
                    db.Tickets.Add(ticket);
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

        // CONFIRM PAYMENT
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
                var payment = new Payment
                {
                    BookingID = booking.BookingID,
                    Amount = finalAmount,
                    PaymentDate = DateTime.Now,
                    Status = "Completed",
                    PaymentGateway = paymentMethod,
                    ReferenceNo = $"REF-{DateTime.Now:yyyyMMddHHmmss}",
                    InvoiceNumber = $"INV-{DateTime.Now:yyyy}-{booking.BookingID:D6}"
                };
                db.Payments.Add(payment);

                // Generate QRs
                var customerName = user.FullName;
                foreach (var ticket in booking.Tickets.ToList())
                {
                    string qrPath = QRCodeHelper.GenerateTicketQRCode(
                        ticket.TicketNumber,
                        booking.BookingID,
                        booking.Event.Title,
                        customerName,
                        booking.Event.StartDate
                    );
                    ticket.QRCodeImage = qrPath;
                }

                // Earn Points
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

                TempData["Success"] = "Payment successful!";
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

        // DOWNLOAD INVOICE (PDF)
        public ActionResult DownloadInvoice(int bookingId)
        {
            var booking = db.Bookings
                .Include("Event.Venue")
                .Include("Payment")
                .Include("User")
                .Include("Tickets")
                .FirstOrDefault(b => b.BookingID == bookingId);

            if (booking == null)
                return HttpNotFound("Booking not found.");

            using (var ms = new MemoryStream())
            {
                Document doc = new Document(PageSize.A4, 40, 40, 50, 40);
                PdfWriter.GetInstance(doc, ms);
                doc.Open();

                var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 18, BaseColor.BLACK);
                var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 13, BaseColor.BLACK);
                var textFont = FontFactory.GetFont(FontFactory.HELVETICA, 11, BaseColor.BLACK);

                doc.Add(new Paragraph("EventSphere Booking Invoice", titleFont));
                doc.Add(new Paragraph("Generated on: " + DateTime.Now.ToString("MMMM dd, yyyy hh:mm tt"), textFont));
                doc.Add(new Paragraph("--------------------------------------------------------------------"));

                doc.Add(new Paragraph("\nBooking Details", headerFont));
                doc.Add(new Paragraph($"Invoice #: {booking.Payment?.InvoiceNumber}", textFont));
                doc.Add(new Paragraph($"Customer: {booking.User?.FullName}", textFont));
                doc.Add(new Paragraph($"Event: {booking.Event.Title}", textFont));
                doc.Add(new Paragraph($"Venue: {booking.Event.Venue?.VenueName}, {booking.Event.Venue?.City}", textFont));
                doc.Add(new Paragraph($"Date: {booking.Event.StartDate:MMMM dd, yyyy}", textFont));
                doc.Add(new Paragraph($"Tickets: {booking.Quantity}", textFont));
                doc.Add(new Paragraph($"Booking Date: {booking.BookingDate:MMMM dd, yyyy hh:mm tt}", textFont));

                doc.Add(new Paragraph("\nPayment Information", headerFont));
                doc.Add(new Paragraph($"Method: {booking.Payment?.PaymentGateway}", textFont));
                doc.Add(new Paragraph($"Reference: {booking.Payment?.ReferenceNo}", textFont));
                doc.Add(new Paragraph($"Status: {booking.Payment?.Status}", textFont));
                doc.Add(new Paragraph($"Payment Date: {booking.Payment?.PaymentDate:MMMM dd, yyyy hh:mm tt}", textFont));
                doc.Add(new Paragraph($"\nFinal Amount Paid: LKR {booking.FinalAmount:N2}", headerFont));

                doc.Add(new Paragraph("\n--------------------------------------------------------------------\n"));
                doc.Add(new Paragraph("Tickets & QR Codes", headerFont));

                if (booking.Tickets != null && booking.Tickets.Any())
                {
                    PdfPTable table = new PdfPTable(2);
                    table.WidthPercentage = 100;
                    table.SetWidths(new float[] { 40f, 60f });

                    foreach (var ticket in booking.Tickets)
                    {
                        iTextSharp.text.Image qrImage = null;

                        // If Base64 QR exists
                        if (!string.IsNullOrEmpty(ticket.QRCodeImage) && ticket.QRCodeImage.StartsWith("data:image"))
                        {
                            var base64Data = ticket.QRCodeImage.Split(',')[1];
                            byte[] qrBytes = Convert.FromBase64String(base64Data);
                            qrImage = iTextSharp.text.Image.GetInstance(qrBytes);
                        }
                        // If file path exists
                        else if (!string.IsNullOrEmpty(ticket.QRCodeImage))
                        {
                            string qrFullPath = HttpContext.Server.MapPath(ticket.QRCodeImage);
                            if (System.IO.File.Exists(qrFullPath))
                                qrImage = iTextSharp.text.Image.GetInstance(qrFullPath);
                            else
                            {
                                // Generate Base64 QR dynamically
                                string qrBase64 = QRCodeHelper.GenerateQRCodeBase64(ticket.TicketNumber);
                                var bytes = Convert.FromBase64String(qrBase64.Split(',')[1]);
                                qrImage = iTextSharp.text.Image.GetInstance(bytes);
                            }
                        }

                        if (qrImage != null)
                        {
                            qrImage.ScaleAbsolute(100f, 100f);
                            PdfPCell qrCell = new PdfPCell(qrImage)
                            {
                                Border = PdfPCell.NO_BORDER,
                                HorizontalAlignment = Element.ALIGN_CENTER
                            };
                            table.AddCell(qrCell);
                        }

                        PdfPCell infoCell = new PdfPCell();
                        infoCell.Border = PdfPCell.NO_BORDER;
                        infoCell.AddElement(new Paragraph($"Ticket #: {ticket.TicketNumber}", textFont));
                        infoCell.AddElement(new Paragraph($"Issued: {ticket.IssueDate:MMMM dd, yyyy}", textFont));
                        table.AddCell(infoCell);
                    }

                    doc.Add(table);
                }
                else
                {
                    doc.Add(new Paragraph("No tickets available.", textFont));
                }

                doc.Add(new Paragraph("\nThank you for booking with EventSphere!", headerFont));
                doc.Add(new Paragraph("Please present your QR code at event entry.", textFont));

                doc.Close();
                return File(ms.ToArray(), "application/pdf", $"Invoice_{booking.BookingID}.pdf");
            }
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

            return View(bookings);
        }
    }
}
