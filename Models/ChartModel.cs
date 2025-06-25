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
}
