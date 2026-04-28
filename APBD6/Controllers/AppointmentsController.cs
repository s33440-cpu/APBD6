using APBD6.DTOs;
using APBD6.Exceptions;
using APBD6.Services;
using Microsoft.AspNetCore.Mvc;

namespace APBD6.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AppointmentController : ControllerBase
    {
        private readonly IAppointmentService _appointmentService;

        public AppointmentController(IAppointmentService appointmentService)
        {
            _appointmentService = appointmentService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAppointments(
            [FromQuery] string? status,
            [FromQuery] string? patientLastName)
        {
            var result = await _appointmentService.GetAppointmentsAsync(status, patientLastName);
            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetAppointmentById(int id)
        {
            var result = await _appointmentService.GetAppointmentByIdAsync(id);
            if (result == null)
            {
                return NotFound();
            }
            return Ok(result);
        }

        [HttpPost]
        public async Task<IActionResult> CreateAppointment(
            [FromBody] CreateAppointmentRequestDto appointmentCreateDto)
        {
            try { 
                var result = await _appointmentService.CreateAppointmentAsync(appointmentCreateDto);
                return CreatedAtAction(nameof(GetAppointmentById), new { id = result }, null);
            }
            catch(OverlapException ex)
            {
                return Conflict(new ErrorResponseDto() { Message = ex.Message , Source = ex.Source ?? string.Empty });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new ErrorResponseDto() { Message = ex.Message, Source = ex.Source ?? string.Empty });
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateAppointment(int id, UpdateAppointmentRequestDto appointmentUpdateDto)
        {
            try
            {
                await _appointmentService.UpdateAppointmentAsync(id, appointmentUpdateDto);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ErrorResponseDto() { Message = ex.Message, Source = ex.Source ?? string.Empty });
            }
            catch (OverlapException ex)
            {
                return Conflict(new ErrorResponseDto() { Message = ex.Message, Source = ex.Source ?? string.Empty });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new ErrorResponseDto(){ Message = ex.Message, Source = ex.Source ?? string.Empty });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new ErrorResponseDto() { Message = ex.Message, Source = ex.Source ?? string.Empty });
            }

            return Ok();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteAppointment(int id)
        {
            try
            {
                await _appointmentService.DeleteAppointmentAsync(id);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ErrorResponseDto() { Message = ex.Message, Source = ex.Source ?? string.Empty });
            }
            catch(InvalidOperationException ex)
            {
                return Conflict(new ErrorResponseDto() { Message = ex.Message, Source = ex.Source ?? string.Empty });
            }
            return NoContent();
        }
    }
}