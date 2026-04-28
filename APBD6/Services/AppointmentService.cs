using APBD6.DTOs;
using APBD6.Exceptions;
using Microsoft.Data.SqlClient;
using System.Data;

namespace APBD6.Services
{
    public class AppointmentService : IAppointmentService
    {
        private readonly IConfiguration _configuration;

        public AppointmentService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<int> CreateAppointmentAsync(CreateAppointmentRequestDto dto)
        {
            if (dto == null)
                throw new ArgumentException("Request cannot be null");

            if (string.IsNullOrWhiteSpace(dto.Reason) || dto.Reason.Length > 250)
                throw new ArgumentException("Reason must be 1–250 characters");

            if (dto.AppointmentDate < DateTime.Now)
                throw new ArgumentException("Appointment date cannot be in the past");

            using var connection = new SqlConnection(_configuration.GetConnectionString("Default"));
            await connection.OpenAsync();

            await EnsurePatientExistsAsync(connection, dto.IdPatient);
            await EnsureDoctorExistsAsync(connection, dto.IdDoctor);
            await CheckOverlapAsync(connection, dto.IdDoctor, dto.AppointmentDate);

            var insertCmd = new SqlCommand(@"
                INSERT INTO Appointments 
                    (IdPatient, IdDoctor, AppointmentDate, Reason, Status, CreatedAt)
                VALUES 
                    (@IdPatient, @IdDoctor, @AppointmentDate, @Reason, 'Scheduled', GETDATE());

                SELECT SCOPE_IDENTITY();", connection);

            insertCmd.Parameters.Add("@IdPatient", SqlDbType.Int).Value = dto.IdPatient;
            insertCmd.Parameters.Add("@IdDoctor", SqlDbType.Int).Value = dto.IdDoctor;
            insertCmd.Parameters.Add("@AppointmentDate", SqlDbType.DateTime).Value = dto.AppointmentDate;
            insertCmd.Parameters.Add("@Reason", SqlDbType.NVarChar, 250).Value = dto.Reason;

            var result = await insertCmd.ExecuteScalarAsync();

            return Convert.ToInt32(result);
        }

        public async Task<AppointmentDetailsDto?> GetAppointmentByIdAsync(int id)
        {

            var query = @"
                SELECT
                    a.IdAppointment,
                    a.AppointmentDate,
                    a.CreatedAt,
                    p.Email AS PatientEmail,
                    p.PhoneNumber AS PatientPhone,
                    d.LicenseNumber AS DoctorLicenseNumber
                FROM dbo.Appointments a
                JOIN dbo.Patients p ON p.IdPatient = a.IdPatient
                JOIN dbo.Doctors d ON d.IdDoctor = a.IdDoctor
                WHERE a.IdAppointment = @Id;";

            using var connection = new SqlConnection(_configuration.GetConnectionString("Default"));
            using var command = new SqlCommand(query, connection);

            command.Parameters.AddWithValue("@Id", id);

            await connection.OpenAsync();

            using var reader = await command.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
                return null;

            var result = new AppointmentDetailsDto
            {
                AppointmentId = reader.GetInt32(0),
                AppointmentDate = reader.GetDateTime(1),
                CreatedAt = reader.GetDateTime(2),
                PatientEmail = reader.GetString(3),
                PatientPhone = reader.GetString(4),
                DoctorLicenseNumber = reader.GetString(5)
            };

            return result;
        }

        public async Task<IEnumerable<AppointmentListDto>> GetAppointmentsAsync(string? status, string? patientLastName)
        {
            var result = new List<AppointmentListDto>();

            var query = @"
                SELECT
                    a.IdAppointment,
                    a.AppointmentDate,
                    a.Status,
                    a.Reason,
                    p.FirstName + N' ' + p.LastName AS PatientFullName,
                    p.Email AS PatientEmail
                FROM dbo.Appointments a
                JOIN dbo.Patients p ON p.IdPatient = a.IdPatient
                WHERE (@Status IS NULL OR a.Status = @Status)
                  AND (@PatientLastName IS NULL OR p.LastName = @PatientLastName)
                ORDER BY a.AppointmentDate;
            ";

            using var connection = new SqlConnection(_configuration.GetConnectionString("Default"));
            using var command = new SqlCommand(query, connection);

            command.Parameters.AddWithValue("@Status", (object?)status ?? DBNull.Value);
            command.Parameters.AddWithValue("@PatientLastName", (object?)patientLastName ?? DBNull.Value);

            await connection.OpenAsync();

            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                result.Add(new AppointmentListDto
                {
                    IdAppointment = reader.GetInt32(0),
                    AppointmentDate = reader.GetDateTime(1),
                    Status = reader.GetString(2),
                    Reason = reader.GetString(3),
                    PatientFullName = reader.GetString(4),
                    PatientEmail = reader.GetString(5)
                });
            }

            return result;
        }

