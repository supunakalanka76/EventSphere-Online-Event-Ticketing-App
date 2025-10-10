using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using BCrypt.Net;

namespace EventSphere.Helpers
{
    public class PasswordHelper
    {
        /// Hashes a plain text password using BCrypt algorithm.
        public static string HashPassword(string password)
        {
            if (string.IsNullOrEmpty(password))
                throw new ArgumentException("Password cannot be empty.");

            // BCrypt automatically handles salt generation
            return BCrypt.Net.BCrypt.HashPassword(password);
        }

        /// Verifies a plain text password against a hashed password.
        public static bool VerifyPassword(string plainPassword, string hashedPassword)
        {
            if (string.IsNullOrEmpty(plainPassword) || string.IsNullOrEmpty(hashedPassword))
                return false;

            return BCrypt.Net.BCrypt.Verify(plainPassword, hashedPassword);
        }
    }
}