using Microsoft.AspNetCore.Mvc;
using Mimsv2.Models;
using Npgsql;

namespace Mimsv2.Controllers
{
    public class RiskController : Controller
    {
        private readonly IConfiguration _configuration;

        public RiskController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var risks = new List<RiskModel>();
            var hospitalId = HttpContext.Session.GetString("inserthospitalid");
           

            using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            await conn.OpenAsync();

            string sql = @"SELECT * FROM tblriskmanagement 
                       WHERE inserthospitalid = @hospitalId AND active = 'Y'
                       ORDER BY dateidentified DESC";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("hospitalId", hospitalId ?? "");

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                risks.Add(new RiskModel
                {
                    Id = reader.GetInt32(reader.GetOrdinal("id")),
                    qarid = reader["qarid"]?.ToString(),
                    RiskTitle = reader["risktitle"]?.ToString(),
                    RootCause = reader["rootcause"]?.ToString(),
                    NearRootCause = reader["nearrootcause"]?.ToString(),
                    RiskType = reader["risktype"]?.ToString(),
                    RiskLevel = reader["risklevel"]?.ToString(),
                    PreventativeAction = reader["preventativeaction"]?.ToString(),
                    ResponsiblePerson = reader["responsibleperson"]?.ToString(),
                    TargetDate = reader.GetDateTime(reader.GetOrdinal("targetdate")),
                    Status = reader["status"]?.ToString(),
                    DateIdentified = reader.GetDateTime(reader.GetOrdinal("dateidentified")),
                    inserthospitalid = reader["inserthospitalid"]?.ToString()
                });
            }

            var model = new RiskModel();
            model.inserthospitalid = HttpContext.Session.GetString("loginhospitalid");

            return View(risks);
        }

               [HttpPost]
        public async Task<IActionResult> AddRisk(RiskModel model)
        {
            //if (!ModelState.IsValid)
            // return RedirectToAction("Index");

           

            model.DateIdentified = DateTime.Now;
             //model.inserthospitalid = HttpContext.Session.GetString("inserthospitalid");
            model.inserthospitalid = HttpContext.Session.GetString("loginhospitalid");

            using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            await conn.OpenAsync();

            string sql = @"INSERT INTO tblriskmanagement 
            (qarid, risktitle, rootcause, nearrootcause, risktype, risklevel,
             preventativeaction, responsibleperson, targetdate, status, dateidentified, inserthospitalid, active, datecaptured)
            VALUES 
            (@qarid, @risktitle, @rootcause, @nearrootcause, @risktype, @risklevel,
             @preventativeaction, @responsibleperson, @targetdate, @status, @dateidentified, @inserthospitalid, 'Y', @datecaptured)";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("qarid", (object?)model.qarid ?? DBNull.Value);
            cmd.Parameters.AddWithValue("risktitle", model.RiskTitle ?? "");
            cmd.Parameters.AddWithValue("rootcause", model.RootCause ?? "");
            cmd.Parameters.AddWithValue("nearrootcause", model.NearRootCause ?? "");
            cmd.Parameters.AddWithValue("risktype", model.RiskType ?? "");
            cmd.Parameters.AddWithValue("risklevel", model.RiskLevel ?? "");
            cmd.Parameters.AddWithValue("preventativeaction", model.PreventativeAction ?? "");
            cmd.Parameters.AddWithValue("responsibleperson", model.ResponsiblePerson ?? "");
            cmd.Parameters.AddWithValue("targetdate", model.TargetDate);
            cmd.Parameters.AddWithValue("status", model.Status ?? "Open");
            cmd.Parameters.AddWithValue("dateidentified", model.DateIdentified);
            cmd.Parameters.AddWithValue("inserthospitalid", model.inserthospitalid ?? "");
            cmd.Parameters.AddWithValue("@datecaptured", DateTime.Today);

            await cmd.ExecuteNonQueryAsync();
            return RedirectToAction("Index");
        }



        [HttpPost]
        public async Task<IActionResult> DeleteRisk(int Id)
        {
            //if (string.IsNullOrWhiteSpace(Id))
               // return BadRequest("Invalid Id.");

            using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            await conn.OpenAsync();

            string sql = "UPDATE tblriskmanagement SET active = 'N' WHERE Id = @Id";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Id", Id);

            await cmd.ExecuteNonQueryAsync();

            return RedirectToAction("Index");
        }
    }

}
