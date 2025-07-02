﻿using Microsoft.AspNetCore.Mvc;
using Mimsv2.Models;
using Npgsql;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Security.Cryptography;
using System.Text;

namespace Mimsv2.Controllers
{
    public class LoginController : Controller
    {
        private readonly IConfiguration _configuration;

        public LoginController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public IActionResult Index()
        {
            return View(new LoginModel());
        }

        public async Task<IActionResult> Edit(int id)
        {
            using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            await conn.OpenAsync();

            string sql = "SELECT * FROM tblusers WHERE id = @id";
            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("id", id);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var user = new LoginModel
                {
                    id = Convert.ToInt32(reader["id"]),
                    loginname = reader["loginname"].ToString(),
                    username = reader["username"].ToString(),
                    surname = reader["surname"].ToString(),
                    email = reader["email"].ToString(),
                    password = reader["password"].ToString(),
                    active = reader["active"].ToString(),
                    department = reader["department"].ToString(),
                    hospitalid = reader["hospitalid"].ToString(),
                    titles = reader["titles"].ToString(),
                    rm = reader["rm"].ToString()
                };

                return View(user);
            }

            return NotFound();
        }

        [HttpPost]
        public async Task<IActionResult> Edit(LoginModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            await conn.OpenAsync();

            string sql = @"
                UPDATE tblusers
                SET loginname = @loginname,
                    username = @username,
                    surname = @surname,
                    email = @email,
                    password = @password,
                    department = @department,
                    hospitalid = @hospitalid,
                    titles = @titles,
                    rm = @rm
                WHERE id = @id";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("id", model.id);
            cmd.Parameters.AddWithValue("loginname", model.loginname);
            cmd.Parameters.AddWithValue("username", model.username);
            cmd.Parameters.AddWithValue("surname", model.surname);
            cmd.Parameters.AddWithValue("email", model.email);
            cmd.Parameters.AddWithValue("password", CalculateMD5Hash(model.password));
            cmd.Parameters.AddWithValue("department", model.department);
            cmd.Parameters.AddWithValue("hospitalid", model.hospitalid);
            cmd.Parameters.AddWithValue("titles", model.titles);
            cmd.Parameters.AddWithValue("rm", model.rm);

            await cmd.ExecuteNonQueryAsync();

            return RedirectToAction("Index");
        }

        public async Task<IActionResult> Delete(int id)
        {
            using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            await conn.OpenAsync();

            string sql = "SELECT * FROM tblusers WHERE id = @id";
            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("id", id);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var user = new LoginModel
                {
                    id = Convert.ToInt32(reader["id"]),
                    loginname = reader["loginname"].ToString(),
                    username = reader["username"].ToString(),
                    surname = reader["surname"].ToString(),
                    email = reader["email"].ToString()
                };
                return View(user);
            }

