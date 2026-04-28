namespace APBD6.DTOs
{
    public class AppointmentDetailsDto
    {
        public int AppointmentId { get; set; }

        public DateTime AppointmentDate { get; set; }
        public DateTime CreatedAt { get; set; }

        public string PatientEmail { get; set; }
        public string PatientPhone { get; set; }

        public string DoctorLicenseNumber { get; set; }
    }
}