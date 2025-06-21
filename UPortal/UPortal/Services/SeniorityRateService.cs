using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UPortal.Data;
using UPortal.Data.Models;
using UPortal.Dtos;

namespace UPortal.Services
{
    /// <summary>
    /// Implements operations for managing seniority rates.
    /// </summary>
    public class SeniorityRateService : ISeniorityRateService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly ILogger<SeniorityRateService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="SeniorityRateService"/> class.
        /// </summary>
        /// <param name="contextFactory">The database context factory.</param>
        /// <param name="logger">The logger.</param>
        public SeniorityRateService(IDbContextFactory<ApplicationDbContext> contextFactory, ILogger<SeniorityRateService> logger)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<IEnumerable<SeniorityRateDto>> GetAllAsync()
        {
            _logger.LogInformation("Fetching all seniority rates.");
            await using var context = await _contextFactory.CreateDbContextAsync();
            var rates = await context.SeniorityRates
                .OrderBy(sr => sr.Level)
                .ToListAsync();

            return rates.Select(MapToDto);
        }

        /// <inheritdoc />
        public async Task<SeniorityRateDto?> GetByLevelAsync(SeniorityLevelEnum level)
        {
            _logger.LogInformation("Fetching seniority rate for level: {Level}", level);
            await using var context = await _contextFactory.CreateDbContextAsync();
            var rate = await context.SeniorityRates
                .FirstOrDefaultAsync(sr => sr.Level == level);

            return rate == null ? null : MapToDto(rate);
        }

        /// <inheritdoc />
        public async Task<SeniorityRateDto> CreateAsync(SeniorityRateDto dto)
        {
            _logger.LogInformation("Attempting to create seniority rate for level: {Level}", dto.Level);
            if (!Enum.TryParse<SeniorityLevelEnum>(dto.Level, true, out var levelEnum))
            {
                _logger.LogWarning("Invalid SeniorityLevelEnum string provided: {LevelString}", dto.Level);
                throw new ArgumentException($"Invalid seniority level string: {dto.Level}.", nameof(dto.Level));
            }

            await using var context = await _contextFactory.CreateDbContextAsync();

            var existingRate = await context.SeniorityRates.FirstOrDefaultAsync(sr => sr.Level == levelEnum);
            if (existingRate != null)
            {
                _logger.LogWarning("Seniority rate for level {Level} already exists.", levelEnum);
                throw new ArgumentException($"A seniority rate for level '{levelEnum}' already exists.", nameof(dto.Level));
            }

            var newRate = new SeniorityRate
            {
                Level = levelEnum,
                DailyRate = dto.DailyRate
            };

            context.SeniorityRates.Add(newRate);
            await context.SaveChangesAsync();

            _logger.LogInformation("Successfully created seniority rate for level: {Level} with ID: {Id}", newRate.Level, newRate.Id);
            return MapToDto(newRate);
        }

        /// <inheritdoc />
        public async Task<SeniorityRateDto?> UpdateAsync(SeniorityLevelEnum level, SeniorityRateDto dto)
        {
            _logger.LogInformation("Attempting to update seniority rate for level: {Level}", level);
            await using var context = await _contextFactory.CreateDbContextAsync();

            var existingRate = await context.SeniorityRates
                .FirstOrDefaultAsync(sr => sr.Level == level);

            if (existingRate == null)
            {
                _logger.LogWarning("Seniority rate for level {Level} not found for update.", level);
                return null; // Or throw KeyNotFoundException as per interface doc, let's stick to returning null for now for PUT
            }

            // The level itself is the key and should not be changed via an update.
            // If dto.Level (string) is different from 'level' (enum), it's confusing.
            // We update the rate for the 'level' parameter.
            existingRate.DailyRate = dto.DailyRate;

            try
            {
                await context.SaveChangesAsync();
                _logger.LogInformation("Successfully updated seniority rate for level: {Level}", existingRate.Level);
                return MapToDto(existingRate);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogError(ex, "Concurrency error while updating seniority rate for level {Level}.", level);
                throw; // Re-throw to be handled by global error handler or controller
            }
        }

        /// <inheritdoc />
        public async Task<bool> DeleteAsync(SeniorityLevelEnum level)
        {
            _logger.LogInformation("Attempting to delete seniority rate for level: {Level}", level);
            await using var context = await _contextFactory.CreateDbContextAsync();

            var rateToDelete = await context.SeniorityRates
                .FirstOrDefaultAsync(sr => sr.Level == level);

            if (rateToDelete == null)
            {
                _logger.LogWarning("Seniority rate for level {Level} not found for deletion.", level);
                return false;
            }

            context.SeniorityRates.Remove(rateToDelete);
            var changes = await context.SaveChangesAsync();

            if (changes > 0)
            {
                _logger.LogInformation("Successfully deleted seniority rate for level: {Level}", level);
                return true;
            }
            else
            {
                _logger.LogWarning("Seniority rate for level {Level} was found but not deleted from database (SaveChanges returned 0).", level);
                return false; // Should not happen if found and Remove was called
            }
        }

        /// <summary>
        /// Maps a <see cref="SeniorityRate"/> entity to a <see cref="SeniorityRateDto"/>.
        /// </summary>
        /// <param name="entity">The entity to map.</param>
        /// <returns>The mapped DTO.</returns>
        private static SeniorityRateDto MapToDto(SeniorityRate entity)
        {
            return new SeniorityRateDto
            {
                Id = entity.Id,
                Level = entity.Level.ToString(),
                DailyRate = entity.DailyRate
            };
        }
    }
}
