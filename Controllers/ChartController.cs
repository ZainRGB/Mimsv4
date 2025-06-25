using Microsoft.AspNetCore.Mvc;
using Npgsql;
using Mimsv2.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Mimsv2.Controllers
{
    public class ChartController : Controller
    {
        private readonly IConfiguration _configuration;

        public ChartController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<IActionResult> AdminPage(string? inserthospitalid)
        {
            var model = new ChartModel
            {
                SelectedHospitalId = inserthospitalid,
                Hospitals = new List<SelectListItem>()
            };

            model.inserthospitalid = inserthospitalid;

            using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            await conn.OpenAsync();

            // Load hospitals for dropdown
            var hospitalCmd = new NpgsqlCommand("SELECT hospitalid, hospital FROM tblhospitals ORDER BY hospital", conn);
            using var readerHosp = await hospitalCmd.ExecuteReaderAsync();
            while (await readerHosp.ReadAsync())
            {
                model.Hospitals.Add(new SelectListItem
                {
                    Value = readerHosp["hospitalid"].ToString(),
                    Text = readerHosp["hospital"].ToString(),
                    Selected = (readerHosp["hospitalid"].ToString() == inserthospitalid)
                });
            }
            readerHosp.Close();

            // Build query
            string sql = @"
        SELECT
            COUNT(*) FILTER (
                WHERE status = 'open' AND active = 'Y' {0} 
            ) AS open_count,
            COUNT(*) FILTER (
                WHERE status = 'hold' AND active = 'Y' {0}
            ) AS onhold_count,
            COUNT(*) FILTER (
                WHERE status = 'closed' AND active = 'Y' {0}
            ) AS closed_count,
            COUNT(*) FILTER (
                WHERE status = 'open' AND active = 'Y'
                AND datecaptured <= CURRENT_DATE - INTERVAL '5 days'
                {0}
            ) AS open_5days,
            COUNT(*) FILTER (
                WHERE status = 'open' AND active = 'Y'
                AND datecaptured <= CURRENT_DATE - INTERVAL '10 days'
                {0}
            ) AS open_10days,
            COUNT(*) FILTER (
                WHERE active = 'Y' {0}
            ) AS total_incidents
        FROM tblincident;";

            string hospitalClause = string.IsNullOrEmpty(inserthospitalid) ? "" : $" AND hospitalid = @inserthospitalid";
            sql = string.Format(sql, hospitalClause);

            using var cmd = new NpgsqlCommand(sql, conn);
            if (!string.IsNullOrEmpty(inserthospitalid))
            {
                cmd.Parameters.AddWithValue("@inserthospitalid", inserthospitalid);

                var nameCmd = new NpgsqlCommand("SELECT hospital FROM tblhospitals WHERE hospitalid = @id", conn);
                nameCmd.Parameters.AddWithValue("@id", inserthospitalid);
                var result = await nameCmd.ExecuteScalarAsync();
                model.HospitalName = result?.ToString() ?? inserthospitalid;

            }

            using var reader = await cmd.ExecuteReaderAsync();
            if (reader.Read())
            {
                model.Open = Convert.ToInt32(reader["open_count"]);
                model.OnHold = Convert.ToInt32(reader["onhold_count"]);
                model.Closed = Convert.ToInt32(reader["closed_count"]);
                model.OpenOver5Days = Convert.ToInt32(reader["open_5days"]);
                model.OpenOver10Days = Convert.ToInt32(reader["open_10days"]);
                model.Total = Convert.ToInt32(reader["total_incidents"]);
            }

            return View("AdminPage", model);
        }


        public IActionResult MainPage()
        {
            // placeholder
            return View();
        }

        public IActionResult LocalPage()
        {
            // placeholder
            return View();
        }

        //MONTHLY COUNT
        [HttpGet]
        public async Task<IActionResult> GetMonthlyData(string? hospitalid)
        {
            Console.WriteLine(">>> Incoming hospitalid: " + hospitalid);

            var data = new List<MonthlyIncidentData>();

            using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            await conn.OpenAsync();

            string sql = @"
        SELECT 
            TO_CHAR(datecaptured, 'Mon') AS month,
            EXTRACT(MONTH FROM datecaptured) AS month_number,
            COUNT(*) AS count
        FROM tblincident
        WHERE active = 'Y'
          AND EXTRACT(YEAR FROM datecaptured) = EXTRACT(YEAR FROM CURRENT_DATE)
    ";

            if (!string.IsNullOrEmpty(hospitalid))
            {
                sql += " AND hospitalid = @hospitalid";
            }

            sql += " GROUP BY month, month_number ORDER BY month_number;";

            using var cmd = new NpgsqlCommand(sql, conn);
            if (!string.IsNullOrEmpty(hospitalid))
            {
                cmd.Parameters.AddWithValue("@hospitalid", hospitalid);
            }

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                data.Add(new MonthlyIncidentData
                {
                    Month = reader["month"].ToString()!,
                    Count = Convert.ToInt32(reader["count"])
                });
            }

            return Json(data);
        }

        //COUNT PTE
        [HttpGet]
        public async Task<IActionResult> GetPteDistribution(string? hospitalid)
        {
            var results = new List<object>();

            using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            await conn.OpenAsync();

            string sql = @"
        SELECT pte, COUNT(*) AS count
        FROM tblincident
        WHERE active = 'Y'
          AND EXTRACT(YEAR FROM datecaptured) = 2025
          " + (string.IsNullOrEmpty(hospitalid) ? "" : " AND hospitalid = @hospitalid") + @"
        GROUP BY pte
        ORDER BY count DESC;
    ";

            using var cmd = new NpgsqlCommand(sql, conn);
            if (!string.IsNullOrEmpty(hospitalid))
            {
                cmd.Parameters.AddWithValue("@hospitalid", hospitalid);
            }

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(new
                {
                    label = reader["pte"]?.ToString() ?? "Unknown",
                    count = Convert.ToInt32(reader["count"])
                });
            }

            return Json(results);
        }

        //COUNT PTE end

        //COUNT incidentype
        [HttpGet]
        public async Task<IActionResult> GetIncidentTypeDistribution(string? hospitalid)
        {
            var results = new List<object>();

            using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            await conn.OpenAsync();

            string sql = @"
        SELECT incidenttype, COUNT(*) AS count
        FROM tblincident
        WHERE active = 'Y'
          AND EXTRACT(YEAR FROM datecaptured) = 2025
          " + (string.IsNullOrEmpty(hospitalid) ? "" : " AND hospitalid = @hospitalid") + @"
        GROUP BY incidenttype
        ORDER BY count DESC;
    ";

            using var cmd = new NpgsqlCommand(sql, conn);
            if (!string.IsNullOrEmpty(hospitalid))
            {
                cmd.Parameters.AddWithValue("@hospitalid", hospitalid);
            }

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(new
                {
                    label = reader["incidenttype"]?.ToString() ?? "Unknown",
                    count = Convert.ToInt32(reader["count"])
                });
            }

            return Json(results);
        }

        //COUNT incidenttype end




    }
}
