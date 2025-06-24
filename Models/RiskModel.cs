namespace Mimsv2.Models
{
    public class RiskModel
    {
        public int Id { get; set; }
        public string qarid { get; set; } // Optional: Link to tblincident
        public string RiskTitle { get; set; }
        public string RootCause { get; set; }
        public string NearRootCause { get; set; }
        public string RiskType { get; set; }
        public string RiskLevel { get; set; }
        public string PreventativeAction { get; set; }
        public string ResponsiblePerson { get; set; }
        public DateTime TargetDate { get; set; }
        public string Status { get; set; }
        public DateTime DateIdentified { get; set; }
        public DateTime datecaptured { get; set; }
        public string Active { get; set; }
        public string inserthospitalid { get; set; } // For filtering
        public List<RiskModel> Risks { get; set; }
    }
}