        public async Task DeleteAppointmentAsync(int id)
        {
            using var connection = new SqlConnection(_configuration.GetConnectionString("Default"));
            await connection.OpenAsync();

            var selectCmd = new SqlCommand(@"
                SELECT Status
                FROM Appointments
                WHERE IdAppointment = @Id", connection);

            selectCmd.Parameters.Add("@Id", SqlDbType.Int).Value = id;

            var statusObj = await selectCmd.ExecuteScalarAsync();

            if (statusObj == null || statusObj == DBNull.Value)
                throw new KeyNotFoundException("Appointment not found");

            var status = statusObj.ToString();

            if (status == "Completed")
                throw new InvalidOperationException("Cannot delete a completed appointment");

            var deleteCmd = new SqlCommand(@"
                DELETE FROM Appointments
                WHERE IdAppointment = @Id", connection);

            deleteCmd.Parameters.Add("@Id", SqlDbType.Int).Value = id;

            var rowsAffected = await deleteCmd.ExecuteNonQueryAsync();

            if (rowsAffected == 0)
                throw new KeyNotFoundException("Appointment not found");
        }
        public async Task UpdateAppointmentAsync(int id, UpdateAppointmentRequestDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Reason) || dto.Reason.Length > 250)
                throw new ArgumentException("Reason must be 1–250 characters");

            if (dto.InternalNotes != null && dto.InternalNotes.Length > 500)
                throw new ArgumentException("Internal notes cannot exceed 500 characters");

            if (dto.AppointmentDate < DateTime.Now)
                throw new ArgumentException("Appointment date cannot be in the past");

            using var connection = new SqlConnection(_configuration.GetConnectionString("Default"));
            await connection.OpenAsync();


            var getCmd = new SqlCommand(@"
                SELECT AppointmentDate, Status, IdDoctor
                FROM Appointments
                WHERE IdAppointment = @Id", connection);

            getCmd.Parameters.Add("@Id", SqlDbType.Int).Value = id;

            using var reader = await getCmd.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
                throw new KeyNotFoundException("Appointment not found"); 

            var currentDate = reader.GetDateTime(0);
            var currentStatus = reader.GetString(1);
            var currentDoctorId = reader.GetInt32(2);

            await reader.CloseAsync();

            await EnsurePatientExistsAsync(connection, dto.IdPatient);
            await EnsureDoctorExistsAsync(connection, dto.IdDoctor);

            var allowedStatuses = new[] { "Scheduled", "Completed", "Cancelled" };
            if (!allowedStatuses.Contains(dto.Status))
                throw new ArgumentException("Invalid status");

            if (currentStatus == "Completed" && dto.AppointmentDate != currentDate)
                throw new InvalidOperationException("Cannot change date of a completed appointment");

            if (currentDate != dto.AppointmentDate || currentDoctorId != dto.IdDoctor)
                await CheckOverlapAsync(connection, dto.IdDoctor, dto.AppointmentDate, id);

            var updateCmd = new SqlCommand(@"
                UPDATE Appointments
                SET 
                    IdPatient = @IdPatient,
                    IdDoctor = @IdDoctor,
                    AppointmentDate = @AppointmentDate,
                    Status = @Status,
                    Reason = @Reason,
                    InternalNotes = @InternalNotes
                WHERE IdAppointment = @Id", connection);

            updateCmd.Parameters.Add("@IdPatient", SqlDbType.Int).Value = dto.IdPatient;
            updateCmd.Parameters.Add("@IdDoctor", SqlDbType.Int).Value = dto.IdDoctor;
            updateCmd.Parameters.Add("@AppointmentDate", SqlDbType.DateTime2).Value = dto.AppointmentDate;
            updateCmd.Parameters.Add("@Status", SqlDbType.NVarChar, 30).Value = dto.Status;
            updateCmd.Parameters.Add("@Reason", SqlDbType.NVarChar, 250).Value = dto.Reason;
            updateCmd.Parameters.Add("@InternalNotes", SqlDbType.NVarChar, 500).Value = dto.InternalNotes;
            updateCmd.Parameters.Add("@Id", SqlDbType.Int).Value = id;

            await updateCmd.ExecuteNonQueryAsync();
            
        }
        
        private async Task CheckOverlapAsync(SqlConnection connection, int idDoctor, DateTime appointmentDate, int? exceptId = null)
        {
            var query = new SqlCommand(@"
                SELECT COUNT(1)
                FROM Appointments
                WHERE IdDoctor = @IdDoctor
                  AND AppointmentDate = @AppointmentDate
                  AND Status = 'Scheduled'
                  AND IdAppointment <> ISNULL(@ExceptId, -1);", connection);

            query.Parameters.Add("@IdDoctor", SqlDbType.Int).Value = idDoctor;
            query.Parameters.Add("@AppointmentDate", SqlDbType.DateTime2).Value = appointmentDate;
            query.Parameters.Add("@ExceptId", SqlDbType.Int).Value = (object)exceptId ?? DBNull.Value;

            if ((int)await query.ExecuteScalarAsync() > 0)
                throw new OverlapException("Doctor already has an appointment at this time");
        }
        private async Task EnsurePatientExistsAsync(SqlConnection connection, int idPatient)
        {
            var query = new SqlCommand(@"
                SELECT COUNT(1)
                FROM Patients
                WHERE IdPatient = @IdPatient  AND IsActive = 1", connection);

            query.Parameters.Add("@IdPatient", SqlDbType.Int).Value = idPatient;

            if ((int)await query.ExecuteScalarAsync() == 0)
                throw new ArgumentException("Patient does not exist or is not active");
        }
        private async Task EnsureDoctorExistsAsync(SqlConnection connection, int idDoctor)
        {
            using var query = new SqlCommand(@"
                SELECT COUNT(1)
                FROM Doctors
                WHERE IdDoctor = @IdDoctor AND IsActive = 1", connection);
            query.Parameters.Add("@IdDoctor", SqlDbType.Int).Value = idDoctor;

            if ((int)await query.ExecuteScalarAsync() == 0)
                throw new ArgumentException("Doctor does not exist or is not active");
        }

    }
}