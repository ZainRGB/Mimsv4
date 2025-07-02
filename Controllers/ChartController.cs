using Microsoft.AspNetCore.Mvc;
using Npgsql;
using Mimsv2.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using Mimsv2.Services;
using DocumentFormat.OpenXml.Office.Word;

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

        //COUNT Affectedward
        [HttpGet]
        public async Task<IActionResult> GetTopAffectedWards(string? hospitalid)
        {
            var results = new List<object>();

            using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            await conn.OpenAsync();

            string sql = @"
        SELECT affectedward, COUNT(*) AS totalincidents 
        FROM tblincident 
        WHERE active = 'Y'
          AND datecaptured >= (CURRENT_DATE - INTERVAL '6 months')
          " + (string.IsNullOrEmpty(hospitalid) ? "" : " AND hospitalid = @hospitalid") + @"
        GROUP BY affectedward
        ORDER BY totalincidents DESC 
        LIMIT 10;
    ";

            using var cmd = new NpgsqlCommand(sql, conn);
            if (!string.IsNullOrEmpty(hospitalid))
                cmd.Parameters.AddWithValue("@hospitalid", hospitalid);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(new
                {
                    ward = reader["affectedward"]?.ToString() ?? "Unknown",
                    count = Convert.ToInt32(reader["totalincidents"])
                });
            }

            return Json(results);
        }



        //COUNT Affectedward end
        //COUNT YTD All hospitals
        [HttpGet]
        public async Task<IActionResult> GetIncidentCountsPerHospital(DateTime? startDate, DateTime? endDate)
        {
            var results = new List<object>();
            var start = startDate ?? new DateTime(DateTime.Today.Year, 1, 1);
            var end = endDate ?? DateTime.Today;

            using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            await conn.OpenAsync();

            string sql = @"
        SELECT h.hospital, COUNT(i.*) AS totalincidents
        FROM tblincident i
        JOIN tblhospitals h ON i.hospitalid = h.hospitalid
        WHERE i.active = 'Y'
          AND i.datecaptured BETWEEN @startDate AND @endDate
        GROUP BY h.hospital
        ORDER BY h.hospital DESC;
    ";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@startDate", start);
            cmd.Parameters.AddWithValue("@endDate", end);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(new
                {
                    hospital = reader["hospital"].ToString(),
                    count = Convert.ToInt32(reader["totalincidents"])
                });
            }

            return Json(results);
        }

        //COUNT YTD all hospitals end




        [HttpGet]
        public async Task<IActionResult> GroupQualityIndicators(DateTime? from = null, DateTime? to = null)
        {
            var results = new List<GroupQualityIndicatorModel>();

            var startDate = from ?? DateTime.Today.AddDays(-7);
            var endDate = to ?? DateTime.Today;

            using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            await conn.OpenAsync();

            string sql = @"
            SELECT 
                h.hospital,
                CONCAT(i.incidenttype, ': ', i.description) AS summary,
                i.priority,
                i.affectedward,
                i.correctaction
            FROM tblincident i
            JOIN tblhospitals h ON h.hospitalid = i.hospitalid
            WHERE i.active = 'Y'
              AND i.datecaptured BETWEEN @startDate AND @endDate
            ORDER BY h.hospitalid, 
                     CASE WHEN i.priority ILIKE 'major' THEN 1 
                          WHEN i.priority ILIKE 'moderate' THEN 2 
                          WHEN i.priority ILIKE 'minor' THEN 3 
                          ELSE 4 END";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@startDate", startDate);
            cmd.Parameters.AddWithValue("@endDate", endDate);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(new GroupQualityIndicatorModel
                {
                    Hospital = reader["hospital"].ToString(),
                    Summary = reader["summary"].ToString(),
                    Severity = reader["priority"].ToString(),
                    Department = reader["affectedward"].ToString(),
                    CorrectiveActions = reader["correctaction"].ToString()
                });
            }

            return View(results);
        }


        //excel
        [HttpGet]
        public async Task<IActionResult> ExportQualityIndicatorsToExcel(DateTime? from = null, DateTime? to = null)
        {
            var results = new List<GroupQualityIndicatorModel>();
            var startDate = from ?? DateTime.Today.AddDays(-7);
            var endDate = to ?? DateTime.Today;

            using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            await conn.OpenAsync();

            string sql = @"
        SELECT 
            h.hospital,
            CONCAT(i.incidenttype, ': ', i.description) AS summary,
            i.priority,
            i.affectedward,
            i.correctaction
        FROM tblincident i
        JOIN tblhospitals h ON h.hospitalid = i.hospitalid
        WHERE i.active = 'Y'
          AND i.datecaptured BETWEEN @startDate AND @endDate
        ORDER BY h.hospital, 
                 CASE WHEN i.priority ILIKE 'major' THEN 1 
                      WHEN i.priority ILIKE 'moderate' THEN 2 
                      WHEN i.priority ILIKE 'minor' THEN 3 
                      ELSE 4 END";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@startDate", startDate);
            cmd.Parameters.AddWithValue("@endDate", endDate);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(new GroupQualityIndicatorModel
                {
                    Hospital = reader["hospital"].ToString(),
                    Summary = reader["summary"].ToString(),
                    Severity = reader["priority"].ToString(),
                    Department = reader["affectedward"].ToString(),
                    CorrectiveActions = reader["correctaction"].ToString()
                });
            }

            using var workbook = new ClosedXML.Excel.XLWorkbook();
            var grouped = results.GroupBy(x => x.Hospital);

            foreach (var group in grouped)
            {
                var ws = workbook.Worksheets.Add(group.Key.Length > 30 ? group.Key.Substring(0, 30) : group.Key);
                ws.Cell(1, 1).Value = "No.";
                ws.Cell(1, 2).Value = "Incident Category & Description";
                ws.Cell(1, 3).Value = "Severity";
                ws.Cell(1, 4).Value = "Department";
                ws.Cell(1, 5).Value = "Corrective Actions";

                int row = 2;
                int index = 1;
                foreach (var item in group)
                {
                    ws.Cell(row, 1).Value = index++;
                    ws.Cell(row, 2).Value = item.Summary;
                    ws.Cell(row, 3).Value = item.Severity;
                    ws.Cell(row, 4).Value = item.Department;
                    ws.Cell(row, 5).Value = item.CorrectiveActions;
                    row++;
                }

                ws.Columns().AdjustToContents();
            }

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            stream.Position = 0;

            string filename = $"Group_Quality_Indicators_{DateTime.Now:yyyyMMdd}.xlsx";
            return File(stream.ToArray(),
                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        filename);
        }




        //pdf
        [HttpGet]
        public async Task<IActionResult> ExportQualityIndicatorsToPdf(
     [FromServices] GroupQualityIndicatorsPdfService pdfService,
     DateTime? from = null,
     DateTime? to = null)
        {
            var results = new List<GroupQualityIndicatorModel>();
            var startDate = from ?? DateTime.Today.AddDays(-7);
            var endDate = to ?? DateTime.Today;

            using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            await conn.OpenAsync();

            string sql = @"
        SELECT 
            h.hospital,
            CONCAT(i.incidenttype, ': ', i.description) AS summary,
            i.priority,
            i.affectedward,
            i.correctaction
        FROM tblincident i
        JOIN tblhospitals h ON h.hospitalid = i.hospitalid
        WHERE i.active = 'Y'
          AND i.datecaptured BETWEEN @startDate AND @endDate
        ORDER BY h.hospital, 
                 CASE 
                     WHEN i.priority ILIKE 'major' THEN 1 
                     WHEN i.priority ILIKE 'moderate' THEN 2 
                     WHEN i.priority ILIKE 'minor' THEN 3 
                     ELSE 4 END";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@startDate", startDate);
            cmd.Parameters.AddWithValue("@endDate", endDate);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(new GroupQualityIndicatorModel
                {
                    Hospital = reader["hospital"]?.ToString() ?? "",
                    Summary = reader["summary"]?.ToString() ?? "",
                    Severity = reader["priority"]?.ToString() ?? "",
                    Department = reader["affectedward"]?.ToString() ?? "",
                    CorrectiveActions = reader["correctaction"]?.ToString() ?? ""
                });
            }

            var pdfBytes = pdfService.Generate(results);

            return File(pdfBytes, "application/pdf", $"Group_Quality_Indicators_{DateTime.Now:yyyyMMdd}.pdf");
        }



        //Incident trends
        [HttpGet]
        public async Task<IActionResult> IncidentTrendTable(int? year)
        {
            int selectedYear = year ?? DateTime.Now.Year;
            var model = new List<IncidentTrendSummaryModel>();

            using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            await conn.OpenAsync();

            string sql = @"
SELECT
  	i.pte,
    COALESCE(NULLIF(i.incidentcriteria, ''), i.incidenttype) AS incidenttype,
    i.incidentcriteriasub AS inctypescat1,
    i.inctypescat2,
    COUNT(*) FILTER (WHERE EXTRACT(MONTH FROM i.incidentdate) = 1) AS Jan,
    COUNT(*) FILTER (WHERE EXTRACT(MONTH FROM i.incidentdate) = 2) AS Feb,
    COUNT(*) FILTER (WHERE EXTRACT(MONTH FROM i.incidentdate) = 3) AS Mar,
    COUNT(*) FILTER (WHERE EXTRACT(MONTH FROM i.incidentdate) IN (1,2,3)) AS Q1,
    COUNT(*) FILTER (WHERE EXTRACT(MONTH FROM i.incidentdate) = 4) AS Apr,
    COUNT(*) FILTER (WHERE EXTRACT(MONTH FROM i.incidentdate) = 5) AS May,
    COUNT(*) FILTER (WHERE EXTRACT(MONTH FROM i.incidentdate) = 6) AS Jun,
    COUNT(*) FILTER (WHERE EXTRACT(MONTH FROM i.incidentdate) IN (4,5,6)) AS Q2,
    COUNT(*) FILTER (WHERE EXTRACT(MONTH FROM i.incidentdate) = 7) AS Jul,
    COUNT(*) FILTER (WHERE EXTRACT(MONTH FROM i.incidentdate) = 8) AS Aug,
    COUNT(*) FILTER (WHERE EXTRACT(MONTH FROM i.incidentdate) = 9) AS Sep,
    COUNT(*) FILTER (WHERE EXTRACT(MONTH FROM i.incidentdate) IN (7,8,9)) AS Q3,
    COUNT(*) FILTER (WHERE EXTRACT(MONTH FROM i.incidentdate) = 10) AS Oct,
    COUNT(*) FILTER (WHERE EXTRACT(MONTH FROM i.incidentdate) = 11) AS Nov,
    COUNT(*) FILTER (WHERE EXTRACT(MONTH FROM i.incidentdate) = 12) AS Dec,
    COUNT(*) FILTER (WHERE EXTRACT(MONTH FROM i.incidentdate) IN (10,11,12)) AS Q4,
    COUNT(*) AS total
FROM tblincident i
WHERE i.active = 'Y'
  AND EXTRACT(YEAR FROM i.incidentdate) = @year
GROUP BY
i.pte,
    COALESCE(NULLIF(i.incidentcriteria, ''), i.incidenttype),
    i.incidentcriteriasub,
    i.inctypescat2
ORDER BY total DESC;
    ";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@year", selectedYear);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                int total = Convert.ToInt32(reader["total"]);
                model.Add(new IncidentTrendSummaryModel
                {
                    PTE = reader["pte"].ToString(),
                    IncidentType = reader["incidenttype"].ToString(),
                    IncTypesCat1 = reader["inctypescat1"].ToString(),
                    IncTypesCat2 = reader["inctypescat2"].ToString(),
                    Jan = Convert.ToInt32(reader["Jan"]),
                    Feb = Convert.ToInt32(reader["Feb"]),
                    Mar = Convert.ToInt32(reader["Mar"]),
                    Q1 = Convert.ToInt32(reader["Q1"]),
                    Apr = Convert.ToInt32(reader["Apr"]),
                    May = Convert.ToInt32(reader["May"]),
                    Jun = Convert.ToInt32(reader["Jun"]),
                    Q2 = Convert.ToInt32(reader["Q2"]),
                    Jul = Convert.ToInt32(reader["Jul"]),
                    Aug = Convert.ToInt32(reader["Aug"]),
                    Sep = Convert.ToInt32(reader["Sep"]),
                    Q3 = Convert.ToInt32(reader["Q3"]),
                    Oct = Convert.ToInt32(reader["Oct"]),
                    Nov = Convert.ToInt32(reader["Nov"]),
                    Dec = Convert.ToInt32(reader["Dec"]),
                    Q4 = Convert.ToInt32(reader["Q4"]),
                    Total = total,
                    Goal = (int)Math.Ceiling(total * 0.60) // 10% goal
                });
            }

            ViewBag.Year = selectedYear;
            return View("IncidentTrendTable", model);
        }




        [HttpGet]
        public async Task<IActionResult> IncidentRates()
        {
            var hospitalIncidentRates = new List<IncidentRateModel>();
            var groupIncidentRates = new List<IncidentRateModel>();

            using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            await conn.OpenAsync();

            // A. Incidents per hospital + month
            var sql = @"
SELECT 
    h.hospitalid,
    h.hospital,
    COALESCE(i.incidentcriteria, 'Unspecified') AS incidentcriteria,
    EXTRACT(MONTH FROM i.incidentdate) AS month,
    COUNT(*) AS total
FROM tblincident i
JOIN tblhospitals h ON h.hospitalid = i.hospitalid
WHERE i.active = 'Y' AND h.active = 'Y' AND i.incidentdate IS NOT NULL
GROUP BY h.hospitalid, h.hospital, i.incidentcriteria, month

UNION ALL

SELECT 
    h.hospitalid,
    h.hospital,
    i.pte AS incidentcriteria,
    EXTRACT(MONTH FROM i.incidentdate) AS month,
    COUNT(DISTINCT i.pte) AS total
FROM tblincident i
JOIN tblhospitals h ON h.hospitalid = i.hospitalid
WHERE i.active = 'Y' AND h.active = 'Y' AND i.incidentdate IS NOT NULL
GROUP BY h.hospitalid, h.hospital, month, i.pte

ORDER BY hospitalid, incidentcriteria, month;;
";

            var temp = new Dictionary<string, IncidentRateModel>();

            using (var cmd = new NpgsqlCommand(sql, conn))
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    var hospitalId = reader.GetString(0);
                    var hospitalName = reader.GetString(1);
                    var criteria = reader.IsDBNull(2) ? "Unspecified" : reader.GetString(2);
                    var month = Convert.ToInt32(reader["month"]);
                    var count = reader.GetInt32(4);

                    string key = $"{hospitalId}-{criteria}";
                    if (!temp.TryGetValue(key, out var model))
                    {
                        model = new IncidentRateModel
                        {
                            HospitalId = hospitalId,
                            HospitalName = hospitalName,
                            IncidentCriteria = criteria,
                            MonthlyCounts = new int[12]
                        };
                        temp[key] = model;
                    }

                    model.MonthlyCounts[month - 1] += count;
                }
            }

            hospitalIncidentRates = temp.Values.ToList();

            // B. Calculate % per hospital group
            foreach (var group in hospitalIncidentRates.GroupBy(x => x.HospitalId))
            {
                var total = group.Sum(x => x.TotalIncidents);
                foreach (var item in group)
                {
                    item.PercentageOfGroup = total > 0
                        ? ((double)item.TotalIncidents / total) * 100
                        : 0;
                }
            }

            // C. Group totals (All Hospitals combined)
            var groupSql = @"
        SELECT 
            COALESCE(incidentcriteria, 'Unspecified') AS incidentcriteria,
            EXTRACT(MONTH FROM incidentdate) AS month,
            COUNT(*) AS total
        FROM tblincident
        WHERE active = 'Y' AND incidentdate IS NOT NULL
        GROUP BY incidentcriteria, month
        ORDER BY incidentcriteria, month";

            var groupTemp = new Dictionary<string, IncidentRateModel>();

            using (var cmd = new NpgsqlCommand(groupSql, conn))
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    var criteria = reader.GetString(0);
                    var month = Convert.ToInt32(reader["month"]);
                    var count = reader.GetInt32(2);

                    if (!groupTemp.TryGetValue(criteria, out var model))
                    {
                        model = new IncidentRateModel
                        {
                            HospitalId = "GROUP",
                            HospitalName = "All Hospitals",
                            IncidentCriteria = criteria,
                            MonthlyCounts = new int[12]
                        };
                        groupTemp[criteria] = model;
                    }

                    model.MonthlyCounts[month - 1] += count;
                }
            }

            groupIncidentRates = groupTemp.Values.ToList();

            // D. Group percentage
            var grandTotal = groupIncidentRates.Sum(x => x.TotalIncidents);
            foreach (var item in groupIncidentRates)
            {
                item.PercentageOfGroup = grandTotal > 0
                    ? ((double)item.TotalIncidents / grandTotal) * 100
                    : 0;
            }

            // E. Pack to view
            var viewModel = new IncidentRateViewModel
            {
                HospitalIncidentRates = hospitalIncidentRates,
                GroupIncidentRates = groupIncidentRates
            };

            return View(viewModel);
        }







    }
}
