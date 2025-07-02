using Microsoft.AspNetCore.Mvc;
using Mimsv2.Models;
using MySqlConnector;
using Npgsql;
using System.Data;

public class CmController : Controller
{
    private readonly IConfiguration _configuration;

    public CmController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<IActionResult> Index(int? month, int? year)
   
        {
        var hospitalConnections = new Dictionary<string, string>
    {
        { "MG", _configuration.GetConnectionString("MySql_Connection_1") },
        { "MMP", _configuration.GetConnectionString("MySql_Connection_2") },
        { "MB", _configuration.GetConnectionString("MySql_Connection_3") },
        { "MC", _configuration.GetConnectionString("MySql_Connection_5") },
        { "MT", _configuration.GetConnectionString("MySql_Connection_6") },
        { "MRB", _configuration.GetConnectionString("MySql_Connection_7") }
    };

        var hospitalIdMap = new Dictionary<string, int>
    {
        { "MG", 1 },
        { "MMP", 2 },
        { "MB", 3 },
        { "MC", 5 },
        { "MT", 1 },
        { "MRB", 9 }
    };

        var results = new List<HospitalAdmissionModel>();
        var hospitalCounts = new List<HospitalCountModel>();

        foreach (var hospital in hospitalConnections)
        {
            int count = 0;
            int incidentCount = 0;
            double incidentRate = 0;

            try
            {
                using var conn = new MySqlConnection(hospital.Value);
                await conn.OpenAsync();

                var query = @"
                SELECT attdoc1, refdoc, ADMISSIONDATE, referencenumber, title, firstnames, surname, 
                CONCAT(LPAD(FLOOR(ADMISSIONTIME/6000/60),2,0), ':', 
                LPAD(FLOOR((ADMISSIONTIME - FLOOR(ADMISSIONTIME/6000/60)*6000*60)/6000),2,0)) AS ADMISTIMES 
                FROM patient 
                WHERE referencenumber NOT LIKE 'PRE%' 
                  AND referencenumber NOT LIKE 'NEW%'";

                var cmd = new MySqlCommand { Connection = conn };

                if (month.HasValue)
                {
                    query += " AND MONTH(ADMISSIONDATE) = @month ";
                    cmd.Parameters.AddWithValue("@month", month.Value);
                }

                if (year.HasValue)
                {
                    query += " AND YEAR(ADMISSIONDATE) = @year ";
                    cmd.Parameters.AddWithValue("@year", year.Value);
                }

                query += " ORDER BY ADMISSIONDATE";
                cmd.CommandText = query;

                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    count++;

                    results.Add(new HospitalAdmissionModel
                    {
                        HospitalName = hospital.Key,
                        AttDoc1 = reader["attdoc1"]?.ToString(),
                        RefDoc = reader["refdoc"]?.ToString(),
                        AdmissionDate = reader["ADMISSIONDATE"] as DateTime?,
                        ReferenceNumber = reader["referencenumber"]?.ToString(),
                        Title = reader["title"]?.ToString(),
                        FirstNames = reader["firstnames"]?.ToString(),
                        Surname = reader["surname"]?.ToString(),
                        AdmissionTimeFormatted = reader["ADMISTIMES"]?.ToString()
                    });
                }

                // PostgreSQL: get incident count for this hospital
                if (hospitalIdMap.TryGetValue(hospital.Key, out int mappedHospitalId))
                {
                    using var pgConn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                    await pgConn.OpenAsync();

                    var incidentQuery = @"
                    SELECT COUNT(*) 
                    FROM tblincident 
                    WHERE active = 'Y' AND pte = 'Patient' AND hospitalid = @hospitalid";

                    if (month.HasValue)
                        incidentQuery += " AND EXTRACT(MONTH FROM incidentdate) = @month";
                    if (year.HasValue)
                        incidentQuery += " AND EXTRACT(YEAR FROM incidentdate) = @year";

                    using var pgCmd = new NpgsqlCommand(incidentQuery, pgConn);
                    pgCmd.Parameters.AddWithValue("@hospitalid", mappedHospitalId.ToString());
                    if (month.HasValue)
                        pgCmd.Parameters.AddWithValue("@month", month.Value);
                    if (year.HasValue)
                        pgCmd.Parameters.AddWithValue("@year", year.Value);

                    incidentCount = Convert.ToInt32(await pgCmd.ExecuteScalarAsync());
                    var patientCount = count > 0 ? count : 1;
                    incidentRate = (double)incidentCount / patientCount * 1000;
                }
            }
            catch (Exception ex)
            {
                results.Add(new HospitalAdmissionModel
                {
                    HospitalName = hospital.Key,
                    ReferenceNumber = $"Connection failed: {ex.Message}"
                });
            }

            // Single entry added to hospitalCounts per hospital
            hospitalCounts.Add(new HospitalCountModel
            {
                HospitalName = hospital.Key,
                PatientCount = count,
                IncidentCount = incidentCount,
                IncidentRate = incidentRate
            });
        }

        ViewBag.PatientCounts = hospitalCounts;
        ViewBag.SelectedMonth = month;
        ViewBag.SelectedYear = year;


