using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UPortal.Dtos;
using UPortal.Services;
using UPortal.Security; // For PermissionPolicies

namespace UPortal.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    // Secure the entire controller. Specific actions can override if needed.
    [Authorize] // Base authorization, specific actions will refine with policies
    public class CompanyTaxesController : ControllerBase
    {
        private readonly ICompanyTaxService _companyTaxService;
        private readonly ILogger<CompanyTaxesController> _logger;

        public CompanyTaxesController(ICompanyTaxService companyTaxService, ILogger<CompanyTaxesController> logger)
        {
            _companyTaxService = companyTaxService;
            _logger = logger;
        }

        // GET: api/companytaxes
        [HttpGet]
        [Authorize(Policy = PermissionPolicies.AccessAdminPages)]
        public async Task<ActionResult<IEnumerable<CompanyTaxDto>>> GetAllCompanyTaxes()
        {
            _logger.LogInformation("API: Getting all company taxes.");
            try
            {
                var taxes = await _companyTaxService.GetAllAsync();
                return Ok(taxes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API: Error getting all company taxes.");
                return StatusCode(500, "Internal server error while retrieving company taxes.");
            }
        }

        // GET: api/companytaxes/{id}
        [HttpGet("{id}")]
        [Authorize(Policy = PermissionPolicies.AccessAdminPages)]
        public async Task<ActionResult<CompanyTaxDto>> GetCompanyTaxById(int id)
        {
            _logger.LogInformation("API: Getting company tax by ID: {Id}", id);
            try
            {
                var tax = await _companyTaxService.GetByIdAsync(id);
                if (tax == null)
                {
                    _logger.LogWarning("API: Company tax with ID: {Id} not found.", id);
                    return NotFound($"Company tax with ID {id} not found.");
                }
                return Ok(tax);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API: Error getting company tax by ID: {Id}", id);
                return StatusCode(500, $"Internal server error while retrieving company tax with ID {id}.");
            }
        }

        // POST: api/companytaxes
        [HttpPost]
        [Authorize(Policy = PermissionPolicies.ManageSettings)]
        public async Task<ActionResult<CompanyTaxDto>> CreateCompanyTax([FromBody] CompanyTaxDto companyTaxDto)
        {
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("API: Invalid model state for creating company tax.");
                return BadRequest(ModelState);
            }

            _logger.LogInformation("API: Creating new company tax with name: {Name}", companyTaxDto.Name);
            try
            {
                var createdTax = await _companyTaxService.CreateAsync(companyTaxDto);
                return CreatedAtAction(nameof(GetCompanyTaxById), new { id = createdTax.Id }, createdTax);
            }
            catch (ArgumentException ex) // Catch specific exceptions from service if applicable
            {
                _logger.LogWarning(ex, "API: Argument error creating company tax.");
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API: Error creating company tax.");
                return StatusCode(500, "Internal server error while creating company tax.");
            }
        }

        // PUT: api/companytaxes/{id}
        [HttpPut("{id}")]
        [Authorize(Policy = PermissionPolicies.ManageSettings)]
        public async Task<IActionResult> UpdateCompanyTax(int id, [FromBody] CompanyTaxDto companyTaxDto)
        {
            if (id != companyTaxDto.Id)
            {
                _logger.LogWarning("API: Mismatched ID in update request. Path ID: {PathId}, DTO ID: {DtoId}", id, companyTaxDto.Id);
                return BadRequest("ID mismatch between route parameter and DTO.");
            }

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("API: Invalid model state for updating company tax ID: {Id}", id);
                return BadRequest(ModelState);
            }

            _logger.LogInformation("API: Updating company tax with ID: {Id}", id);
            try
            {
                var success = await _companyTaxService.UpdateAsync(id, companyTaxDto);
                if (!success)
                {
                    _logger.LogWarning("API: Company tax with ID: {Id} not found for update, or update failed.", id);
                    return NotFound($"Company tax with ID {id} not found or update failed.");
                }
                return NoContent(); // Standard response for successful PUT
            }
            catch (DbUpdateConcurrencyException ex)
            {
                 _logger.LogError(ex, "API: Concurrency error updating company tax ID: {Id}", id);
                return StatusCode(409, "The company tax was modified by another user. Please refresh and try again.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API: Error updating company tax ID: {Id}", id);
                return StatusCode(500, $"Internal server error while updating company tax with ID {id}.");
            }
        }

        // DELETE: api/companytaxes/{id}
        [HttpDelete("{id}")]
        [Authorize(Policy = PermissionPolicies.ManageSettings)]
        public async Task<IActionResult> DeleteCompanyTax(int id)
        {
            _logger.LogInformation("API: Deleting company tax with ID: {Id}", id);
            try
            {
                var success = await _companyTaxService.DeleteAsync(id);
                if (!success)
                {
                    _logger.LogWarning("API: Company tax with ID: {Id} not found for deletion.", id);
                    return NotFound($"Company tax with ID {id} not found.");
                }
                return NoContent(); // Standard response for successful DELETE
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API: Error deleting company tax ID: {Id}", id);
                return StatusCode(500, $"Internal server error while deleting company tax with ID {id}.");
            }
        }
    }
}