            return NotFound();
        }

        [HttpPost]
        public async Task<IActionResult> DeleteUser(int id)
        {
            using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            await conn.OpenAsync();

            string sql = "UPDATE tblusers SET active = 'N' WHERE id = @id";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id", id);

            await cmd.ExecuteNonQueryAsync();
            return RedirectToAction("Users");
        }



        [HttpPost]
        public async Task<IActionResult> LoginVerify(LoginModel model)
        {

            string inputLogin = model.loginname?.ToUpper() ?? "";

            using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            await conn.OpenAsync();

            // 1. Get stored password hash and user info
            string getUserSql = @"
        SELECT id, password, loginname, username, surname, email, department, hospitalid, titles, rm, active
        FROM tblusers
        WHERE UPPER(loginname) = @loginname AND active = 'Y'";

            using var getUserCmd = new NpgsqlCommand(getUserSql, conn);
            getUserCmd.Parameters.AddWithValue("loginname", inputLogin);

            using var reader = await getUserCmd.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
            {
                ModelState.AddModelError("loginname", "This login name does not exist. Please register.");
                return View("Index", model);
            }

            string storedPassword = reader["password"].ToString() ?? "";
            string dbLoginName = reader["loginname"].ToString() ?? "";
            string accessLevel = reader["rm"].ToString() ?? "";

            int userId = Convert.ToInt32(reader["id"]);

            //added
            string loginname = reader["loginname"].ToString() ?? "";
            string username = reader["username"].ToString() ?? "";
            string surname = reader["surname"].ToString() ?? "";
            string email = reader["email"].ToString() ?? "";
            string hospitalid = reader["hospitalid"].ToString() ?? "";
            string title = reader["titles"].ToString() ?? "";
            string department = reader["department"].ToString() ?? "";
            //added

            reader.Close();

            // 2. Verify password (detect bcrypt or MD5)
            bool passwordMatches = false;

            if (storedPassword.StartsWith("$2a$") || storedPassword.StartsWith("$2b$"))
            {
                // bcrypt verify
                passwordMatches = BCrypt.Net.BCrypt.Verify(model.password ?? "", storedPassword);
            }
            else
            {
                // legacy MD5 verify
                using var md5 = System.Security.Cryptography.MD5.Create();
                byte[] inputBytes = System.Text.Encoding.UTF8.GetBytes(model.password ?? "");
                byte[] hashBytes = md5.ComputeHash(inputBytes);
                string hashedInputPassword = Convert.ToHexString(hashBytes).ToLower();

                passwordMatches = (hashedInputPassword == storedPassword.ToLower());

                if (passwordMatches)
                {
                    // Upgrade to bcrypt!
                    string newHashedPassword = BCrypt.Net.BCrypt.HashPassword(model.password ?? "");

                    string updateSql = "UPDATE tblusers SET password = @password WHERE id = @userId";
                    using var updateCmd = new NpgsqlCommand(updateSql, conn);
                    updateCmd.Parameters.AddWithValue("password", newHashedPassword);
                    updateCmd.Parameters.AddWithValue("userId", userId);
                    await updateCmd.ExecuteNonQueryAsync();

                }
            }

            if (!passwordMatches)
            {
                ModelState.AddModelError("password", "Incorrect password. Please try again.");

                // Use the variables we stored earlier, not the reader which is closed
                HttpContext.Session.SetString("loginname", loginname);
                HttpContext.Session.SetString("username", username);
                HttpContext.Session.SetString("surname", surname);
                HttpContext.Session.SetString("titles", title);
                HttpContext.Session.SetString("email", email);
                HttpContext.Session.SetString("loginhospitalid", hospitalid);
                HttpContext.Session.SetString("department", department);


                return View("Index", model);
            }

            // 3. Password ok — sign in user
            // Set session data for successful login
            HttpContext.Session.SetString("loginname", loginname);
            HttpContext.Session.SetString("username", username);
            HttpContext.Session.SetString("surname", surname);
            HttpContext.Session.SetString("titles", title);
            HttpContext.Session.SetString("email", email);
            HttpContext.Session.SetString("loginhospitalid", hospitalid);
            HttpContext.Session.SetString("accessLevel", accessLevel);
            HttpContext.Session.SetString("department", department);
           

            var claims = new List<Claim>
{




        new Claim(ClaimTypes.Name, dbLoginName),
        new Claim("AccessLevel", accessLevel),
        new Claim("loginhospitalid", hospitalid)
    };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);



            // 4. Redirect by access level
            return accessLevel.ToLower() switch
            {
                //"admin" => RedirectToAction("AdminDashboard", "Home"),
                "admin" => RedirectToAction("AdminPage", "Chart"),
                "main" => RedirectToAction("MainDashboard", "Home"),
                "local" => RedirectToAction("LocalDashboard", "Home"),
                _ => RedirectToAction("Index", "Home")
            };



        }









        public async Task<IActionResult> Users()
        {
            var users = new List<LoginModel>();

            var accessLevel = HttpContext.Session.GetString("accessLevel");
            var loginHospitalId = HttpContext.Session.GetString("loginhospitalid");

            using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            await conn.OpenAsync();

            // Base SQL
            string sql = @"
        SELECT u.id, u.loginname, u.username, u.surname, u.email, u.active,
               u.department, u.hospitalid, u.titles, u.rm, h.hospital AS hospitalname
        FROM tblusers u
        INNER JOIN tblhospitals h ON u.hospitalid = h.hospitalid
        WHERE u.active = 'Y' AND h.hospitalid != '0'";

            // Add hospital filter for non-admin
            if (accessLevel != "admin")
            {
                sql += " AND u.hospitalid = @loginhospitalid";
            }

            sql += " ORDER BY u.hospitalid";

            using var cmd = new NpgsqlCommand(sql, conn);

            if (accessLevel != "admin")
            {
                cmd.Parameters.AddWithValue("@loginhospitalid", loginHospitalId ?? (object)DBNull.Value);
            }

            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                users.Add(new LoginModel
                {
                    id = Convert.ToInt32(reader["id"]),
                    loginname = reader["loginname"].ToString(),
                    username = reader["username"].ToString(),
                    surname = reader["surname"].ToString(),
                    email = reader["email"].ToString(),
                    active = reader["active"].ToString(),
                    department = reader["department"].ToString(),
                    hospitalid = reader["hospitalid"].ToString(),
                    titles = reader["titles"].ToString(),
                    rm = reader["rm"].ToString(),
                    hospitalname = reader["hospitalname"].ToString()
                });
            }

            return View(users);
        }



        public async Task<IActionResult> Register()
        {
            var model = new RegisterViewModel();
            await PopulateDropdowns(model);
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> RegisterVerify(RegisterViewModel model)
        {
            // if (!ModelState.IsValid)
            //{
            //    await PopulateDropdowns(model);
            //     return View("Register", model);
            // }

            string hashedPassword = BCrypt.Net.BCrypt.HashPassword(model.password);

            using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            await conn.OpenAsync();

            // Check if loginname exists
            string checkSql = "SELECT COUNT(1) FROM tblusers WHERE loginname = @loginname";
            using var checkCmd = new NpgsqlCommand(checkSql, conn);
            checkCmd.Parameters.AddWithValue("loginname", model.loginname);

            var exists = (long)await checkCmd.ExecuteScalarAsync() > 0;
            if (exists)
            {
                ModelState.AddModelError("loginname", "This login name is already taken.");
                await PopulateDropdowns(model);
                return View("Register", model);
            }

            // Check if email already exists
            string checkEmailSql = "SELECT COUNT(*) FROM tblusers WHERE LOWER(email) = LOWER(@Email)";
            using var checkEmailCmd = new NpgsqlCommand(checkEmailSql, conn);
            checkEmailCmd.Parameters.AddWithValue("Email", model.email);
            var emailExists = (long)await checkEmailCmd.ExecuteScalarAsync();

            if (emailExists > 0)
            {
                ModelState.AddModelError("email", "Email is already registered.");
                await PopulateDropdowns(model);
                return View("Register", model);
            }

            string sql = @"
INSERT INTO tblusers 
(loginname, username, surname, email, password, active, department, hospitalid, titles, rm, dateadd)
VALUES 
(@loginname, @username, @surname, @email, @password, 'Y', @department, @hospitalid, @titles, @rm, @dateadd)";

            using var cmd = new NpgsqlCommand(sql, conn);

            cmd.Parameters.AddWithValue("loginname", model.loginname);
            cmd.Parameters.AddWithValue("username", model.username);
            cmd.Parameters.AddWithValue("surname", model.surname);
            cmd.Parameters.AddWithValue("email", model.email);
            cmd.Parameters.AddWithValue("password", hashedPassword);
            cmd.Parameters.AddWithValue("department", model.department);
            cmd.Parameters.AddWithValue("hospitalid", model.hospitalid);
            cmd.Parameters.AddWithValue("titles", model.titles ?? "");
            cmd.Parameters.AddWithValue("rm", model.rm ?? "local");
            cmd.Parameters.AddWithValue("dateadd", model.dateadd);

            await cmd.ExecuteNonQueryAsync();

            return RedirectToAction("Index", "Login");
        }

        private async Task PopulateDropdowns(RegisterViewModel model)
        {
            using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            await conn.OpenAsync();

            // Departments
            string deptSql = "SELECT department FROM tbldepartments ORDER BY department";
            using var deptCmd = new NpgsqlCommand(deptSql, conn);
            using var deptReader = await deptCmd.ExecuteReaderAsync();

            model.Departments = new List<SelectListItem>();
            while (await deptReader.ReadAsync())
            {
                model.Departments.Add(new SelectListItem
                {
                    Value = deptReader["department"].ToString(),
                    Text = deptReader["department"].ToString()
                });
            }
            await deptReader.CloseAsync();

            // Hospitals
            string hospSql = "SELECT hospitalid, hospital FROM tblhospitals ORDER BY hospital";
            using var hospCmd = new NpgsqlCommand(hospSql, conn);
            using var hospReader = await hospCmd.ExecuteReaderAsync();

            model.Hospitals = new List<SelectListItem>();
            while (await hospReader.ReadAsync())
            {
                model.Hospitals.Add(new SelectListItem
                {
                    Value = hospReader["hospitalid"].ToString(),
                    Text = hospReader["hospital"].ToString()
                });
            }
            await hospReader.CloseAsync();

            // Titles
            string titleSql = "SELECT id, title FROM tbltitles ORDER BY title";
            using var titleCmd = new NpgsqlCommand(titleSql, conn);
            using var titleReader = await titleCmd.ExecuteReaderAsync();

            model.Titles = new List<SelectListItem>();
            while (await titleReader.ReadAsync())
            {
                model.Titles.Add(new SelectListItem
                {
                    Value = titleReader["title"].ToString(),
                    Text = titleReader["title"].ToString()
                });
            }
            await titleReader.CloseAsync();
        }

        private string CalculateMD5Hash(string input)
        {
            using var md5 = MD5.Create();
            byte[] inputBytes = Encoding.ASCII.GetBytes(input);
            byte[] hashBytes = md5.ComputeHash(inputBytes);
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
        }

        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public async Task<IActionResult> Logout()
        //{
        //    await HttpContext.SignOutAsync(); // Signs out the current user

        //    return RedirectToAction("Index", "Login"); // Redirect to login page or homepage
        //}
        [HttpGet, HttpPost]

        public async Task<IActionResult> Logout()
        {

            await HttpContext.SignOutAsync();
            //return RedirectToAction("Index", "Login");

            return RedirectToAction("Index", "Home");
        }










        //ADD edit here
























        public async Task<IActionResult> EditUsers(string hospitalId)
        {
            var users = new List<LoginModel>();

            using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            await conn.OpenAsync();

            string sql = @"
        SELECT id, loginname, username, surname, email, active, department, hospitalid, titles, rm 
        FROM tblusers
        WHERE hospitalid = @hospitalId";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("hospitalId", hospitalId ?? string.Empty);


            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                users.Add(new LoginModel
                {
                    id = Convert.ToInt32(reader["id"]),
                    loginname = reader["loginname"].ToString(),
                    username = reader["username"].ToString(),
                    surname = reader["surname"].ToString(),
                    email = reader["email"].ToString(),
                    active = reader["active"].ToString(),
                    department = reader["department"].ToString(),
                    hospitalid = reader["hospitalid"].ToString(),
                    titles = reader["titles"].ToString(),
                    rm = reader["rm"].ToString()
                });
            }

            return View(Users);
            //return View(EditUsers);
        }











        public async Task<IActionResult> EditUser(int id)
        {
            var model = new RegisterViewModel();


            using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            await conn.OpenAsync();

            string sql = "SELECT * FROM tblusers WHERE id = @id";
            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("id", id);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                model.id = id;
                model.loginname = reader["loginname"].ToString();
                model.username = reader["username"].ToString();
                model.surname = reader["surname"].ToString();
                model.email = reader["email"].ToString();
                model.department = reader["department"].ToString();
                model.hospitalid = reader["hospitalid"].ToString();
                model.titles = reader["titles"].ToString();
                model.active = reader["active"].ToString();
            }

            await PopulateDropdowns(model);
            return View("EditUsers", model);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateUser(RegisterViewModel model, string submit)
        {
            using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            await conn.OpenAsync();

            string sql = @"
        UPDATE tblusers SET 
            loginname = @loginname,
            username = @username,
            surname = @surname,
            email = @email,
            department = @department,
            hospitalid = @hospitalid,
            titles = @titles,
            active = @active
        WHERE id = @id";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("loginname", model.loginname);
            cmd.Parameters.AddWithValue("username", model.username);
            cmd.Parameters.AddWithValue("surname", model.surname);
            cmd.Parameters.AddWithValue("email", model.email);
            cmd.Parameters.AddWithValue("department", model.department);
            cmd.Parameters.AddWithValue("hospitalid", model.hospitalid);
            cmd.Parameters.AddWithValue("titles", model.titles);
            cmd.Parameters.AddWithValue("active", submit == "Delete" ? "N" : "Y");
            cmd.Parameters.AddWithValue("id", model.id);

            await cmd.ExecuteNonQueryAsync();

            return RedirectToAction("Users");
        }


    }
}
