using QRCoder;
using System;
using System.Drawing;
using System.IO;
using System.Web;

namespace EventSphere.Helpers
{
    public class QRCodeHelper
    {
        /// Generates a detailed ticket QR code and saves it to disk.
        /// Returns a relative URL (e.g., /Content/Tickets/QR_xxxx.png).
        public static string GenerateTicketQRCode(
            string ticketCode,
            int bookingId,
            string eventName,
            string customerName,
            DateTime eventDate)
        {
            string qrContent =
                $"Ticket Code: {ticketCode}\n" +
                $"Booking ID: {bookingId}\n" +
                $"Event: {eventName}\n" +
                $"Date: {eventDate:yyyy-MM-dd}\n" +
                $"Customer: {customerName}";

            return GenerateQRCode(qrContent, "Tickets");
        }

        /// Generates and saves a QR image in ~/Content/{folderName}/,
        /// returns relative URL (for web display).
        public static string GenerateQRCode(string qrText, string folderName)
        {
            if (string.IsNullOrEmpty(qrText))
                throw new ArgumentException("QR text cannot be empty.");

            // Folder path resolution
            string folderPath = HttpContext.Current.Server.MapPath($"~/Content/{folderName}/");
            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);

            // Unique file name
            string fileName = $"QR_{Guid.NewGuid().ToString().Substring(0, 8)}.png";
            string fullPath = Path.Combine(folderPath, fileName);

            // Generate and save
            using (QRCodeGenerator qrGenerator = new QRCodeGenerator())
            using (QRCodeData qrCodeData = qrGenerator.CreateQrCode(qrText, QRCodeGenerator.ECCLevel.Q))
            using (QRCode qrCode = new QRCode(qrCodeData))
            using (Bitmap qrBitmap = qrCode.GetGraphic(20))
            {
                qrBitmap.Save(fullPath, System.Drawing.Imaging.ImageFormat.Png);
            }

            return $"/Content/{folderName}/{fileName}";
        }
        /// Generates a Base64-encoded QR image (for inline PDF embedding).
        /// Returns a "data:image/png;base64,..." string.
        public static string GenerateQRCodeBase64(string qrText)
        {
            if (string.IsNullOrEmpty(qrText))
                throw new ArgumentException("QR text cannot be empty.");

            using (QRCodeGenerator qrGenerator = new QRCodeGenerator())
            using (QRCodeData qrCodeData = qrGenerator.CreateQrCode(qrText, QRCodeGenerator.ECCLevel.Q))
            using (QRCode qrCode = new QRCode(qrCodeData))
            using (Bitmap qrBitmap = qrCode.GetGraphic(20))
            using (MemoryStream ms = new MemoryStream())
            {
                qrBitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                string base64Image = Convert.ToBase64String(ms.ToArray());
                return $"data:image/png;base64,{base64Image}";
            }
        }
    }
}
