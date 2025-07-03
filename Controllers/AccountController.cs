﻿using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using Mimsv2.Services;
using Mimsv2.Models;

namespace Mimsv2.Controllers
{
    public class AccountController : Controller
    {
        private readonly NpgsqlConnection _conn;
        private readonly EmailService _emailService;
       

        public AccountController(NpgsqlConnection conn, EmailService emailService)
        {
            _conn = conn;
            _emailService = emailService;

        }

        // GET: Show forgot password form
        [HttpGet]
        public IActionResult ForgotPassword()

        {

            return View();
        }

        // POST: Handle email submission
        [HttpPost]
        public async Task<IActionResult> ForgotPassword(AccountModel model)
        {
            if (string.IsNullOrWhiteSpace(model.Email))
            {
                ViewBag.Message = "Please enter your email address.";
                return View();
            }

            if (_conn.State != System.Data.ConnectionState.Open)
                await _conn.OpenAsync();

            string getUserSql = "SELECT id FROM tblusers WHERE LOWER(email) = LOWER(@Email)";
            using var getUserCmd = new NpgsqlCommand(getUserSql, _conn);
            getUserCmd.Parameters.AddWithValue("Email", model.Email);

            var userIdObj = await getUserCmd.ExecuteScalarAsync();

            if (userIdObj == null)
            {
                ViewBag.Message = "The email address you entered was not found. Please check and try again.";
                return View();
            }

            int userId = Convert.ToInt32(userIdObj);
            string token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

            try
            {
                string insertTokenSql = @"
            INSERT INTO password_reset_tokens (user_id, token, expires_at, used)
            VALUES (@userId, @token, @expiresAt, FALSE)";
                using var insertTokenCmd = new NpgsqlCommand(insertTokenSql, _conn);
                insertTokenCmd.Parameters.AddWithValue("userId", userId);
                insertTokenCmd.Parameters.AddWithValue("token", token);
                insertTokenCmd.Parameters.AddWithValue("expiresAt", DateTime.UtcNow.AddHours(1));
                await insertTokenCmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                ViewBag.Message = "Error while generating reset link: " + ex.Message;
                return View();
            }

            string resetUrl = Url.Action("ResetPassword", "Account", new { token = token }, Request.Scheme);
            await _emailService.SendPasswordResetEmail(model.Email, resetUrl);

            ViewBag.Message = "We've sent you a password reset link. Please check your inbox and follow the instructions to reset your password.";
            return View();
        }




        // GET: Show reset password form
        [HttpGet]
        public IActionResult ResetPassword(string token)
        {
            if (string.IsNullOrEmpty(token))
                return BadRequest();

            return View(model: token);
        }

        // POST: Handle new password submission
        [HttpPost]
        public async Task<IActionResult> ResetPassword(string token, string newPassword)
        {
            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(newPassword))
            {
                ModelState.AddModelError("", "Invalid request.");
                return View();
            }

            if (_conn.State != System.Data.ConnectionState.Open)
                await _conn.OpenAsync();

            // Validate token
            string checkTokenSql = @"SELECT user_id, expires_at, used FROM password_reset_tokens WHERE token = @token";
            using var checkTokenCmd = new NpgsqlCommand(checkTokenSql, _conn);
            checkTokenCmd.Parameters.AddWithValue("token", token);

            using var reader = await checkTokenCmd.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
            {
                ModelState.AddModelError("", "Invalid or expired reset link.");
                return View();
            }

            int userId = reader.GetInt32(0);
            DateTime expiresAt = reader.GetDateTime(1);
            bool used = reader.GetBoolean(2);

            await reader.DisposeAsync();

            if (used || expiresAt < DateTime.UtcNow)
            {
                ModelState.AddModelError("", "This reset link is expired or already used.");
                return View();
            }

            // Hash new password (bcrypt recommended)
            string hashedPassword = BCrypt.Net.BCrypt.HashPassword(newPassword);

            // Update user password
            string updatePasswordSql = "UPDATE tblusers SET password = @password WHERE id = @userId";
            using var updatePasswordCmd = new NpgsqlCommand(updatePasswordSql, _conn);
            updatePasswordCmd.Parameters.AddWithValue("password", hashedPassword);
            updatePasswordCmd.Parameters.AddWithValue("userId", userId);
            int rowsAffected = await updatePasswordCmd.ExecuteNonQueryAsync();

            if (rowsAffected == 0)
            {
                ModelState.AddModelError("", "Failed to update password. Please try again.");
                return View();
            }

            // Mark token used
            string markUsedSql = "UPDATE password_reset_tokens SET used = TRUE WHERE token = @token";
            using var markUsedCmd = new NpgsqlCommand(markUsedSql, _conn);
            markUsedCmd.Parameters.AddWithValue("token", token);
            await markUsedCmd.ExecuteNonQueryAsync();

            return RedirectToAction("Index", "Login", new { reset = "success" });
        }

    }
}
