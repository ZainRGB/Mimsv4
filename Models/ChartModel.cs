using Microsoft.AspNetCore.Mvc.Rendering;

namespace Mimsv2.Models
{
    public class ChartModel
    {
        public int Open { get; set; }
        public int OnHold { get; set; }
        public int Closed { get; set; }
        public int OpenOver5Days { get; set; }
        public int OpenOver10Days { get; set; }
        public int Total { get; set; }


        public string? SelectedHospitalId { get; set; }
        public string? inserthospitalid { get; set; }
        public List<SelectListItem>? Hospitals { get; set; }

        public string? HospitalName { get; set; }
    }


    public class MonthlyIncidentData
    {
        public string Month { get; set; }
        public int Count { get; set; }
    }


    public class GroupQualityIndicatorModel
    {
        public string Hospital { get; set; }
        public string Summary { get; set; }
        public string Severity { get; set; }
        public string Department { get; set; }
        public string CorrectiveActions { get; set; }
    }

    public class IncidentTrendSummaryModel
    {
        public string PTE { get; set; }
        public string IncidentType { get; set; }
        public string IncTypesCat1 { get; set; }
        public string IncTypesCat2 { get; set; }

        public int Jan { get; set; }
        public int Feb { get; set; }
        public int Mar { get; set; }
        public int Q1 { get; set; }

        public int Apr { get; set; }
        public int May { get; set; }
        public int Jun { get; set; }
        public int Q2 { get; set; }

        public int Jul { get; set; }
        public int Aug { get; set; }
        public int Sep { get; set; }
        public int Q3 { get; set; }

        public int Oct { get; set; }
        public int Nov { get; set; }
        public int Dec { get; set; }
        public int Q4 { get; set; }

        public int Total { get; set; }
        public int Goal { get; set; } // You can define static or dynamic goal
    }


    public class IncidentRateModel
    {
        public string HospitalId { get; set; } = "";
        public string HospitalName { get; set; } = "";
        public string IncidentCriteria { get; set; } = "";

        public int[] MonthlyCounts { get; set; } = new int[12]; // Jan to Dec

        // Quarterly breakdowns
        public int Q1 => MonthlyCounts[0] + MonthlyCounts[1] + MonthlyCounts[2];
        public int Q2 => MonthlyCounts[3] + MonthlyCounts[4] + MonthlyCounts[5];
        public int Q3 => MonthlyCounts[6] + MonthlyCounts[7] + MonthlyCounts[8];
        public int Q4 => MonthlyCounts[9] + MonthlyCounts[10] + MonthlyCounts[11];

        public int TotalIncidents => MonthlyCounts.Sum();

        public double PercentageOfGroup { get; set; } // Calculated in controller
    }

    public class IncidentRateViewModel
    {
        public List<IncidentRateModel> HospitalIncidentRates { get; set; } = new();
        public List<IncidentRateModel> GroupIncidentRates { get; set; } = new();
        public int GroupTotalPTE { get; set; }

        
    }

}
