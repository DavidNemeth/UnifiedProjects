using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UPortal.Data;
using UPortal.Data.Models;
using UPortal.Dtos;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace UPortal.Services
{
    public class CompanyTaxService : ICompanyTaxService
    {
        private readonly ApplicationDbContext _context;
        private readonly IMapper _mapper;
        private readonly ILogger<CompanyTaxService> _logger;

        public CompanyTaxService(ApplicationDbContext context, IMapper mapper, ILogger<CompanyTaxService> logger)
        {
            _context = context;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<IEnumerable<CompanyTaxDto>> GetAllAsync()
        {
            _logger.LogInformation("Fetching all company taxes.");
            var taxes = await _context.CompanyTaxes.AsNoTracking().ToListAsync();
            return _mapper.Map<IEnumerable<CompanyTaxDto>>(taxes);
        }

        public async Task<CompanyTaxDto?> GetByIdAsync(int id)
        {
            _logger.LogInformation("Fetching company tax with ID: {Id}", id);
            var tax = await _context.CompanyTaxes.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id);
            if (tax == null)
            {
                _logger.LogWarning("Company tax with ID: {Id} not found.", id);
                return null;
            }
            return _mapper.Map<CompanyTaxDto>(tax);
        }

        public async Task<CompanyTaxDto> CreateAsync(CompanyTaxDto companyTaxDto)
        {
            _logger.LogInformation("Creating new company tax with name: {Name}", companyTaxDto.Name);
            var taxEntity = _mapper.Map<CompanyTax>(companyTaxDto);

            taxEntity.Id = 0; // Ensure EF Core treats it as a new entity for auto-increment PK

            _context.CompanyTaxes.Add(taxEntity);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Successfully created company tax with ID: {Id}", taxEntity.Id);
            return _mapper.Map<CompanyTaxDto>(taxEntity);
        }

        public async Task<bool> UpdateAsync(int id, CompanyTaxDto companyTaxDto)
        {
            _logger.LogInformation("Attempting to update company tax with ID: {Id}", id);
            if (id != companyTaxDto.Id)
            {
                _logger.LogWarning("Mismatched ID in update request. Path ID: {PathId}, DTO ID: {DtoId}", id, companyTaxDto.Id);
                return false;
            }

            // Use AsNoTracking for fetching to avoid conflicts if the entity is already tracked.
            // The mapper will create a new instance or update an existing one that will then be tracked.
            var existingEntity = await _context.CompanyTaxes.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id);
            if (existingEntity == null)
            {
                _logger.LogWarning("Company tax with ID: {Id} not found for update.", id);
                return false;
            }

            var updatedEntity = _mapper.Map<CompanyTax>(companyTaxDto);
            _context.Entry(updatedEntity).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("Successfully updated company tax with ID: {Id}", id);
                return true;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogError(ex, "Concurrency exception while updating company tax ID: {Id}.", id);
                if (!await _context.CompanyTaxes.AnyAsync(e => e.Id == id))
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
            var taxEntity = await _context.CompanyTaxes.FindAsync(id);
            if (taxEntity == null)
            {
                _logger.LogWarning("Company tax with ID: {Id} not found for deletion.", id);
                return false;
            }

            _context.CompanyTaxes.Remove(taxEntity);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Successfully deleted company tax with ID: {Id}", id);
            return true;
        }
    }
}
