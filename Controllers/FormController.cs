﻿using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Configuration;
using Mimsv2.Models;
using Mimsv2.Services;
using Npgsql;
using System.Collections.Generic;
using System.Threading.Tasks;


namespace Mimsv2.Controllers
{
    public class FormController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly EmailService _emailService;

        public FormController(IConfiguration configuration, EmailService emailService)
        {
            _configuration = configuration;
            _emailService = emailService;
        }

        public async Task<IActionResult> IncidentForm()
        {
            var model = new FormModel();

            // Populate session values
            model.CapturedByLoginName = HttpContext.Session.GetString("loginname") ?? "Unknown";
            model.CapturedByName = HttpContext.Session.GetString("username") ?? "Unknown";
            model.CapturedBySurname = HttpContext.Session.GetString("surname") ?? "Unknown";
            model.CapturedByTitle = HttpContext.Session.GetString("titles") ?? "Unknown";
            model.CapturedByEmail = HttpContext.Session.GetString("email") ?? "Unknown";
            model.CapturedbyDpt = HttpContext.Session.GetString("department") ?? "Unknown";
            model.HospitalId = HttpContext.Session.GetString("loginhospitalid") ?? "0";
            model.AccessLevel = HttpContext.Session.GetString("accessLevel") ?? "User";

            if (model.AccessLevel == "admin")
            {
                await PopulateDropdowns(model); // Load all hospitals
            }
            else
            {
                var loginHospitalId = HttpContext.Session.GetString("loginhospitalid") ?? "0";
                model.hospitalid = loginHospitalId;

                await PopulateDropdowns(model); // Load only this hospital
            }

            //await PopulateDropdowns(model);
            return View(model);
        }

        private async Task PopulateDropdowns(FormModel model)
        {
            using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            await conn.OpenAsync();

            // 1. Departments Dropdown
            string deptSql = "SELECT department FROM tbldepartments WHERE description = 'ward' ORDER BY department";
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

            model.IncidentCategories = await GetDistinctCategories(); // cat

            if (!string.IsNullOrEmpty(model.inctypescat1))
            {
                model.IncidentSubCat1 = await GetSubCategories(1, model.inctypescat1);
            }

            if (!string.IsNullOrEmpty(model.inctypescat2))
            {
                model.IncidentSubCat2 = await GetSubCategories(2, model.inctypescat2);
            }

            if (!string.IsNullOrEmpty(model.inctypescat3))
            {
                model.IncidentSubCat3 = await GetSubCategories(3, model.inctypescat3);
            }


            // 2. Add another dropdown list here (Example: Locations)

            string locationSql = "SELECT hospitalid, hospital FROM tblhospitals WHERE active = 'Y' ORDER BY hospitalid";
            using var locationCmd = new NpgsqlCommand(locationSql, conn);
            using var locationReader = await locationCmd.ExecuteReaderAsync();
            
            model.Hospitals = new List<SelectListItem>();
            while (await locationReader.ReadAsync())
            {
                model.Hospitals.Add(new SelectListItem
                {
                    Value = locationReader["hospitalid"].ToString(),
                    Text = locationReader["hospital"].ToString()
                });
            }
            await locationReader.CloseAsync();
            

            // 3. Add another dropdown list here (Example: Incident Types)
            /*
            string incidentTypeSql = "SELECT code, description FROM tblIncidentTypes ORDER BY description";
            using var incidentTypeCmd = new NpgsqlCommand(incidentTypeSql, conn);
            using var incidentTypeReader = await incidentTypeCmd.ExecuteReaderAsync();
            
            model.IncidentTypes = new List<SelectListItem>();
            while (await incidentTypeReader.ReadAsync())
            {
                model.IncidentTypes.Add(new SelectListItem
                {
                    Value = incidentTypeReader["code"].ToString(),
                    Text = incidentTypeReader["description"].ToString()
                });
            }
            await incidentTypeReader.CloseAsync();
            */

            // 4. Add more dropdown lists as needed following the same pattern
        }


        //SECTION A
        [HttpGet]
        public async Task<IActionResult> GetDepartmentsByHospital(int hospitalId)
        {
            try
            {
                var departments = new List<SelectListItem>();

                using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                await conn.OpenAsync();

                string sql = "SELECT department FROM tbldepartments WHERE description = 'ward' AND hospitalid = @hid ORDER BY department";
                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@hid", hospitalId.ToString()); // FIXED HERE

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    departments.Add(new SelectListItem
                    {
                        Value = reader["department"].ToString(),
                        Text = reader["department"].ToString()
                    });
                }

                return Json(departments);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = "Failed to load departments", details = ex.Message });
            }
        }



        //SECTION B
        [HttpGet]
        public async Task<IActionResult> GetIncidentCategories()
        {
            var list = new List<SelectListItem>();

            using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            await conn.OpenAsync();

            string sql = "SELECT DISTINCT cat FROM tblincidenttype WHERE active = 'Y' ORDER BY cat";
            using var cmd = new NpgsqlCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var cat = reader["cat"].ToString();
                if (!string.IsNullOrWhiteSpace(cat))
                {
                    list.Add(new SelectListItem { Value = cat, Text = cat });
                }
            }

            return Json(list);
        }

        [HttpGet]
        public async Task<IActionResult> GetSubCategory1(string cat)
        {
            var list = new List<SelectListItem>();

            using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            await conn.OpenAsync();

            string sql = "SELECT DISTINCT subcat1 FROM tblincidenttype WHERE active = 'Y' AND cat = @cat ORDER BY subcat1";
            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("cat", cat ?? "");

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var val = reader["subcat1"].ToString();
                if (!string.IsNullOrWhiteSpace(val))
                {
                    list.Add(new SelectListItem { Value = val, Text = val });
                }
            }

            return Json(list);
        }

        [HttpGet]
        public async Task<IActionResult> GetSubCategory2(string sub1)
        {
            var list = new List<SelectListItem>();

            using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            await conn.OpenAsync();

            string sql = "SELECT DISTINCT subcat2 FROM tblincidenttype WHERE active = 'Y' AND subcat1 = @sub1 ORDER BY subcat2";
            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("sub1", sub1 ?? "");

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var val = reader["subcat2"].ToString();
                if (!string.IsNullOrWhiteSpace(val))
                {
                    list.Add(new SelectListItem { Value = val, Text = val });
                }
            }

            return Json(list);
        }

        [HttpGet]
        public async Task<IActionResult> GetSubCategory3(string sub2)
        {
            var list = new List<SelectListItem>();

            using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            await conn.OpenAsync();

            string sql = "SELECT DISTINCT subcat3 FROM tblincidenttype WHERE active = 'Y' AND subcat2 = @sub2 ORDER BY subcat3";
            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("sub2", sub2 ?? "");

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var val = reader["subcat3"].ToString();
                if (!string.IsNullOrWhiteSpace(val))
                {
                    list.Add(new SelectListItem { Value = val, Text = val });
                }
            }

            return Json(list);
        }
        // Load all categories



private async Task<List<SelectListItem>> GetDistinctCategories()
{
    var result = new List<SelectListItem>();
    using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
    await conn.OpenAsync();

    var cmd = new NpgsqlCommand("SELECT DISTINCT cat FROM tblincidenttype WHERE active = 'Y' ORDER BY cat", conn);
    using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        result.Add(new SelectListItem
        {
            Value = reader["cat"].ToString(),
            Text = reader["cat"].ToString()
        });
    }
    return result;
}

