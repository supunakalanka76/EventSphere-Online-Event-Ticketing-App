using QRCoder;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Web;

namespace EventSphere.Helpers
{
    public class QRCodeHelper
    {
        /// Generates a QR code for a given ticket with meaningful info.
        /// Unique ticket code (e.g. EVT-20250110-000015-001)
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

            return GenerateQRCode(qrContent);
        }

        /// Generates a generic QR code image (Base64 string for inline display).
        public static string GenerateQRCode(string qrText)
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

        /// Saves QR code image to disk and returns relative path.
        public static string SaveQRCodeImage(string qrText, string savePath)
        {
            if (string.IsNullOrEmpty(qrText))
                throw new ArgumentException("QR text cannot be empty.");

            string fileName = $"QR_{Guid.NewGuid().ToString().Substring(0, 8)}.png";
            string fullPath = Path.Combine(savePath, fileName);

            using (QRCodeGenerator qrGenerator = new QRCodeGenerator())
            using (QRCodeData qrCodeData = qrGenerator.CreateQrCode(qrText, QRCodeGenerator.ECCLevel.Q))
            using (QRCode qrCode = new QRCode(qrCodeData))
            using (Bitmap qrBitmap = qrCode.GetGraphic(20))
            {
                qrBitmap.Save(fullPath, System.Drawing.Imaging.ImageFormat.Png);
            }

            return $"/Content/QRCodes/{fileName}";
        }
    }
}