using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UPortal.Dtos;
using UPortal.Services;

namespace UPortal.Controllers
{
    /// <summary>
    /// API controller for managing employee financial data.
    /// Access to these endpoints is restricted to users with the "Manager" role.
    /// </summary>
    [ApiController]
    [Route("api/employeefinancials")]
    [Authorize(Roles = "Manager")] // Ensures all actions in this controller require Manager role
    public class EmployeeFinancialsController : ControllerBase
    {
        private readonly IAppUserService _appUserService;
        private readonly IFinancialService _financialService;
        private readonly ILogger<EmployeeFinancialsController> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="EmployeeFinancialsController"/> class.
        /// </summary>
        /// <param name="appUserService">The application user service.</param>
        /// <param name="financialService">The financial calculation service.</param>
        /// <param name="logger">The logger.</param>
        public EmployeeFinancialsController(
            IAppUserService appUserService,
            IFinancialService financialService,
            ILogger<EmployeeFinancialsController> logger)
        {
            _appUserService = appUserService ?? throw new ArgumentNullException(nameof(appUserService));
            _financialService = financialService ?? throw new ArgumentNullException(nameof(financialService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Retrieves the financial details for a specific employee.
        /// </summary>
        /// <param name="userId">The ID of the user whose financial details are to be retrieved.</param>
        /// <returns>An <see cref="AppUserDto"/> containing the user's details, including financial information.</returns>
        /// <response code="200">Returns the user's details.</response>
        /// <response code="401">If the user is not authenticated.</response>
        /// <response code="403">If the authenticated user is not a Manager.</response>
        /// <response code="404">If the user is not found.</response>
        /// <response code="500">If an unexpected error occurs.</response>
        [HttpGet("{userId}")]
        public async Task<IActionResult> GetEmployeeFinancialDetails(int userId)
        {
            _logger.LogInformation("Attempting to get financial details for user ID: {UserId}", userId);
            var user = await _appUserService.GetUserByIdAsync(userId); // Using the new method

            if (user == null)
            {
                _logger.LogWarning("User with ID {UserId} not found when requesting financial details.", userId);
                return NotFound($"User with ID {userId} not found.");
            }
            return Ok(user);
        }

        /// <summary>
        /// Updates the financial details (GrossMonthlyWage, SeniorityLevel) for a specific employee.
        /// </summary>
        /// <param name="userId">The ID of the user whose financial details are to be updated.</param>
        /// <param name="dto">The DTO containing the financial data to update.</param>
        /// <returns>No content if successful.</returns>
        /// <response code="204">Indicates the update was successful.</response>
        /// <response code="400">If the provided DTO is invalid.</response>
        /// <response code="401">If the user is not authenticated.</response>
        /// <response code="403">If the authenticated user is not a Manager.</response>
        /// <response code="404">If the user to update is not found.</response>
        /// <response code="500">If an unexpected error occurs.</response>
        [HttpPut("{userId}")]
        public async Task<IActionResult> UpdateEmployeeFinancialDetails(int userId, [FromBody] UpdateAppUserFinancialsDto dto)
        {
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid model state for UpdateEmployeeFinancialDetails for UserId: {UserId}", userId);
                return BadRequest(ModelState);
            }

            _logger.LogInformation("Attempting to update financial details for user ID: {UserId}", userId);
            try
            {
                await _appUserService.UpdateFinancialDataAsync(userId, dto);
                _logger.LogInformation("Successfully updated financial details for user ID: {UserId}", userId);
                return NoContent();
            }
            catch (KeyNotFoundException knfex)
            {
                _logger.LogWarning(knfex, "User with ID {UserId} not found during financial update.", userId);
                return NotFound(knfex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating financial details for user ID: {UserId}", userId);
                return StatusCode(500, "An internal server error occurred.");
            }
        }

        /// <summary>
        /// Calculates and retrieves the total monthly cost for a specific employee.
        /// The calculation is based on the employee's current gross wage and applicable employer taxes.
        /// </summary>
        /// <param name="userId">The ID of the user for whom to calculate the monthly cost.</param>
        /// <returns>The total calculated monthly cost.</returns>
        /// <response code="200">Returns the calculated monthly cost.</response>
        /// <response code="401">If the user is not authenticated.</response>
        /// <response code="403">If the authenticated user is not a Manager.</response>
        /// <response code="404">If the user or their gross wage is not found (results in 0 cost as per service logic).</response>
        /// <response code="500">If an unexpected error occurs.</response>
        [HttpGet("{userId}/monthlycost")]
        public async Task<IActionResult> GetEmployeeMonthlyCost(int userId)
        {
            _logger.LogInformation("Attempting to calculate monthly cost for user ID: {UserId}", userId);
            try
            {
                // The FinancialService's CalculateTotalMonthlyCostAsync handles cases where the user
                // or their wage is not found by returning 0 and logging a warning.
                // If the user doesn't exist at all for AppUserService.GetUserByIdAsync, that would be a 404 before this.
                // However, FinancialService uses AppUserService internally. If GetUserByIdAsync in AppUserService returns null,
                // FinancialService will log it and return 0. This seems acceptable.
                // An alternative is to check if user exists here first.
                var userCheck = await _appUserService.GetUserByIdAsync(userId);
                if (userCheck == null)
                {
                     _logger.LogWarning("User with ID {UserId} not found when trying to calculate monthly cost.", userId);
                    return NotFound($"User with ID {userId} not found.");
                }
                if (userCheck.GrossMonthlyWage == null || userCheck.GrossMonthlyWage <=0)
                {
                    _logger.LogInformation("User with ID {UserId} has no gross monthly wage; monthly cost is 0.", userId);
                    return Ok(0m); // Return 0 if no wage is set, consistent with FinancialService logic
                }


                decimal monthlyCost = await _financialService.CalculateTotalMonthlyCostAsync(userId);
                _logger.LogInformation("Successfully calculated monthly cost for user ID: {UserId}. Cost: {MonthlyCost}", userId, monthlyCost);
                return Ok(monthlyCost);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating monthly cost for user ID: {UserId}", userId);
                return StatusCode(500, "An internal server error occurred.");
            }
        }
    }
}
