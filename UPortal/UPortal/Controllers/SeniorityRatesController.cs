using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UPortal.Data.Models; // For SeniorityLevelEnum
using UPortal.Dtos;
using UPortal.Services;

namespace UPortal.Controllers
{
    /// <summary>
    /// API controller for managing seniority rates.
    /// Access to these endpoints is restricted to users with the "Manager" role.
    /// </summary>
    [ApiController]
    [Route("api/seniorityrates")]
    [Authorize(Roles = "Manager")] // Ensures all actions require Manager role
    public class SeniorityRatesController : ControllerBase
    {
        private readonly ISeniorityRateService _seniorityRateService;
        private readonly ILogger<SeniorityRatesController> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="SeniorityRatesController"/> class.
        /// </summary>
        /// <param name="seniorityRateService">The seniority rate service.</param>
        /// <param name="logger">The logger.</param>
        public SeniorityRatesController(ISeniorityRateService seniorityRateService, ILogger<SeniorityRatesController> logger)
        {
            _seniorityRateService = seniorityRateService ?? throw new ArgumentNullException(nameof(seniorityRateService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Retrieves all seniority rates.
        /// </summary>
        /// <returns>A list of all seniority rates.</returns>
        /// <response code="200">Returns the list of seniority rates.</response>
        /// <response code="401">If the user is not authenticated.</response>
        /// <response code="403">If the authenticated user is not a Manager.</response>
        /// <response code="500">If an unexpected error occurs.</response>
        [HttpGet]
        public async Task<IActionResult> GetAllSeniorityRates()
        {
            _logger.LogInformation("Attempting to retrieve all seniority rates.");
            try
            {
                var rates = await _seniorityRateService.GetAllAsync();
                return Ok(rates);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all seniority rates.");
                return StatusCode(500, "An internal server error occurred.");
            }
        }

        /// <summary>
        /// Creates a new seniority rate.
        /// </summary>
        /// <param name="dto">The DTO containing the details for the new seniority rate.</param>
        /// <returns>The created seniority rate.</returns>
        /// <response code="201">Returns the newly created seniority rate.</response>
        /// <response code="400">If the DTO is invalid or a rate for the level already exists.</response>
        /// <response code="401">If the user is not authenticated.</response>
        /// <response code="403">If the authenticated user is not a Manager.</response>
        /// <response code="500">If an unexpected error occurs.</response>
        [HttpPost]
        public async Task<IActionResult> CreateSeniorityRate([FromBody] SeniorityRateDto dto)
        {
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid model state for CreateSeniorityRate for Level: {Level}", dto.Level);
                return BadRequest(ModelState);
            }

            _logger.LogInformation("Attempting to create seniority rate for level: {Level}", dto.Level);
            try
            {
                var createdRate = await _seniorityRateService.CreateAsync(dto);
                // The GetByLevelAsync method in service expects enum, but controller action for GET by level might take string.
                // For CreatedAtAction, we need a GET action that takes the key of the created resource.
                // Let's assume a GetSeniorityRateByLevel action exists or will be created that takes the string representation of the enum.
                return CreatedAtAction(nameof(GetSeniorityRateByLevel), new { levelString = createdRate.Level }, createdRate);
            }
            catch (ArgumentException aex) // Handles duplicate level or invalid enum string
            {
                _logger.LogWarning(aex, "Argument error creating seniority rate for level: {Level}", dto.Level);
                return BadRequest(aex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating seniority rate for level: {Level}", dto.Level);
                return StatusCode(500, "An internal server error occurred.");
            }
        }

        /// <summary>
        /// Retrieves a specific seniority rate by its level.
        /// </summary>
        /// <param name="levelString">The string representation of the seniority level (e.g., "Junior", "Senior").</param>
        /// <returns>The requested seniority rate if found.</returns>
        /// <response code="200">Returns the seniority rate.</response>
        /// <response code="400">If the level string is not a valid SeniorityLevelEnum value.</response>
        /// <response code="401">If the user is not authenticated.</response>
        /// <response code="403">If the authenticated user is not a Manager.</response>
        /// <response code="404">If no rate is found for the specified level.</response>
        /// <response code="500">If an unexpected error occurs.</response>
        [HttpGet("{levelString}")]
        public async Task<IActionResult> GetSeniorityRateByLevel(string levelString)
        {
            _logger.LogInformation("Attempting to retrieve seniority rate for level string: {LevelString}", levelString);
            if (!Enum.TryParse<SeniorityLevelEnum>(levelString, true, out var levelEnum))
            {
                _logger.LogWarning("Invalid seniority level string provided: {LevelString}", levelString);
                return BadRequest($"Invalid seniority level string: {levelString}. Valid levels are Junior, MidLevel, Senior, Lead.");
            }

            try
            {
                var rate = await _seniorityRateService.GetByLevelAsync(levelEnum);
                if (rate == null)
                {
                    _logger.LogWarning("Seniority rate for level {LevelEnum} not found.", levelEnum);
                    return NotFound($"Seniority rate for level '{levelEnum}' not found.");
                }
                return Ok(rate);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving seniority rate for level {LevelEnum}", levelEnum);
                return StatusCode(500, "An internal server error occurred.");
            }
        }


        /// <summary>
        /// Updates an existing seniority rate for a specific level.
        /// </summary>
        /// <param name="levelString">The string representation of the seniority level of the rate to update (e.g., "Junior").</param>
        /// <param name="dto">The DTO containing the updated daily rate. The Level in the DTO should match levelString or will be ignored.</param>
        /// <returns>No content if successful.</returns>
        /// <response code="204">Indicates the update was successful.</response>
        /// <response code="400">If the DTO is invalid or the level string is not a valid SeniorityLevelEnum value.</response>
        /// <response code="401">If the user is not authenticated.</response>
        /// <response code="403">If the authenticated user is not a Manager.</response>
        /// <response code="404">If no rate is found for the specified level to update.</response>
        /// <response code="500">If an unexpected error occurs.</response>
        [HttpPut("{levelString}")]
        public async Task<IActionResult> UpdateSeniorityRate(string levelString, [FromBody] SeniorityRateDto dto)
        {
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid model state for UpdateSeniorityRate for LevelString: {LevelString}", levelString);
                return BadRequest(ModelState);
            }

            if (!Enum.TryParse<SeniorityLevelEnum>(levelString, true, out var levelEnum))
            {
                _logger.LogWarning("Invalid seniority level string provided for update: {LevelString}", levelString);
                return BadRequest($"Invalid seniority level string: {levelString}. Valid levels are Junior, MidLevel, Senior, Lead.");
            }

            // Ensure the DTO's level, if provided and different, doesn't cause confusion.
            // The URL's levelString is canonical for which resource to update.
            if (!string.IsNullOrWhiteSpace(dto.Level) && !dto.Level.Equals(levelString, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("SeniorityRateDto.Level ({DtoLevel}) in body does not match levelString ({LevelString}) in URL path.", dto.Level, levelString);
                // Optionally return BadRequest here, or just ignore dto.Level and use levelEnum from path. Service uses enum from path.
            }


            _logger.LogInformation("Attempting to update seniority rate for level: {LevelEnum}", levelEnum);
            try
            {
                var updatedRate = await _seniorityRateService.UpdateAsync(levelEnum, dto);
                if (updatedRate == null)
                {
                    _logger.LogWarning("Seniority rate for level {LevelEnum} not found for update.", levelEnum);
                    return NotFound($"Seniority rate for level '{levelEnum}' not found.");
                }
                return NoContent();
            }
            catch (Exception ex) // Catches DbUpdateConcurrencyException from service too
            {
                _logger.LogError(ex, "Error updating seniority rate for level {LevelEnum}", levelEnum);
                return StatusCode(500, "An internal server error occurred.");
            }
        }

        /// <summary>
        /// Deletes a seniority rate by its level.
        /// </summary>
        /// <param name="levelString">The string representation of the seniority level of the rate to delete (e.g., "Junior").</param>
        /// <returns>No content if successful.</returns>
        /// <response code="204">Indicates the deletion was successful.</response>
        /// <response code="400">If the level string is not a valid SeniorityLevelEnum value.</response>
        /// <response code="401">If the user is not authenticated.</response>
        /// <response code="403">If the authenticated user is not a Manager.</response>
        /// <response code="404">If no rate is found for the specified level to delete.</response>
        /// <response code="500">If an unexpected error occurs.</response>
        [HttpDelete("{levelString}")]
        public async Task<IActionResult> DeleteSeniorityRate(string levelString)
        {
             if (!Enum.TryParse<SeniorityLevelEnum>(levelString, true, out var levelEnum))
            {
                _logger.LogWarning("Invalid seniority level string provided for delete: {LevelString}", levelString);
                return BadRequest($"Invalid seniority level string: {levelString}. Valid levels are Junior, MidLevel, Senior, Lead.");
            }

            _logger.LogInformation("Attempting to delete seniority rate for level: {LevelEnum}", levelEnum);
            try
            {
                var deleted = await _seniorityRateService.DeleteAsync(levelEnum);
                if (!deleted)
                {
                    _logger.LogWarning("Seniority rate for level {LevelEnum} not found for deletion.", levelEnum);
                    return NotFound($"Seniority rate for level '{levelEnum}' not found.");
                }
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting seniority rate for level {LevelEnum}", levelEnum);
                return StatusCode(500, "An internal server error occurred.");
            }
        }
    }
}
