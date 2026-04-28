namespace APBD6.DTOs
{
    public class UpdateAppointmentRequestDto
    {
        public int IdPatient { get; set; }
        public int IdDoctor { get; set; }
        public DateTime AppointmentDate { get; set; }
        public string Status { get; set; } = String.Empty;
        public string Reason { get; set; } = String.Empty;
        public string InternalNotes { get; set; } = String.Empty;
    }
}