        await NurseTrends();
        return View(results);
    }

    public async Task<IActionResult> NurseTrends()
    {
        var trends = new List<IncidentTrendModel>();

        using var conn = new NpgsqlConnection(_configuration.GetConnectionString("DefaultConnection"));
        await conn.OpenAsync();

        string sql = @"
        SELECT
            TO_CHAR(incidentdate, 'Mon') AS month,
            COUNT(CASE WHEN LOWER(incidenttype) iLIKE '%medicat%' OR 
                            LOWER(inctypescat1) iLIKE '%medicat%' OR 
                            LOWER(inctypescat2) iLIKE '%medicat%' OR 
                            LOWER(incidentcriteria) iLIKE '%medicat%' OR 
                            LOWER(incidentcriteriasub) iLIKE '%medicat%' THEN 1 END) AS medication_related,
            COUNT(CASE WHEN LOWER(incidenttype) iLIKE '%pressure%' OR 
                            LOWER(inctypescat1) iLIKE '%pressure%' OR 
                            LOWER(inctypescat2) iLIKE '%pressure%' OR 
                            LOWER(incidentcriteria) iLIKE '%pressure%' OR 
                            LOWER(incidentcriteriasub) iLIKE '%pressure%' THEN 1 END) AS pressure_injuries,
            COUNT(CASE WHEN LOWER(incidenttype) iLIKE '%fall%' OR 
                            LOWER(inctypescat1) iLIKE '%fall%' OR 
                            LOWER(inctypescat2) iLIKE '%fall%' OR 
                            LOWER(incidentcriteria) iLIKE '%fall%' OR 
                            LOWER(incidentcriteriasub) iLIKE '%fall%' THEN 1 END) AS slip_falls
        FROM tblincident
        WHERE active = 'Y' AND incidentdate >= date_trunc('year', current_date)
        GROUP BY TO_CHAR(incidentdate, 'Mon'), EXTRACT(MONTH FROM incidentdate)
        ORDER BY EXTRACT(MONTH FROM incidentdate);
    ";

        using var cmd = new NpgsqlCommand(sql, conn);
        using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            trends.Add(new IncidentTrendModel
            {
                Month = reader["month"]?.ToString() ?? "",
                MedicationRelated = Convert.ToInt32(reader["medication_related"]),
                PressureInjuries = Convert.ToInt32(reader["pressure_injuries"]),
                SlipFalls = Convert.ToInt32(reader["slip_falls"])
            });
        }

        ViewBag.IncidentTrends = trends;
        return View();
    }







    //public async Task<IActionResult> Index()
    //{
    //    var hospitalConnections = new Dictionary<string, string>
    //    {
    //        { "MG", _configuration["MySql_Connection_1"] },
    //        { "MMP", _configuration["MySql_Connection_2"] },
    //        { "MB", _configuration["MySql_Connection_3"] },
    //        { "MC", _configuration["MySql_Connection_5"] },
    //        { "MT", _configuration["MySql_Connection_6"] },
    //        { "MRB", _configuration["MySql_Connection_7"] }
    //    };

    //    var results = new List<HospitalAdmissionModel>();
    //    string query = @"SELECT attdoc1, refdoc, ADMISSIONDATE, referencenumber, title, firstnames, surname, 
    //                    CONCAT(LPAD(FLOOR(ADMISSIONTIME/6000/60),2,0), ':', 
    //                    LPAD(FLOOR((ADMISSIONTIME - FLOOR(ADMISSIONTIME/6000/60)*6000*60)/6000),2,0)) AS ADMISTIMES 
    //                    FROM patient 
    //                    WHERE referencenumber NOT LIKE 'PRE%' 
    //                      AND referencenumber NOT LIKE 'NEW%' 
    //                      AND dischargedate IS NULL 
    //                      AND released = 'N' 
    //                    ORDER BY ADMISSIONDATE";

    //    foreach (var hospital in hospitalConnections)
    //    {
    //        try
    //        {
    //            using var conn = new MySqlConnection(hospital.Value);
    //            await conn.OpenAsync();
    //            using var cmd = new MySqlCommand(query, conn);
    //            using var reader = await cmd.ExecuteReaderAsync();

    //            while (await reader.ReadAsync())
    //            {
    //                results.Add(new HospitalAdmissionModel
    //                {
    //                    HospitalName = hospital.Key,
    //                    AttDoc1 = reader["attdoc1"]?.ToString(),
    //                    RefDoc = reader["refdoc"]?.ToString(),
    //                    AdmissionDate = reader["ADMISSIONDATE"] as DateTime?,
    //                    ReferenceNumber = reader["referencenumber"]?.ToString(),
    //                    Title = reader["title"]?.ToString(),
    //                    FirstNames = reader["firstnames"]?.ToString(),
    //                    Surname = reader["surname"]?.ToString(),
    //                    AdmissionTimeFormatted = reader["ADMISTIMES"]?.ToString()
    //                });
    //            }
    //        }
    //        catch (Exception ex)
    //        {
    //            // Log or display which hospital failed
    //            results.Add(new HospitalAdmissionModel
    //            {
    //                HospitalName = hospital.Key,
    //                ReferenceNumber = $"Connection failed: {ex.Message}"
    //            });
    //        }
    //    }


    //    return View(results);
    //}







}
