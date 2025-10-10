using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;

namespace EventSphere.Helpers
{
    public class CodeGeneratorHelper
    {
        /// Generates a formatted ticket code.
        /// Example: EVT-20250110-000015-001
        public static string GenerateTicketCode(string eventPrefix, int bookingId, int ticketNumber, DateTime? eventDate = null)
        {
            DateTime date = eventDate ?? DateTime.Now;
            string datePart = date.ToString("yyyyMMdd");
            string bookingPart = bookingId.ToString("D6");    // zero-padded to 6 digits
            string ticketPart = ticketNumber.ToString("D3");  // zero-padded to 3 digits

            return $"{eventPrefix}-{datePart}-{bookingPart}-{ticketPart}";
        }

        /// Generates a unique transaction ID.
        /// Example: TXN20250110143025ABCD1234
        public static string GenerateTransactionId()
        {
            string prefix = "TXN";
            string datePart = DateTime.Now.ToString("yyyyMMddHHmmss");
            string randomPart = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();

            return $"{prefix}{datePart}{randomPart}";
        }

        /// Generates a formatted invoice number.
        /// Example: INV-2025-000015
        public static string GenerateInvoiceNumber(int bookingId)
        {
            string prefix = "INV";
            string year = DateTime.Now.Year.ToString();
            string bookingPart = bookingId.ToString("D6"); // zero-padded to 6 digits

            return $"{prefix}-{year}-{bookingPart}";
        }

        /// Generates a simple alphanumeric code for short references or discount codes.
        public static string GenerateShortCode(string prefix, int length = 6)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var random = new Random();
            var code = new StringBuilder();

            for (int i = 0; i < length; i++)
                code.Append(chars[random.Next(chars.Length)]);

            return $"{prefix}-{code}";
        }
    }
}