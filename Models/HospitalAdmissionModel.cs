namespace Mimsv2.Models
{
    public class HospitalAdmissionModel
    {
        public string HospitalName { get; set; }
        public string AttDoc1 { get; set; }
        public string RefDoc { get; set; }
        public DateTime? AdmissionDate { get; set; }
        public string ReferenceNumber { get; set; }
        public string Title { get; set; }
        public string FirstNames { get; set; }
        public string Surname { get; set; }
        public string AdmissionTimeFormatted { get; set; }


    }

    public class HospitalCountModel
    {
        public string HospitalName { get; set; }
        public int PatientCount { get; set; }

        public int IncidentCount { get; set; }      
        public double IncidentRate { get; set; }
    }

    public class IncidentTrendModel
    {
        public string Month { get; set; }
        public int MedicationRelated { get; set; }
        public int PressureInjuries { get; set; }
        public int SlipFalls { get; set; }
    }


}
