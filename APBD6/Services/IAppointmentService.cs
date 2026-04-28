using APBD6.DTOs;

namespace APBD6.Services
{
    public interface IAppointmentService
    {
        Task<IEnumerable<AppointmentListDto>> GetAppointmentsAsync(string? status, string? patientLastName);
        Task<AppointmentDetailsDto?> GetAppointmentByIdAsync(int id);
        Task<int> CreateAppointmentAsync(CreateAppointmentRequestDto appointmentCreateDto);
        Task UpdateAppointmentAsync(int id, UpdateAppointmentRequestDto updateAppointmentRequestDto);
        Task DeleteAppointmentAsync(int id);
    }
}