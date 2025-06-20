using Microsoft.EntityFrameworkCore;
using USheets.Api.Data;
using USheets.Api.Dtos;
using USheets.Api.Exceptions; // <-- Import the new exception
using USheets.Api.Models;

namespace USheets.Api.Services
{
    public class TimesheetService : ITimesheetService
    {
        private readonly ApiDbContext _context;
        private readonly ILogger<TimesheetService> _logger;

        public TimesheetService(ApiDbContext context, ILogger<TimesheetService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<TimesheetDto?> GetTimesheetAsync(int userId, DateTime weekStartDate)
        {
            // 1. Added entry point logging
            _logger.LogInformation("Attempting to get timesheet for User ID {UserId} and week starting {WeekStartDate}", userId, weekStartDate);

            var timesheet = await _context.Timesheets
                .Include(t => t.Lines)
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.UserId == userId && t.WeekStartDate.Date == weekStartDate.Date);

            if (timesheet == null)
            {
                _logger.LogInformation("No timesheet found for User ID {UserId} and week starting {WeekStartDate}", userId, weekStartDate);
                return null;
            }

            return MapToDto(timesheet);
        }

        public async Task<IEnumerable<TimesheetDto>> GetPendingApprovalTimesheetsAsync()
        {
            _logger.LogInformation("Fetching all timesheets pending approval.");
            var timesheets = await _context.Timesheets
                .Include(t => t.Lines)
                .Where(t => t.Status == TimesheetStatus.Submitted)
                .AsNoTracking()
                .OrderBy(t => t.WeekStartDate)
                .ToListAsync();

            _logger.LogInformation("Found {Count} timesheets pending approval.", timesheets.Count);
            return timesheets.Select(MapToDto);
        }

        public async Task<TimesheetDto> CreateOrUpdateTimesheetAsync(int userId, TimesheetCreateUpdateDto dto)
        {
            _logger.LogInformation("Attempting to create or update timesheet for User ID {UserId} and week starting {WeekStartDate}", userId, dto.WeekStartDate);

            var existingTimesheet = await _context.Timesheets
                .Include(t => t.Lines)
                .FirstOrDefaultAsync(t => t.UserId == userId && t.WeekStartDate.Date == dto.WeekStartDate.Date);

            if (existingTimesheet != null)
            {
                return await UpdateExistingTimesheet(userId, existingTimesheet, dto);
            }
            else
            {
                return await CreateNewTimesheet(userId, dto);
            }
        }

        // 2. Logic broken into private helpers for clarity and wrapped in try-catch
        private async Task<TimesheetDto> UpdateExistingTimesheet(int userId, Timesheet existingTimesheet, TimesheetCreateUpdateDto dto)
        {
            if (existingTimesheet.Status == TimesheetStatus.Approved)
            {
                _logger.LogWarning("User {UserId} attempted to modify an approved timesheet (ID: {TimesheetId})", userId, existingTimesheet.Id);
                // 3. Throwing a more specific, custom exception
                throw new TimesheetLockedException("Approved timesheets cannot be modified.");
            }

            _logger.LogInformation("Updating existing timesheet ID: {TimesheetId} for user {UserId}", existingTimesheet.Id, userId);

            // This pattern is safer and more explicit than RemoveRange + AddRange
            existingTimesheet.Lines.Clear();
            foreach (var lineDto in dto.Lines)
            {
                existingTimesheet.Lines.Add(MapToEntity(lineDto));
            }

            existingTimesheet.Comments = dto.Comments;
            existingTimesheet.Status = dto.Status;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "A database error occurred while updating timesheet {TimesheetId}.", existingTimesheet.Id);
                throw; // Re-throw to be handled by the controller
            }

            return MapToDto(existingTimesheet);
        }

        private async Task<TimesheetDto> CreateNewTimesheet(int userId, TimesheetCreateUpdateDto dto)
        {
            _logger.LogInformation("Creating new timesheet for user {UserId} and week {WeekStartDate}", userId, dto.WeekStartDate);
            var newTimesheet = new Timesheet
            {
                UserId = userId,
                WeekStartDate = dto.WeekStartDate.Date,
                Comments = dto.Comments,
                Status = dto.Status,
                Lines = dto.Lines.Select(lineDto => MapToEntity(lineDto)).ToList()
            };

            _context.Timesheets.Add(newTimesheet);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "A database error occurred while creating a new timesheet for user {UserId}.", userId);
                throw;
            }

            return MapToDto(newTimesheet);
        }

        public async Task<TimesheetDto?> ApproveTimesheetAsync(int timesheetId)
        {
            _logger.LogInformation("Attempting to approve timesheet ID: {TimesheetId}", timesheetId);
            // Switched to FirstOrDefaultAsync to attach the entity for tracking
            var timesheet = await _context.Timesheets.FirstOrDefaultAsync(t => t.Id == timesheetId);

            if (timesheet == null)
            {
                _logger.LogWarning("Approve failed: Timesheet {TimesheetId} not found.", timesheetId);
                return null;
            }
            if (timesheet.Status != TimesheetStatus.Submitted)
            {
                _logger.LogWarning("Approve failed: Timesheet {TimesheetId} is not in 'Submitted' state. Current state: {Status}", timesheetId, timesheet.Status);
                return null;
            }

            timesheet.Status = TimesheetStatus.Approved;
            timesheet.RejectionReason = null;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Timesheet ID: {TimesheetId} has been approved.", timesheetId);
            return await GetTimesheetAsync(timesheet.UserId, timesheet.WeekStartDate);
        }

        public async Task<TimesheetDto?> RejectTimesheetAsync(int timesheetId, string reason)
        {
            _logger.LogInformation("Attempting to reject timesheet ID: {TimesheetId}", timesheetId);
            var timesheet = await _context.Timesheets.FirstOrDefaultAsync(t => t.Id == timesheetId);

            if (timesheet == null)
            {
                _logger.LogWarning("Reject failed: Timesheet {TimesheetId} not found.", timesheetId);
                return null;
            }
            if (timesheet.Status != TimesheetStatus.Submitted)
            {
                _logger.LogWarning("Reject failed: Timesheet {TimesheetId} is not in 'Submitted' state. Current state: {Status}", timesheetId, timesheet.Status);
                return null;
            }

            timesheet.Status = TimesheetStatus.Rejected;
            timesheet.RejectionReason = reason;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Timesheet ID: {TimesheetId} has been rejected with reason.", timesheetId);
            return await GetTimesheetAsync(timesheet.UserId, timesheet.WeekStartDate);
        }

        // --- Helper Methods ---
        private TimesheetDto MapToDto(Timesheet entity) => new()
        {
            Id = entity.Id,
            UserId = entity.UserId,
            WeekStartDate = entity.WeekStartDate,
            Status = entity.Status,
            Comments = entity.Comments,
            RejectionReason = entity.RejectionReason,
            Lines = entity.Lines.Select(line => new TimesheetLineDto
            {
                Id = line.Id,
                PayCode = line.PayCode,
                ProjectName = line.ProjectName,
                Hours = line.Hours,
                TotalHours = line.TotalHours
            }).ToList()
        };

        private TimesheetLine MapToEntity(TimesheetLineDto dto) => new()
        {
            PayCode = dto.PayCode,
            ProjectName = dto.ProjectName,
            Hours = dto.Hours
        };
    }
}