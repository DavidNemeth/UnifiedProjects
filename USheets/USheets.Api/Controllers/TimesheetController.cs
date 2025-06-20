using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using USheets.Api.Dtos;
using USheets.Api.Models;
using USheets.Api.Services;
using System.Security.Claims;

namespace USheets.Api.Controllers
{
    [Route("api/timesheets")] // Pluralized resource name
    [ApiController]
    [Authorize] // All endpoints require authentication by default
    public class TimesheetController : ControllerBase
    {
        private readonly ITimesheetService _timesheetService;
        private readonly ILogger<TimesheetController> _logger;

        public TimesheetController(ITimesheetService timesheetService, ILogger<TimesheetController> logger)
        {
            _timesheetService = timesheetService;
            _logger = logger;
        }

        /// <summary>
        /// Gets the timesheet for the currently authenticated user for a specific week.
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<TimesheetDto>> GetTimesheet([FromQuery] DateTime weekStartDate)
        {
            var userId = GetCurrentUserId();
            if (weekStartDate == DateTime.MinValue) return BadRequest("A valid 'weekStartDate' is required.");

            var timesheetDto = await _timesheetService.GetTimesheetAsync(userId, weekStartDate);

            if (timesheetDto == null)
            {
                return NoContent();
            }

            return Ok(timesheetDto);
        }

        /// <summary>
        /// Creates or updates the timesheet for the currently authenticated user for a specific week.
        /// </summary>
        [HttpPut]
        public async Task<ActionResult<TimesheetDto>> CreateOrUpdateTimesheet([FromBody] TimesheetCreateUpdateDto timesheetDto)
        {
            var userId = GetCurrentUserId();
            timesheetDto.UserId = userId; // Ensure the DTO uses the authenticated user's ID

            try
            {
                var resultDto = await _timesheetService.CreateOrUpdateTimesheetAsync(userId, timesheetDto);
                return Ok(resultDto);
            }
            catch (InvalidOperationException ex)
            {
                // Catches specific, known business rule violations (e.g., modifying an approved timesheet)
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while saving timesheet for user {UserId}", userId);
                return StatusCode(500, "An internal server error occurred.");
            }
        }

        // --- Manager-Specific Endpoints ---

        /// <summary>
        /// [Manager] Gets all timesheets pending approval.
        /// </summary>
        [HttpGet("pending-approval")]
        //[Authorize(Roles = "Manager")]
        public async Task<ActionResult<IEnumerable<TimesheetDto>>> GetPendingApprovalTimesheets()
        {
            var pendingTimesheets = await _timesheetService.GetPendingApprovalTimesheetsAsync();
            return Ok(pendingTimesheets);
        }

        /// <summary>
        /// [Manager] Approves a specific timesheet.
        /// </summary>
        [HttpPost("{id:int}/approve")]
        //[Authorize(Roles = "Manager")]
        public async Task<ActionResult<TimesheetDto>> ApproveTimesheet(int id)
        {
            var result = await _timesheetService.ApproveTimesheetAsync(id);
            if (result == null)
            {
                return BadRequest("Timesheet not found or is not in a state that can be approved.");
            }
            return Ok(result);
        }

        /// <summary>
        /// [Manager] Rejects a specific timesheet.
        /// </summary>
        [HttpPost("{id:int}/reject")]
        //[Authorize(Roles = "Manager")]
        public async Task<ActionResult<TimesheetDto>> RejectTimesheet(int id, [FromBody] RejectionReasonModel rejectionModel)
        {
            if (!ModelState.IsValid || string.IsNullOrWhiteSpace(rejectionModel.Reason))
            {
                return BadRequest("A valid reason is required for rejection.");
            }

            var result = await _timesheetService.RejectTimesheetAsync(id, rejectionModel.Reason);
            if (result == null)
            {
                return BadRequest("Timesheet not found or is not in a state that can be rejected.");
            }
            return Ok(result);
        }

        // --- Helper to get User ID from claims ---
        private int GetCurrentUserId()
        {
            // Read the custom "InternalUserId" claim we added in UPortal.
            var userIdClaim = User.FindFirst("InternalUserId")?.Value;

            if (int.TryParse(userIdClaim, out var userId))
            {
                return userId;
            }

            // This code path should ideally never be hit if the user is properly authenticated.
            _logger.LogError("User ID claim ('InternalUserId') is missing, null, or not a valid integer for the authenticated user.");
            throw new UnauthorizedAccessException("User ID claim is missing or invalid.");
        }

    }
}