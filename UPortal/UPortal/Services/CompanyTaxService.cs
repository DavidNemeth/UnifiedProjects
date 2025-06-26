using Microsoft.EntityFrameworkCore;
using UPortal.Data;
using UPortal.Data.Models;
using UPortal.Dtos;

namespace UPortal.Services
{
    public class CompanyTaxService : ICompanyTaxService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly ILogger<CompanyTaxService> _logger;

        /// <summary>
        /// Initializes a new instance of the CompanyTaxService.
        /// </summary>
        /// <param name="contextFactory">The factory for creating ApplicationDbContext instances.</param>
        /// <param name="logger">The logger for this service.</param>
        public CompanyTaxService(IDbContextFactory<ApplicationDbContext> contextFactory, ILogger<CompanyTaxService> logger)
        {
            _contextFactory = contextFactory;
            _logger = logger;
        }

        public async Task<IEnumerable<CompanyTaxDto>> GetAllAsync()
        {
            _logger.LogInformation("Fetching all company taxes.");
            // Create a new DbContext instance for this operation.
            await using var context = await _contextFactory.CreateDbContextAsync();

            var taxes = await context.CompanyTaxes.AsNoTracking().ToListAsync();

            // Manual mapping from a list of entities to a list of DTOs.
            return taxes.Select(tax => new CompanyTaxDto
            {
                Id = tax.Id,
                Name = tax.Name,
                Rate = tax.Rate,
                Description = tax.Description
            });
        }

        public async Task<CompanyTaxDto?> GetByIdAsync(int id)
        {
            _logger.LogInformation("Fetching company tax with ID: {Id}", id);
            await using var context = await _contextFactory.CreateDbContextAsync();

            var tax = await context.CompanyTaxes.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id);
            if (tax == null)
            {
                _logger.LogWarning("Company tax with ID: {Id} not found.", id);
                return null;
            }

            // Manual mapping from a single entity to a DTO.
            return new CompanyTaxDto
            {
                Id = tax.Id,
                Name = tax.Name,
                Rate = tax.Rate,
                Description = tax.Description
            };
        }

        public async Task<CompanyTaxDto> CreateAsync(CompanyTaxDto companyTaxDto)
        {
            _logger.LogInformation("Creating new company tax with name: {Name}", companyTaxDto.Name);
            await using var context = await _contextFactory.CreateDbContextAsync();

            var taxEntity = new CompanyTax
            {
                Name = companyTaxDto.Name,
                Rate = companyTaxDto.Rate,
                Description = companyTaxDto.Description
            };

            context.CompanyTaxes.Add(taxEntity);
            await context.SaveChangesAsync();

            _logger.LogInformation("Successfully created company tax with ID: {Id}", taxEntity.Id);

            companyTaxDto.Id = taxEntity.Id;
            return companyTaxDto;
        }

        public async Task<bool> UpdateAsync(int id, CompanyTaxDto companyTaxDto)
        {
            _logger.LogInformation("Attempting to update company tax with ID: {Id}", id);
            if (id != companyTaxDto.Id)
            {
                _logger.LogWarning("Mismatched ID in update request. Path ID: {PathId}, DTO ID: {DtoId}", id, companyTaxDto.Id);
                return false;
            }

            await using var context = await _contextFactory.CreateDbContextAsync();
            var existingEntity = await context.CompanyTaxes.FirstOrDefaultAsync(t => t.Id == id);

            if (existingEntity == null)
            {
                _logger.LogWarning("Company tax with ID: {Id} not found for update.", id);
                return false;
            }

            existingEntity.Name = companyTaxDto.Name;
            existingEntity.Rate = companyTaxDto.Rate;
            existingEntity.Description = companyTaxDto.Description;

            try
            {
                await context.SaveChangesAsync();
                _logger.LogInformation("Successfully updated company tax with ID: {Id}", id);
                return true;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogError(ex, "Concurrency exception while updating company tax ID: {Id}.", id);
                if (!await context.CompanyTaxes.AnyAsync(e => e.Id == id))
                {
                    _logger.LogWarning("Company tax with ID: {Id} was not found during concurrency check.", id);
                    return false;
                }
                else
                {
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating company tax with ID: {Id}", id);
                return false;
            }
        }

        public async Task<bool> DeleteAsync(int id)
        {
            _logger.LogInformation("Attempting to delete company tax with ID: {Id}", id);
            await using var context = await _contextFactory.CreateDbContextAsync();

            var taxEntity = await context.CompanyTaxes.FindAsync(id);
            if (taxEntity == null)
            {
                _logger.LogWarning("Company tax with ID: {Id} not found for deletion.", id);
                return false;
            }

            context.CompanyTaxes.Remove(taxEntity);
            await context.SaveChangesAsync();

            _logger.LogInformation("Successfully deleted company tax with ID: {Id}", id);
            return true;
        }
    }
}