private async Task<List<SelectListItem>> GetSubCategories(int level, string parentValue)
{
    string field = level switch
    {
        1 => "subcat1",
        2 => "subcat2",
        3 => "subcat3",
        _ => throw new ArgumentException("Invalid level")
    };

    string parentField = level switch
    {
        1 => "cat",
        2 => "subcat1",
        3 => "subcat2",
        _ => throw new ArgumentException("Invalid level")
    };

    var result = new List<SelectListItem>();
    using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
    await conn.OpenAsync();

    var sql = $"SELECT DISTINCT {field} FROM tblincidenttype WHERE {parentField} = @parent AND active = 'Y' ORDER BY {field}";
    using var cmd = new NpgsqlCommand(sql, conn);
    cmd.Parameters.AddWithValue("parent", parentValue);

    using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        result.Add(new SelectListItem
        {
            Value = reader[field].ToString(),
            Text = reader[field].ToString()
        });
    }

    return result;
}


        //SECTION B END



        //SECTION C
        //[HttpGet]
        //public async Task<IActionResult> GetUsers(int hospitalId)
        //{
        //    try
        //    {
        //        var loginname = new List<SelectListItem>();

        //        using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
        //        await conn.OpenAsync();

        //        string sql = "SELECT * FROM tblusers WHERE active = 'Y' AND hospitalid = @hid ORDER BY username";
        //        using var cmd = new NpgsqlCommand(sql, conn);
        //        cmd.Parameters.AddWithValue("@hid", hospitalId.ToString()); // FIXED HERE

        //        using var reader = await cmd.ExecuteReaderAsync();
        //        while (await reader.ReadAsync())
        //        {
        //            loginname.Add(new SelectListItem
        //            {
        //                Value = reader["loginname"].ToString(),
        //                Text = reader["username"].ToString()
        //            });
        //        }

        //        return Json(loginname);
        //    }
        //    catch (Exception ex)
        //    {
        //        return BadRequest(new { error = "Failed to load user", details = ex.Message });
        //    }
        //}



        //[HttpGet]
        //public async Task<IActionResult> GetInvestigators(int hospitalid)
        //{

        //    var list = new List<SelectListItem>();

        //    using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
        //    await conn.OpenAsync();

        //    string sql = @"SELECT * FROM tblusers
        //           WHERE hospitalid = @hospitalId AND active = 'Y'
        //           ORDER BY username, surname";

        //    using var cmd = new NpgsqlCommand(sql, conn);
        //    //cmd.Parameters.AddWithValue("@hospitalId", hospitalId);
        //    cmd.Parameters.AddWithValue("@hospitalId", hospitalid.ToString());

        //    using var reader = await cmd.ExecuteReaderAsync();
        //    while (await reader.ReadAsync())
        //    {
        //        string loginname = reader["loginname"].ToString();
        //        string fullName = $"{reader["username"]} {reader["surname"]}";
        //        list.Add(new SelectListItem { Value = loginname, Text = fullName });
        //    }

        //    return Json(list);
        //}

        [HttpGet]
        public async Task<List<SelectListItem>> GetInvestigators(int hospitalid)
        {

            var list = new List<SelectListItem>();

            using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            await conn.OpenAsync();

            string sql = @"SELECT * FROM tblusers
                   WHERE hospitalid = @hospitalId AND active = 'Y'
                   ORDER BY username, surname";

            using var cmd = new NpgsqlCommand(sql, conn);
            //cmd.Parameters.AddWithValue("@hospitalId", hospitalId);
            cmd.Parameters.AddWithValue("@hospitalId", hospitalid.ToString());

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                string loginname = reader["loginname"].ToString();
                string fullName = $"{reader["username"]} {reader["surname"]}";
                list.Add(new SelectListItem { Value = loginname, Text = fullName });
            }

            return list;
        }

        [HttpGet]
        public async Task<IActionResult> GetInvestigatorDetails(string loginname)
        {
            using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            await conn.OpenAsync();

            string sql = @"SELECT * FROM tblusers
                   WHERE loginname = @loginname AND active = 'Y'";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@loginname", loginname);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return Json(new
                {
                    username = reader["username"].ToString(),
                    surname = reader["surname"].ToString(),
                    email = reader["email"].ToString()
                });
            }

            return Json(null);
        }



        //SECTION C END



        //SECTION D
        [HttpGet]
        public async Task<IActionResult> GetEmp()
        {
            var list = new List<SelectListItem>();

            using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            await conn.OpenAsync();

            string sql = @"SELECT Distinct dpt FROM tblempcat
                   WHERE active = 'Y'
                   ORDER BY dpt";

            using var cmd = new NpgsqlCommand(sql, conn);


            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                string dpt = reader["dpt"].ToString();
                string fullName = $"{reader["dpt"]}";
                list.Add(new SelectListItem { Value = dpt, Text = fullName });
            }

            return Json(list);
        }

        [HttpGet]
        public async Task<IActionResult> GetEmpDetails(string dpt)
        {
            var list = new List<SelectListItem>();

            using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            await conn.OpenAsync();

            string sql = @"SELECT dptcat FROM tblempcat
                   WHERE dpt = @dpt AND active = 'Y'
                   ORDER BY dptcat";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@dpt", dpt);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                string dptcat = reader["dptcat"].ToString();
                list.Add(new SelectListItem { Value = dptcat, Text = dptcat });
            }

            return Json(list);
        }


        //SECTION D END






        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitForm(FormModel model)
        {
            if (!ModelState.IsValid)
            {
                await PopulateDropdowns(model);
                return View("IncidentForm", model);
            }

            // Form submission logic goes here

            return RedirectToAction("Success");
        }


        //INPUT FORM

        [HttpGet]
        public async Task<IActionResult> IncidentForm(FormModel model)
        {
            // Pull access level and loginhospitalid from session
            string accessLevel = HttpContext.Session.GetString("accessLevel");
            string loginHospitalId = HttpContext.Session.GetString("loginhospitalid");

            model.AccessLevel = accessLevel;

            if (accessLevel == "admin")
            {
                // Admins can choose any hospital
                await PopulateDropdowns(model);
            }
            else
            {
                // Non-admin: Lock hospital, load only relevant dropdowns
                model.hospitalid = loginHospitalId;

                await PopulateDropdowns(model);

                using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                await conn.OpenAsync();

                string hospSql = "SELECT hospital FROM tblhospitals WHERE hospitalid = @id";
                using var hospCmd = new NpgsqlCommand(hospSql, conn);
                hospCmd.Parameters.AddWithValue("id", loginHospitalId);
                var result = await hospCmd.ExecuteScalarAsync();
                model.Hospital = result?.ToString();
            }

            return View(model);
        }







        //[HttpPost]
        //public async Task<IActionResult> IncidentFormInput(FormModel model)
        //{
        //    using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
        //    await conn.OpenAsync();

        //    string sql = @"INSERT INTO tblincident (
        //    affectedward, 
        //    incidentarea, 
        //    incidentcriteria, 
        //    incidentcriteriasub, 
        //    requester,
        //    requesteremail, 
        //    priority, 
        //    titles, 
        //    reportedby, 
        //    invesitgatedby, 
        //    assignedcat, 
        //    assignedstaff, 
        //    incidentdate, 
        //    incidenttime, 
        //    datereported, 
        //    datecaptured, 
        //    summary, 
        //    description, 
        //    timecaptured, 
        //    active, 
        //    status, 
        //    hospitalid, 
        //    qarid, 
        //    username, 
        //    surname, 
        //    onholddescdate, 
        //    onholddesctime, 
        //    closeddesctime, 
        //    onholddesc, 
        //    closeddesc, 
        //    pte, 
        //    ptenumber, 
        //    ptename, 
        //    ptesurname, 
        //    ptetitle, 
        //    reportedbyemail, 
        //    correctaction, 
        //    correctactiontime, 
        //    preventaction, 
        //    preventactiontime, 
        //    preventactiondate, 
        //    correctactiondate, 
        //    investigation, 
        //    summary2, 
        //    medrelatedtotal, 
        //    reportedbydepartment, 
        //    incidentexpires, 
        //    incidentareanight, 
        //    acquired, 
        //    incidenttype, 
        //    inctypescat1, 
        //    inctypescat2
        //) VALUES (
        //    @affectedward, 
        //    @incidentarea, 
        //    @incidentcriteria, 
        //    @incidentcriteriasub, 
        //    @requester,  
        //    @requesteremail, 
        //    @priority, 
        //    @titles, 
        //    @reportedby, 
        //    @invesitgatedby, 
        //    @assignedcat, 
        //    @assignedstaff, 
        //    @incidentdate, 
        //    @incidenttime, 
        //    @datereported, 
        //    @datecaptured, 
        //    @summary, 
        //    @description, 
        //    @timecaptured, 
        //    @active, 
        //    @status, 
        //    @hospitalid, 
        //    @qarid, 
        //    @username, 
        //    @surname, 
        //    @onholddescdate, 
        //    @onholddesctime, 
        //    @closeddesctime, 
        //    @onholddesc, 
        //    @closeddesc, 
        //    @pte, 
        //    @ptenumber, 
        //    @ptename, 
        //    @ptesurname, 
        //    @ptetitle, 
        //    @reportedbyemail, 
        //    @correctaction, 
        //    @correctactiontime, 
        //    @preventaction, 
        //    @preventactiontime, 
        //    @preventactiondate, 
        //    @correctactiondate, 
        //    @investigation, 
        //    @summary2, 
        //    @medrelatedtotal, 
        //    @reportedbydepartment, 
        //    @incidentexpires, 
        //    @incidentareanight, 
        //    @acquired, 
        //    @incidenttype, 
        //    @inctypescat1, 
        //    @inctypescat2
        //)
        //RETURNING id;";

        //    int insertedId;
        //    using var cmd = new NpgsqlCommand(sql, conn);
        //    cmd.Parameters.AddWithValue("@affectedward", model.affectedward ?? (object)DBNull.Value);
        //    cmd.Parameters.AddWithValue("@incidentarea", model.incidentarea ?? (object)DBNull.Value);
        //    cmd.Parameters.AddWithValue("@incidentcriteria", model.incidentcriteria ?? (object)DBNull.Value);
        //    cmd.Parameters.AddWithValue("@incidentcriteriasub", model.incidentcriteriasub ?? (object)DBNull.Value);
        //    cmd.Parameters.AddWithValue("@requester", model.CapturedByLoginName ?? (object)DBNull.Value);
        //    cmd.Parameters.AddWithValue("@requesteremail", model.CapturedByEmail ?? (object)DBNull.Value);
        //    cmd.Parameters.AddWithValue("@priority", model.priority ?? (object)DBNull.Value);
        //    cmd.Parameters.AddWithValue("@titles", model.CapturedByTitle ?? (object)DBNull.Value);
        //    cmd.Parameters.AddWithValue("@reportedby", model.CapturedByLoginName ?? (object)DBNull.Value);
        //    cmd.Parameters.AddWithValue("@invesitgatedby", model.invesitgatedby ?? (object)DBNull.Value);
        //    cmd.Parameters.AddWithValue("@assignedcat", model.assignedcat ?? (object)DBNull.Value);
        //    cmd.Parameters.AddWithValue("@assignedstaff", model.assignedstaff ?? (object)DBNull.Value);
        //    cmd.Parameters.AddWithValue("@incidentdate", model.incidentdate ?? (object)DBNull.Value);
        //    cmd.Parameters.AddWithValue("@incidenttime", model.incidenttime ?? (object)DBNull.Value);
        //    cmd.Parameters.AddWithValue("@datereported", model.datereported ?? (object)DBNull.Value);
        //    cmd.Parameters.AddWithValue("@datecaptured", model.datecaptured?.Date ?? DateTime.Today);
        //    cmd.Parameters.AddWithValue("@summary", model.summary ?? (object)DBNull.Value);
        //    cmd.Parameters.AddWithValue("@description", model.description ?? (object)DBNull.Value);
        //    cmd.Parameters.AddWithValue("@timecaptured", string.IsNullOrWhiteSpace(model.timecaptured) ? DateTime.Now.ToString("HH:mm") : model.timecaptured);
        //    cmd.Parameters.AddWithValue("@active", model.active ?? (object)DBNull.Value);
        //    cmd.Parameters.AddWithValue("@status", model.status ?? (object)DBNull.Value);
        //    cmd.Parameters.AddWithValue("@hospitalid", model.inserthospitalid ?? (object)DBNull.Value);
        //    cmd.Parameters.AddWithValue("@qarid", model.qarid ?? (object)DBNull.Value);
        //    cmd.Parameters.AddWithValue("@username", model.CapturedByName ?? (object)DBNull.Value);
        //    cmd.Parameters.AddWithValue("@surname", model.CapturedBySurname ?? (object)DBNull.Value);
        //    cmd.Parameters.AddWithValue("@onholddescdate", model.onholddescdate ?? (object)DBNull.Value);
        //    cmd.Parameters.AddWithValue("@closeddescdate", model.closeddescdate ?? (object)DBNull.Value);
        //    cmd.Parameters.AddWithValue("@onholddesctime", model.onholddesctime ?? (object)DBNull.Value);
        //    cmd.Parameters.AddWithValue("@closeddesctime", model.closeddesctime ?? (object)DBNull.Value);
        //    cmd.Parameters.AddWithValue("@onholddesc", model.onholddesc ?? (object)DBNull.Value);
        //    cmd.Parameters.AddWithValue("@closeddesc", model.closeddesc ?? (object)DBNull.Value);
        //    cmd.Parameters.AddWithValue("@pte", model.pte ?? (object)DBNull.Value);
        //    cmd.Parameters.AddWithValue("@ptenumber", model.ptenumber ?? (object)DBNull.Value);
        //    cmd.Parameters.AddWithValue("@ptename", model.ptename ?? (object)DBNull.Value);
        //    cmd.Parameters.AddWithValue("@ptesurname", model.ptesurname ?? (object)DBNull.Value);
        //    cmd.Parameters.AddWithValue("@ptetitle", model.ptetitle ?? (object)DBNull.Value);
        //    cmd.Parameters.AddWithValue("@reportedbyemail", model.CapturedByEmail ?? (object)DBNull.Value);
        //    cmd.Parameters.AddWithValue("@correctaction", model.correctaction ?? (object)DBNull.Value);
        //    cmd.Parameters.AddWithValue("@correctactiontime", model.correctactiontime ?? (object)DBNull.Value);
        //    cmd.Parameters.AddWithValue("@preventaction", model.preventaction ?? (object)DBNull.Value);
        //    cmd.Parameters.AddWithValue("@preventactiontime", model.preventactiontime ?? (object)DBNull.Value);
        //    cmd.Parameters.AddWithValue("@preventactiondate", model.preventactiondate ?? (object)DBNull.Value);
        //    cmd.Parameters.AddWithValue("@correctactiondate", model.correctactiondate ?? (object)DBNull.Value);
        //    cmd.Parameters.AddWithValue("@investigation", model.investigation ?? (object)DBNull.Value);
        //    cmd.Parameters.AddWithValue("@summary2", model.summary2 ?? (object)DBNull.Value);
        //    cmd.Parameters.AddWithValue("@medrelatedtotal", model.medrelatedtotal ?? (object)DBNull.Value);
        //    cmd.Parameters.AddWithValue("@reportedbydepartment", model.CapturedbyDpt ?? (object)DBNull.Value);
        //    cmd.Parameters.AddWithValue("@incidentexpires", DateTime.Now.Date.AddDays(5));
        //    cmd.Parameters.AddWithValue("@incidentareanight", model.incidentareanight ?? (object)DBNull.Value);
        //    cmd.Parameters.AddWithValue("@acquired", model.acquired ?? (object)DBNull.Value);
        //    cmd.Parameters.AddWithValue("@incidenttype", model.incidenttype ?? (object)DBNull.Value);
        //    cmd.Parameters.AddWithValue("@inctypescat1", model.inctypescat1 ?? (object)DBNull.Value);
        //    cmd.Parameters.AddWithValue("@inctypescat2", model.inctypescat2 ?? (object)DBNull.Value);

        //    insertedId = Convert.ToInt32(await cmd.ExecuteScalarAsync());

        //    // Build QARID
        //    string abbr = "";
        //    string abbrSql = "SELECT abbr FROM tblhospitals WHERE hospitalid = @hospitalid";
        //    using (var abbrCmd = new NpgsqlCommand(abbrSql, conn))
        //    {
        //        //abbrCmd.Parameters.AddWithValue("@hospitalid", model.hospitalid);
        //        abbrCmd.Parameters.AddWithValue("@hospitalid", model.inserthospitalid ?? (object)DBNull.Value);
        //        abbr = (await abbrCmd.ExecuteScalarAsync())?.ToString() ?? "XX";
        //    }

        //    var now = DateTime.Now;
        //    //string qarid = $"{abbr}/{now.Day}/{now.Month}/{insertedId}";
        //    string qarid = $"{abbr}-{now.Day}-{now.Month}-{insertedId}";

        //    string updateSql = "UPDATE tblincident SET qarid = @qarid WHERE id = @id";
        //    using (var updateCmd = new NpgsqlCommand(updateSql, conn))
        //    {
        //        updateCmd.Parameters.AddWithValue("@qarid", qarid);
        //        updateCmd.Parameters.AddWithValue("@id", insertedId);
        //        await updateCmd.ExecuteNonQueryAsync();
        //    }

        //    if (model.UploadedFiles != null && model.UploadedFiles.Any())
        //    {
        //        foreach (var filename in model.UploadedFiles)
        //        {
        //            string fileUpdateSql = @"
        //        UPDATE incidentattachments
        //        SET qarid = @qarid
        //        WHERE attachment = @filename AND (qarid IS NULL OR qarid = '' OR qarid = '0')";
        //            using var fileCmd = new NpgsqlCommand(fileUpdateSql, conn);
        //            fileCmd.Parameters.AddWithValue("@qarid", qarid);
        //            fileCmd.Parameters.AddWithValue("@filename", filename);
        //            await fileCmd.ExecuteNonQueryAsync();
        //        }
        //    }

        //    return RedirectToAction("EditIncident", "Form", new { id = qarid, inserthospitalid = model.inserthospitalid });

        //}


        [HttpPost]
        public async Task<IActionResult> IncidentFormInput(FormModel model)
        {
            using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            await conn.OpenAsync();

            string sql = @"INSERT INTO tblincident (
        affectedward, incidentarea, incidentcriteria, incidentcriteriasub, requester,
        requesteremail, priority, titles, reportedby, invesitgatedby, assignedcat, 
        assignedstaff, incidentdate, incidenttime, datereported, datecaptured, summary, 
        description, timecaptured, active, status, hospitalid, qarid, username, surname, 
        onholddescdate, onholddesctime, closeddesctime, onholddesc, closeddesc, pte, 
        ptenumber, ptename, ptesurname, ptetitle, reportedbyemail, correctaction, 
        correctactiontime, preventaction, preventactiontime, preventactiondate, 
        correctactiondate, investigation, summary2, medrelatedtotal, reportedbydepartment, 
        incidentexpires, incidentareanight, acquired, incidenttype, inctypescat1, inctypescat2
    ) VALUES (
        @affectedward, @incidentarea, @incidentcriteria, @incidentcriteriasub, @requester,  
        @requesteremail, @priority, @titles, @reportedby, @invesitgatedby, @assignedcat, 
        @assignedstaff, @incidentdate, @incidenttime, @datereported, @datecaptured, @summary, 
        @description, @timecaptured, @active, @status, @hospitalid, @qarid, @username, @surname, 
        @onholddescdate, @onholddesctime, @closeddesctime, @onholddesc, @closeddesc, @pte, 
        @ptenumber, @ptename, @ptesurname, @ptetitle, @reportedbyemail, @correctaction, 
        @correctactiontime, @preventaction, @preventactiontime, @preventactiondate, 
        @correctactiondate, @investigation, @summary2, @medrelatedtotal, @reportedbydepartment, 
        @incidentexpires, @incidentareanight, @acquired, @incidenttype, @inctypescat1, @inctypescat2
    )
    RETURNING id;";

            int insertedId;
            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@affectedward", model.affectedward ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@incidentarea", model.incidentarea ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@incidentcriteria", model.incidentcriteria ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@incidentcriteriasub", model.incidentcriteriasub ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@requester", model.CapturedByLoginName ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@requesteremail", model.CapturedByEmail ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@priority", model.priority ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@titles", model.CapturedByTitle ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@reportedby", model.CapturedByLoginName ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@invesitgatedby", model.invesitgatedby ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@assignedcat", model.assignedcat ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@assignedstaff", model.assignedstaff ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@incidentdate", model.incidentdate ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@incidenttime", model.incidenttime ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@datereported", model.datereported ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@datecaptured", model.datecaptured?.Date ?? DateTime.Today);
            cmd.Parameters.AddWithValue("@summary", model.summary ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@description", model.description ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@timecaptured", string.IsNullOrWhiteSpace(model.timecaptured) ? DateTime.Now.ToString("HH:mm") : model.timecaptured);
            cmd.Parameters.AddWithValue("@active", model.active ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@status", model.status ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@hospitalid", model.inserthospitalid ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@qarid", model.qarid ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@username", model.CapturedByName ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@surname", model.CapturedBySurname ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@onholddescdate", model.onholddescdate ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@closeddescdate", model.closeddescdate ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@onholddesctime", model.onholddesctime ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@closeddesctime", model.closeddesctime ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@onholddesc", model.onholddesc ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@closeddesc", model.closeddesc ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@pte", model.pte ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@ptenumber", model.ptenumber ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@ptename", model.ptename ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@ptesurname", model.ptesurname ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@ptetitle", model.ptetitle ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@reportedbyemail", model.CapturedByEmail ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@correctaction", model.correctaction ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@correctactiontime", model.correctactiontime ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@preventaction", model.preventaction ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@preventactiontime", model.preventactiontime ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@preventactiondate", model.preventactiondate ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@correctactiondate", model.correctactiondate ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@investigation", model.investigation ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@summary2", model.summary2 ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@medrelatedtotal", model.medrelatedtotal ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@reportedbydepartment", model.CapturedbyDpt ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@incidentexpires", DateTime.Now.Date.AddDays(5));
            cmd.Parameters.AddWithValue("@incidentareanight", model.incidentareanight ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@acquired", model.acquired ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@incidenttype", model.incidenttype ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@inctypescat1", model.inctypescat1 ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@inctypescat2", model.inctypescat2 ?? (object)DBNull.Value);

            insertedId = Convert.ToInt32(await cmd.ExecuteScalarAsync());

            // Generate qarid
            string abbr = "";
            string abbrSql = "SELECT abbr FROM tblhospitals WHERE hospitalid = @hospitalid";
            using (var abbrCmd = new NpgsqlCommand(abbrSql, conn))
            {
                abbrCmd.Parameters.AddWithValue("@hospitalid", model.inserthospitalid ?? (object)DBNull.Value);
                abbr = (await abbrCmd.ExecuteScalarAsync())?.ToString() ?? "XX";
            }

            var now = DateTime.Now;
            string qarid = $"{abbr}-{now.Day}-{now.Month}-{insertedId}";

            string updateSql = "UPDATE tblincident SET qarid = @qarid WHERE id = @id";
            using (var updateCmd = new NpgsqlCommand(updateSql, conn))
            {
                updateCmd.Parameters.AddWithValue("@qarid", qarid);
                updateCmd.Parameters.AddWithValue("@id", insertedId);
                await updateCmd.ExecuteNonQueryAsync();
            }

            // Update attachment qarid
            if (model.UploadedFiles != null && model.UploadedFiles.Any())
            {
                foreach (var filename in model.UploadedFiles)
                {
                    string fileUpdateSql = @"
            UPDATE incidentattachments
            SET qarid = @qarid
            WHERE attachment = @filename AND (qarid IS NULL OR qarid = '' OR qarid = '0')";
                    using var fileCmd = new NpgsqlCommand(fileUpdateSql, conn);
                    fileCmd.Parameters.AddWithValue("@qarid", qarid);
                    fileCmd.Parameters.AddWithValue("@filename", filename);
                    await fileCmd.ExecuteNonQueryAsync();
                }
            }

            //  Send Email
            try
            {
                string emailSql = @"SELECT i.qarid, i.username, i.surname, i.invesitgatedby, i.datecaptured, i.datereported, i.description, 
                                   h.hospital, 
                                   u.email as investigator_email, u.username as investigator_name, u.surname as investigator_surname
                            FROM tblincident i
                            LEFT JOIN tblhospitals h ON h.hospitalid = i.hospitalid
                            LEFT JOIN tblusers u ON u.loginname = i.invesitgatedby
                            WHERE i.qarid = @qarid";

                using var emailCmd = new NpgsqlCommand(emailSql, conn);
                emailCmd.Parameters.AddWithValue("@qarid", qarid);

                using var reader = await emailCmd.ExecuteReaderAsync();

                if (reader.Read())
                {
                    string hospitalName = reader["hospital"].ToString();
                    string capturedBy = reader["username"] + " " + reader["surname"];
                    string investigator = reader["investigator_name"] + " " + reader["investigator_surname"];
                    string investigatorEmail = reader["investigator_email"].ToString();
                    string dateCaptured = Convert.ToDateTime(reader["datecaptured"]).ToString("yyyy-MM-dd");
                    string dateReported = Convert.ToDateTime(reader["datereported"]).ToString("yyyy-MM-dd");
                    string description = reader["description"].ToString();

                    string subject = $"New Incident Created - {qarid}";
                    string body = $@"
<html>
<body style='font-family:Segoe UI, sans-serif; font-size:14px; color:#333;'>
    <h2 style='color:#005691;'>New Incident Notification</h2>
    <p><b>Hospital:</b> {hospitalName}</p>
    <p><b>New Incident Created:</b> {qarid}</p>
    <p><b>Captured / Reported By:</b> {capturedBy}</p>
    <p><b>Investigated By:</b> {investigator}</p>
    <p><b>Date Captured:</b> {dateCaptured}</p>
    <p><b>Date Reported:</b> {dateReported}</p>
    <p><b>Description:</b><br>{description?.Replace(Environment.NewLine, "<br>")}</p>
</body>
</html>";

                    //await _emailService.SendIncidentNotificationEmail(investigatorEmail, subject, body);
                    var ccList = new List<string> { "zain@xperts.co.za", "carli@xperts.co.za" };
                    var bccList = new List<string> { "zain.slamdien@gmail.com" };

                    await _emailService.SendIncidentNotificationEmail(investigatorEmail, subject, body, ccList, bccList);

                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error sending incident email: " + ex.Message);
            }

            return RedirectToAction("EditIncident", "Form", new { id = qarid, inserthospitalid = model.inserthospitalid });
        }











        //[HttpPost]
        //public async Task<IActionResult> UploadAddendum(IFormFile addendumFile, string addendumType, string criteria)
        //{
        //    try
        //    {
        //        string qaridToUse = string.IsNullOrWhiteSpace(qarid) ? "0" : qarid;

        //        if (addendumFile == null || addendumFile.Length == 0 || string.IsNullOrEmpty(addendumType) || string.IsNullOrEmpty(criteria))
        //        {
        //            return BadRequest("Missing file or required metadata.");
        //        }

        //        var uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
        //        if (!Directory.Exists(uploadPath))
        //        {
        //            Directory.CreateDirectory(uploadPath);
        //        }

        //        var uniqueFileName = $"{Guid.NewGuid()}_{Path.GetFileName(addendumFile.FileName)}";
        //        var filePath = Path.Combine(uploadPath, uniqueFileName);

        //        using (var stream = new FileStream(filePath, FileMode.Create))
        //        {
        //            await addendumFile.CopyToAsync(stream);
        //        }

        //        using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
        //        await conn.OpenAsync();

        //        string insertSql = @"
        //    INSERT INTO incidentattachments 
        //    (attachtime, attachment, active, qarid, attachdate,foldername, addendums, criteria)
        //    VALUES (@attchatime, @attachment, 'Y', @qarid, @attachdate,@foldername, @addendums, @criteria)";

        //        using var cmd = new NpgsqlCommand(insertSql, conn);
        //        cmd.Parameters.AddWithValue("attchatime", DateTime.Now);
        //        cmd.Parameters.AddWithValue("attachment", uniqueFileName);
        //        //cmd.Parameters.AddWithValue("qarid", 0); // temp ID
        //        cmd.Parameters.AddWithValue("@qarid", qaridToUse);
        //        cmd.Parameters.AddWithValue("attachdate", DateTime.Today);
        //        cmd.Parameters.AddWithValue("addendums", addendumType);
        //        cmd.Parameters.AddWithValue("criteria", criteria);
        //        cmd.Parameters.AddWithValue("foldername", Path.GetFileName(addendumFile.FileName));

        //        await cmd.ExecuteNonQueryAsync();

        //        return Ok(new
        //        {
        //            message = "File uploaded successfully.",
        //            attachment = uniqueFileName,

        //        });
        //    }
        //    catch (Exception ex)
        //    {
        //        return StatusCode(500, $"Internal server error: {ex.Message}");
        //    }
        //}

        [HttpPost]
        public async Task<IActionResult> UploadAddendum(IFormFile addendumFile, string addendumType, string criteria, string qarid)
        {
            try
            {
                string qaridToUse = string.IsNullOrWhiteSpace(qarid) ? "0" : qarid;

                if (addendumFile == null || addendumFile.Length == 0 || string.IsNullOrEmpty(addendumType) || string.IsNullOrEmpty(criteria))
                {
                    return BadRequest("Missing file or required metadata.");
                }

                var uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
                if (!Directory.Exists(uploadPath))
                {
                    Directory.CreateDirectory(uploadPath);
                }

                var uniqueFileName = $"{Guid.NewGuid()}_{Path.GetFileName(addendumFile.FileName)}";
                var filePath = Path.Combine(uploadPath, uniqueFileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await addendumFile.CopyToAsync(stream);
                }

                using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                await conn.OpenAsync();

                string insertSql = @"
            INSERT INTO incidentattachments 
            (attachtime, attachment, active, qarid, attachdate, foldername, addendums, criteria)
            VALUES (@attachtime, @attachment, 'Y', @qarid, @attachdate, @foldername, @addendums, @criteria)";

                using var cmd = new NpgsqlCommand(insertSql, conn);
                cmd.Parameters.AddWithValue("attachtime", DateTime.Now);
                cmd.Parameters.AddWithValue("attachment", uniqueFileName);
                cmd.Parameters.AddWithValue("qarid", qaridToUse);
                cmd.Parameters.AddWithValue("attachdate", DateTime.Today);
                cmd.Parameters.AddWithValue("addendums", addendumType);
                cmd.Parameters.AddWithValue("criteria", criteria);
                cmd.Parameters.AddWithValue("foldername", Path.GetFileName(addendumFile.FileName));

                await cmd.ExecuteNonQueryAsync();

                return Ok(new
                {
                    message = "File uploaded successfully.",
                    attachment = uniqueFileName
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }


        [HttpPost]
        public async Task<IActionResult> DeleteAddendum([FromForm] string attachment)
        {
            if (string.IsNullOrWhiteSpace(attachment))
                return BadRequest("Missing attachment filename.");

            using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            await conn.OpenAsync();

            string sql = @"UPDATE incidentattachments 
                   SET active = 'N' 
                   WHERE attachment = @attachment";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("attachment", attachment);
            var rows = await cmd.ExecuteNonQueryAsync();

            if (rows == 0)
                return NotFound("No matching record found.");

            return Ok("File deleted.");
        }
        //ADDENDUMS END





        //NEW INCIDENT EDIT
        [HttpGet]
        public async Task<IActionResult> EditIncident(string? id, string? inserthospitalid)
        {

            if (string.IsNullOrEmpty(id))
                return NotFound();

            var model = new FormModel();

            using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            await conn.OpenAsync();

            // 1. Load main incident
            string sql = @"SELECT * FROM tblincident WHERE qarid = @qarid LIMIT 1";
            using (var cmd = new NpgsqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@qarid", id);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    model.qarid = reader["qarid"]?.ToString();
                    model.affectedward = reader["affectedward"]?.ToString();
                    model.incidentcriteria = reader["incidentcriteria"]?.ToString();
                    model.incidentcriteriasub = reader["incidentcriteriasub"]?.ToString();
                    model.CapturedByLoginName = reader["requester"]?.ToString();
                    model.CapturedByEmail = reader["requesteremail"]?.ToString();
                    model.priority = reader["priority"]?.ToString();
                    model.CapturedByTitle = reader["titles"]?.ToString();
                    model.invesitgatedby = reader["invesitgatedby"]?.ToString();
                    model.assignedcat = reader["assignedcat"]?.ToString();
                    model.assignedstaff = reader["assignedstaff"]?.ToString();
                    model.incidentdate = reader["incidentdate"] as DateTime?;
                    model.incidenttime = reader["incidenttime"]?.ToString();
                    model.datereported = reader["datereported"] as DateTime?;
                    model.datecaptured = reader["datecaptured"] as DateTime?;
                    model.summary = reader["summary"]?.ToString();
                    model.description = reader["description"]?.ToString();
                    model.timecaptured = reader["timecaptured"]?.ToString();
                    model.active = reader["active"]?.ToString();
                    model.status = reader["status"]?.ToString();
                    model.hospitalid = reader["hospitalid"]?.ToString();
                    model.CapturedByName = reader["username"]?.ToString();
                    model.CapturedBySurname = reader["surname"]?.ToString();
                    model.onholddescdate = reader["onholddescdate"] as DateTime?;
                    model.closeddescdate = reader["closeddescdate"] as DateTime?;
                    model.onholddesctime = reader["onholddesctime"]?.ToString();
                    model.closeddesctime = reader["closeddesctime"]?.ToString();
                    model.onholddesc = reader["onholddesc"]?.ToString();
                    model.closeddesc = reader["closeddesc"]?.ToString();
                    model.pte = reader["pte"]?.ToString();
                    model.ptenumber = reader["ptenumber"]?.ToString();
                    model.ptename = reader["ptename"]?.ToString();
                    model.ptesurname = reader["ptesurname"]?.ToString();
                    model.ptetitle = reader["ptetitle"]?.ToString();
                    model.correctaction = reader["correctaction"]?.ToString();
                    model.correctactiontime = reader["correctactiontime"]?.ToString();
                    model.preventaction = reader["preventaction"]?.ToString();
                    model.preventactiontime = reader["preventactiontime"]?.ToString();
                    model.preventactiondate = reader["preventactiondate"] as DateTime?;
                    model.correctactiondate = reader["correctactiondate"] as DateTime?;
                    model.investigation = reader["investigation"]?.ToString();
                    model.summary2 = reader["summary2"]?.ToString();
                    model.medrelatedtotal = reader["medrelatedtotal"]?.ToString();
                    model.CapturedbyDpt = reader["reportedbydepartment"]?.ToString();
                    model.acquired = reader["acquired"]?.ToString();
                    model.incidenttype = reader["incidenttype"]?.ToString();
                    model.inctypescat1 = reader["inctypescat1"]?.ToString();
                    model.inctypescat2 = reader["inctypescat2"]?.ToString();
                    model.incidentarea = reader["incidentarea"]?.ToString();
                    model.incidentareanight = reader["incidentareanight"]?.ToString();
                }
            }

            // 2. Load attachments (AFTER incident reader is closed)
            model.Attachments = new List<IncidentAttachment>();

            string attachSql = @"
        SELECT attachment, foldername, addendums
        FROM incidentattachments
        WHERE qarid = @qarid AND active = 'Y' AND criteria = 'Attachments'";

            using (var attachCmd = new NpgsqlCommand(attachSql, conn))
            {
                attachCmd.Parameters.AddWithValue("@qarid", id);

                using var attachReader = await attachCmd.ExecuteReaderAsync();
                while (await attachReader.ReadAsync())
                {
                    model.Attachments.Add(new IncidentAttachment
                    {
                        Attachment = attachReader["attachment"]?.ToString() ?? "",
                        Foldername = attachReader["foldername"]?.ToString() ?? "",
                        Addendums = attachReader["addendums"]?.ToString() ?? ""
                    });
                }
            }



            var loginhospitalid = HttpContext.Session.GetString("loginhospitalid");
            model.inserthospitalid = loginhospitalid;

            if (!string.IsNullOrEmpty(loginhospitalid))
            {
                //using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                //await conn.OpenAsync();

                using var cmd = new NpgsqlCommand("SELECT hospital, abbr FROM tblhospitals WHERE hospitalid = @id", conn);
                cmd.Parameters.AddWithValue("id", loginhospitalid);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    model.HospitalName = reader["hospital"]?.ToString();
                    model.HospitalAbbr = reader["abbr"]?.ToString();
                }
            }


            // 3. Extra setup
            //await GetInvestigators(int.Parse(model.hospitalid));

            //model.Investigators = await GetInvestigators(int.Parse(model.hospitalid));
            //model.invesitgatedby = "Zain Slamdien";

            model.Investigators = await GetInvestigators(int.Parse(model.hospitalid));



            await PopulateDropdowns(model);

            model.AccessLevel = HttpContext.Session.GetString("accessLevel");
            model.inserthospitalid = inserthospitalid;
            return View("IncidentEdit", model);
        }

        

        [HttpPost]
        public async Task<IActionResult> EditIncident(FormModel model)
        {
            //if (!ModelState.IsValid)
            //{
           //     await PopulateDropdowns(model);
            //    return View("IncidentEdit", model);
           // }

            using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            await conn.OpenAsync();



            // 2. Log the closure or hold
            if (model.status == "closed" || model.status == "hold")
            {
                string actionType = model.status == "closed" ? "closed" : "hold";
                string notes = $"Status set to {actionType}";

                // Prevent duplicate log entries (optional, in case user reloads)
                var checkLogCmd = new NpgsqlCommand(@"
        SELECT COUNT(*) FROM tblincidentlog 
        WHERE qarid = @qarid AND actiontype = @actiontype", conn);
                checkLogCmd.Parameters.AddWithValue("qarid", model.qarid);
                checkLogCmd.Parameters.AddWithValue("actiontype", actionType);
                var count = Convert.ToInt32(await checkLogCmd.ExecuteScalarAsync());

                if (count == 0)
                {
                    var logCmd = new NpgsqlCommand(@"
            INSERT INTO tblincidentlog (qarid, hospitalid, actiontype, actionby, actiondate, notes)
            VALUES (@qarid, @hospitalid, @actiontype, @user, NOW(), @notes)", conn);

                    logCmd.Parameters.AddWithValue("qarid", model.qarid);
                    logCmd.Parameters.AddWithValue("hospitalid", model.hospitalid ?? "unknown");
                    logCmd.Parameters.AddWithValue("actiontype", actionType);
                    logCmd.Parameters.AddWithValue("user", HttpContext.Session.GetString("username") ?? "unknown");
                    logCmd.Parameters.AddWithValue("notes", notes);

                    await logCmd.ExecuteNonQueryAsync();
                }
            }














            string sql = @"UPDATE tblincident SET
    affectedward = @affectedward,
    incidentarea = @incidentarea,
    incidentcriteria = @incidentcriteria,
    incidentcriteriasub = @incidentcriteriasub,
    requester = @requester,
    requesteremail = @requesteremail,
    priority = @priority,
    titles = @titles,
    reportedby = @reportedby,
    invesitgatedby = @invesitgatedby,
    assignedcat = @assignedcat,
    assignedstaff = @assignedstaff,
    incidentdate = @incidentdate,
    incidenttime = @incidenttime,
    datereported = @datereported,
    datecaptured = @datecaptured,
    summary = @summary,
    description = @description,
    timecaptured = @timecaptured,
    active = @active,
    status = @status,
    hospitalid = @hospitalid,
    qarid = @qarid,
    username = @username,
    surname = @surname,
    onholddescdate = @onholddescdate,
    onholddesctime = @onholddesctime,
    closeddesctime = @closeddesctime,
    onholddesc = @onholddesc,
    closeddesc = @closeddesc,
    pte = @pte,
    ptenumber = @ptenumber,
    ptename = @ptename,
    ptesurname = @ptesurname,
    ptetitle = @ptetitle,
    reportedbyemail = @reportedbyemail,
    correctaction = @correctaction,
    correctactiontime = @correctactiontime,
    preventaction = @preventaction,
    preventactiontime = @preventactiontime,
    preventactiondate = @preventactiondate,
    correctactiondate = @correctactiondate,
    investigation = @investigation,
    summary2 = @summary2,
    medrelatedtotal = @medrelatedtotal,
    reportedbydepartment = @reportedbydepartment,
    incidentexpires = @incidentexpires,
    incidentareanight = @incidentareanight,
    acquired = @acquired,
    incidenttype = @incidenttype,
    inctypescat1 = @inctypescat1,
    inctypescat2 = @inctypescat2
                WHERE qarid = @qarid";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@qarid", model.qarid ?? "");
            cmd.Parameters.AddWithValue("@affectedward", model.affectedward ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@incidentarea", model.incidentarea ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@incidentcriteria", model.incidentcriteria ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@incidentcriteriasub", model.incidentcriteriasub ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@requester", model.CapturedByLoginName ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@requesteremail", model.CapturedByEmail ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@priority", model.priority ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@titles", model.CapturedByTitle ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@reportedby", model.CapturedByLoginName ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@invesitgatedby", model.invesitgatedby ?? (object)DBNull.Value);
           
            cmd.Parameters.AddWithValue("@assignedcat", model.assignedcat ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@assignedstaff", model.assignedstaff ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@incidentdate", model.incidentdate ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@incidenttime", model.incidenttime ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@datereported", model.datereported ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@datecaptured", model.datecaptured?.Date ?? DateTime.Today);
            cmd.Parameters.AddWithValue("@summary", model.summary ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@description", model.description ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@timecaptured", string.IsNullOrWhiteSpace(model.timecaptured) ? DateTime.Now.ToString("HH:mm") : model.timecaptured);
            cmd.Parameters.AddWithValue("@active", model.active ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@status", model.status ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@hospitalid", model.inserthospitalid ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@qarid", model.qarid ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@username", model.CapturedByName ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@surname", model.CapturedBySurname ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@onholddescdate", model.onholddescdate ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@closeddescdate", model.closeddescdate ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@onholddesctime", model.onholddesctime ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@closeddesctime", model.closeddesctime ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@onholddesc", model.onholddesc ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@closeddesc", model.closeddesc ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@pte", model.pte ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@ptenumber", model.ptenumber ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@ptename", model.ptename ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@ptesurname", model.ptesurname ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@ptetitle", model.ptetitle ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@reportedbyemail", model.CapturedByEmail ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@correctaction", model.correctaction ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@correctactiontime", model.correctactiontime ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@preventaction", model.preventaction ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@preventactiontime", model.preventactiontime ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@preventactiondate", model.preventactiondate ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@correctactiondate", model.correctactiondate ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@investigation", model.investigation ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@summary2", model.summary2 ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@medrelatedtotal", model.medrelatedtotal ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@reportedbydepartment", model.CapturedbyDpt ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@incidentexpires", DateTime.Now.Date.AddDays(5));
            cmd.Parameters.AddWithValue("@incidentareanight", model.incidentareanight ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@acquired", model.acquired ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@incidenttype", model.incidenttype ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@inctypescat1", model.inctypescat1 ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@inctypescat2", model.inctypescat2 ?? (object)DBNull.Value);

            await cmd.ExecuteNonQueryAsync();


            //await GetInvestigators(int.Parse(model.inserthospitalid));
            model.Investigators = await GetInvestigators(int.Parse(model.inserthospitalid));
            await PopulateDropdowns(model);
            HttpContext.Session.SetString("accessLevel", model.AccessLevel ?? "standard");
            //return View("IncidentEdit", model); // Or wherever you want to go after update
            return RedirectToAction("EditIncident", new { id = model.qarid, inserthospitalid = model.inserthospitalid });
        }
      

        //NEW INCIDENT EDIT END


        //get the addendums
        [HttpGet]
        public async Task<IActionResult> GetAddendumsByQarid(string qarid)
        {
            if (string.IsNullOrWhiteSpace(qarid))
                return BadRequest("Missing qarid");

            var addendums = new List<object>();

            using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            await conn.OpenAsync();

            string sql = @"SELECT * 
                   FROM incidentattachments 
                   WHERE qarid = @qarid AND active = 'Y' AND criteria != 'Attachments'";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@qarid", qarid);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                addendums.Add(new
                {
                    attachment = reader["attachment"]?.ToString() ?? "",
                    foldername = reader["foldername"]?.ToString() ?? "",
                    addendums = reader["addendums"]?.ToString() ?? "",
                    criteria = reader["criteria"]?.ToString() ?? ""
                });
            }

            return Json(addendums);
        }



        [HttpPost]
        public async Task<IActionResult> DeleteAddendum2([FromBody] AddendumDeleteModel model)
        {
            if (string.IsNullOrWhiteSpace(model.Attachment))
                return BadRequest("Missing attachment.");

            using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            await conn.OpenAsync();

            string sql = @"
        UPDATE incidentattachments 
        SET active = 'N' 
        WHERE attachment = @attachment";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@attachment", model.Attachment);

            var rowsAffected = await cmd.ExecuteNonQueryAsync();

            return rowsAffected > 0 ? Ok("Deleted") : NotFound("No record updated.");
        }


        public class AddendumDeleteModel
        {
            public string Attachment { get; set; }
            public string Foldername { get; set; } // optional for now
            public string Qarid { get; set; }
        }

        //get the addendums


        //upload attachments linked to form
        //[HttpPost]
        //public async Task<IActionResult> UploadBasicAttachment(IFormFile file, string qarid)
        //{
        //    if (file == null || string.IsNullOrWhiteSpace(qarid))
        //        return BadRequest("Missing file or QARID.");

        //    try
        //    {
        //        // Clean up QARID for folder name (remove dashes)
        //        string foldername = qarid.Replace("-", "");
        //        string folderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", foldername);

        //        // Ensure directory exists
        //        if (!Directory.Exists(folderPath))
        //            Directory.CreateDirectory(folderPath);

        //        // Create unique file name
        //        string uniqueFileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
        //        string fullFilePath = Path.Combine(folderPath, uniqueFileName);

        //        // Save the file
        //        using (var stream = new FileStream(fullFilePath, FileMode.Create))
        //            await file.CopyToAsync(stream);

        //        // Save record to database
        //        using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
        //        await conn.OpenAsync();

        //        string sql = @"
        //    INSERT INTO incidentattachments 
        //    (attachtime, attachment, active, qarid, attachdate, foldername, addendums, criteria)
        //    VALUES (@attachtime, @attachment, 'Y', @qarid, @attachdate, @foldername, 'Form Attachments', 'Attachments')";

        //        using var cmd = new NpgsqlCommand(sql, conn);
        //        cmd.Parameters.AddWithValue("attachtime", DateTime.Now);
        //        cmd.Parameters.AddWithValue("attachment", uniqueFileName);
        //        cmd.Parameters.AddWithValue("qarid", qarid);
        //        cmd.Parameters.AddWithValue("attachdate", DateTime.Today);
        //        cmd.Parameters.AddWithValue("foldername", foldername);

        //        await cmd.ExecuteNonQueryAsync();

        //        // Return success response
        //        return Ok(new
        //        {
        //            filename = uniqueFileName,
        //            message = "File uploaded successfully"
        //        });
        //    }
        //    catch (Exception ex)
        //    {
        //        return StatusCode(500, $"Upload failed: {ex.Message}");
        //    }
        //}
        [HttpPost]
        public async Task<IActionResult> UploadBasicAttachment(IFormFile file, string qarid)
        {
            if (file == null || string.IsNullOrWhiteSpace(qarid))
                return BadRequest("Missing file or QARID.");

            string foldername = qarid.Replace("-", "");
            string folderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", foldername);

            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);

            string originalFileName = Path.GetFileName(file.FileName);
            string uniqueFileName = $"{Guid.NewGuid()}_{originalFileName}";
            string fullFilePath = Path.Combine(folderPath, uniqueFileName);

            using (var stream = new FileStream(fullFilePath, FileMode.Create))
                await file.CopyToAsync(stream);

            using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            await conn.OpenAsync();

            string sql = @"
        INSERT INTO incidentattachments 
        (attachtime, attachment, active, qarid, attachdate, foldername, addendums, criteria)
        VALUES (@attachtime, @attachment, 'Y', @qarid, @attachdate, @foldername, @addendums, 'Attachments')";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("attachtime", DateTime.Now);
            cmd.Parameters.AddWithValue("attachment", uniqueFileName);
            cmd.Parameters.AddWithValue("qarid", qarid);
            cmd.Parameters.AddWithValue("attachdate", DateTime.Today);
            cmd.Parameters.AddWithValue("foldername", foldername);
            cmd.Parameters.AddWithValue("addendums", originalFileName); //  Save actual file name

            await cmd.ExecuteNonQueryAsync();

            return Ok(new { filename = originalFileName });
        }



        [HttpPost]
        public async Task<IActionResult> DeleteAttachment([FromForm] string attachment)
        {
            if (string.IsNullOrWhiteSpace(attachment))
                return BadRequest("Missing attachment name.");

            using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            await conn.OpenAsync();

            var sql = @"UPDATE incidentattachments SET active = 'N' WHERE attachment = @attachment";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@attachment", attachment);

            var affected = await cmd.ExecuteNonQueryAsync();

            return affected > 0 ? Ok("Deleted") : NotFound("No matching record.");
        }


        //end upload attachements

        //view incidents


        //[HttpGet]
        //public async Task<IActionResult> ViewAllIncidents()
        //{
        //    var incidents = new List<FormModel>();

        //    var accessLevel = HttpContext.Session.GetString("accessLevel");
        //    var loginHospitalId = HttpContext.Session.GetString("loginhospitalid");

        //    using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
        //    await conn.OpenAsync();

        //    string sql;
        //    var cmd = new NpgsqlCommand();

        //    if (accessLevel == "admin")
        //    {
        //                sql = @"SELECT i.*, h.hospital as hospitalname 
        //                FROM tblincident i 
        //                INNER JOIN tblhospitals h ON i.hospitalid = h.hospitalid 
        //                WHERE i.active = 'Y' 
        //                ORDER BY i.id DESC 
        //                LIMIT 200";
        //                cmd = new NpgsqlCommand(sql, conn);

        //            }
        //            else
        //    {
        //                sql = @"SELECT i.*, h.hospital as hospitalname 
        //            FROM tblincident i 
        //            INNER JOIN tblhospitals h ON i.hospitalid = h.hospitalid 
        //            WHERE i.active = 'Y' 
        //            AND h.hospitalid = @hospitalid
        //            ORDER BY i.id DESC 
        //            LIMIT 200";
        //                cmd = new NpgsqlCommand(sql, conn);
        //                cmd.Parameters.AddWithValue("hospitalid", loginHospitalId);

        //            }

        //            using var reader = await cmd.ExecuteReaderAsync();
        //    while (await reader.ReadAsync())
        //    {
        //        incidents.Add(new FormModel
        //        {
        //            qarid = reader["qarid"]?.ToString(),
        //            incidentdate = reader["incidentdate"] as DateTime?,
        //            summary = reader["summary"]?.ToString(),
        //            hospitalid = reader["hospitalid"]?.ToString(),
        //            status = reader["status"]?.ToString(),
        //            affectedward = reader["affectedward"]?.ToString(),
        //            HospitalName = reader["hospitalname"]?.ToString(),
        //            pte = reader["pte"]?.ToString(),
        //            requester = reader["requester"]?.ToString(),
        //        });
        //    }

        //    return View("ViewAllIncidents", incidents);
        //}


        [HttpGet]
        public async Task<IActionResult> ViewAllIncidents(string? selectedHospitalId, string? status)
        {
            var incidents = new List<FormModel>();
            var hospitalList = new List<SelectListItem>();

            var accessLevel = HttpContext.Session.GetString("accessLevel");
            var loginHospitalId = HttpContext.Session.GetString("loginhospitalid");
            var loginname = HttpContext.Session.GetString("loginname"); //  grab login name

            using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            await conn.OpenAsync();



            //count incidents
            ViewBag.OpenCount = 0;
            ViewBag.ClosedCount = 0;
            ViewBag.HoldCount = 0;

            // Build count SQL
            string countSql = @"
    SELECT status, COUNT(*) AS count 
    FROM tblincident 
    WHERE active = 'Y'";

            var countCmd = new NpgsqlCommand();
            countCmd.Connection = conn;

            if (accessLevel == "admin")
            {
                if (!string.IsNullOrEmpty(selectedHospitalId))
                {
                    countSql += " AND hospitalid = @selectedHospitalId";
                    countCmd.Parameters.AddWithValue("selectedHospitalId", selectedHospitalId);
                }
            }
            else
            {
                countSql += " AND hospitalid = @hospitalid AND username = @username";
                countCmd.Parameters.AddWithValue("hospitalid", loginHospitalId);
                countCmd.Parameters.AddWithValue("username", loginname);
            }

            countSql += " GROUP BY status";
            countCmd.CommandText = countSql;

            using (var countReader = await countCmd.ExecuteReaderAsync())
            {
                while (await countReader.ReadAsync())
                {
                    string stat = countReader["status"]?.ToString()?.ToLower() ?? "";
                    int cnt = Convert.ToInt32(countReader["count"]);

                    if (stat == "open") ViewBag.OpenCount = cnt;
                    else if (stat == "closed") ViewBag.ClosedCount = cnt;
                    else if (stat == "hold") ViewBag.HoldCount = cnt;
                }
            }

            //count incidents











            // Admin gets hospital dropdown list
            if (accessLevel == "admin")
            {
                using (var hospitalCmd = new NpgsqlCommand("SELECT hospitalid, hospital FROM tblhospitals WHERE active = 'Y' ORDER BY hospitalid", conn))
                using (var reader = await hospitalCmd.ExecuteReaderAsync())
                {
                    hospitalList.Add(new SelectListItem { Text = "All Hospitals", Value = "" }); // for all
                    while (await reader.ReadAsync())
                    {
                        hospitalList.Add(new SelectListItem
                        {
                            Text = reader["hospital"]?.ToString(),
                            Value = reader["hospitalid"]?.ToString(),
                            Selected = selectedHospitalId == reader["hospitalid"]?.ToString()
                        });
                    }
                }
            }

            // Build SQL
            string sql = @"
        SELECT i.*, h.hospital AS hospitalname 
        FROM tblincident i 
        INNER JOIN tblhospitals h ON i.hospitalid = h.hospitalid 
        WHERE i.active = 'Y'";

            var cmd = new NpgsqlCommand();
            cmd.Connection = conn;

            if (accessLevel == "admin")
            {
                if (!string.IsNullOrEmpty(selectedHospitalId))
                {
                    sql += " AND i.hospitalid = @selectedHospitalId";
                    cmd.Parameters.AddWithValue("selectedHospitalId", selectedHospitalId);
                }
            }
            else
            {
                sql += " AND i.hospitalid = @hospitalid AND i.username = @username";
                cmd.Parameters.AddWithValue("hospitalid", loginHospitalId);
                cmd.Parameters.AddWithValue("username", loginname);
            }

            if (!string.IsNullOrEmpty(status) && status != "all")
            {
                sql += " AND i.status = @status";
                cmd.Parameters.AddWithValue("status", status);
            }

            sql += " ORDER BY i.id DESC LIMIT 200";
            cmd.CommandText = sql;

            // Execute and read results
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    incidents.Add(new FormModel
                    {
                        qarid = reader["qarid"]?.ToString(),
                        incidentdate = reader["incidentdate"] as DateTime?,
                        summary = reader["summary"]?.ToString(),
                        hospitalid = reader["hospitalid"]?.ToString(),
                        status = reader["status"]?.ToString(),
                        affectedward = reader["affectedward"]?.ToString(),
                        HospitalName = reader["hospitalname"]?.ToString(),
                        pte = reader["pte"]?.ToString(),
                        requester = reader["requester"]?.ToString(),
                    });
                }
            }

            ViewBag.HospitalList = hospitalList;
            ViewBag.SelectedHospitalId = selectedHospitalId;
            ViewBag.SelectedStatus = status;
           
            return View("ViewAllIncidents", incidents);
        }




        //view incidents


        [HttpPost]
        public async Task<IActionResult> DeleteIncident(string qarid)
        {
            if (string.IsNullOrWhiteSpace(qarid))
                return BadRequest("Invalid QARID.");

            using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            await conn.OpenAsync();

            string hospitalid = null;
            // Log deletion
            var logCmd = new NpgsqlCommand(@"
            INSERT INTO tblincidentlog (qarid, hospitalid, actiontype, actionby, actiondate)
             VALUES (@qarid, @hospitalid, 'Deleted', @user, NOW())", conn);
            logCmd.Parameters.AddWithValue("qarid", qarid);
            logCmd.Parameters.AddWithValue("hospitalid", hospitalid ?? "unknown");
            logCmd.Parameters.AddWithValue("user", HttpContext.Session.GetString("username") ?? "unknown");
            await logCmd.ExecuteNonQueryAsync();





            string sql = "UPDATE tblincident SET active = 'N' WHERE qarid = @qarid";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@qarid", qarid);

            await cmd.ExecuteNonQueryAsync();

            // Redirect or return success
            return RedirectToAction("ViewAllIncidents"); // or wherever you list incidents
        }

    }
}