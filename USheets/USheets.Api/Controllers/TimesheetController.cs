
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using USheets.Api.Data;
using USheets.Api.Models;

namespace USheets.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TimesheetController : ControllerBase
    {
        private readonly ApiDbContext _context;
        private readonly ILogger<TimesheetController> _logger;

        public TimesheetController(ApiDbContext context, ILogger<TimesheetController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: api/Timesheet?weekStartDate=YYYY-MM-DD
        [HttpGet]
        public async Task<ActionResult<IEnumerable<TimesheetEntry>>> GetTimesheetEntries([FromQuery] DateTime weekStartDate)
        {
            if (weekStartDate == DateTime.MinValue)
            {
                return BadRequest("weekStartDate is required.");
            }
            try
            {
                var entries = await _context.TimesheetEntries
                                            .Where(e => e.Date.Date == weekStartDate.Date)
                                            .ToListAsync();
                return Ok(entries);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving timesheet entries for week {WeekStartDate}", weekStartDate);
                return StatusCode(StatusCodes.Status500InternalServerError, "Error retrieving data from the database");
            }
        }

        // POST: api/Timesheet/{id}/approve
        [HttpPost("{id}/approve")]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> ApproveTimesheet(int id)
        {
            try
            {
                var timesheetEntry = await _context.TimesheetEntries.FindAsync(id);

                if (timesheetEntry == null)
                {
                    _logger.LogWarning("Approve failed: Timesheet entry with id {Id} not found.", id);
                    return NotFound($"Timesheet entry with id {id} not found.");
                }

                if (timesheetEntry.Status != TimesheetStatus.Submitted)
                {
                    _logger.LogWarning("Approve failed: Timesheet entry with id {Id} is not in 'Submitted' state. Current state: {Status}", id, timesheetEntry.Status);
                    return BadRequest($"Timesheet entry with id {id} cannot be approved because its status is '{timesheetEntry.Status}'. Only submitted timesheets can be approved.");
                }

                timesheetEntry.Status = TimesheetStatus.Approved;
                timesheetEntry.RejectionReason = null; // Clear any previous rejection reason
                _context.Entry(timesheetEntry).State = EntityState.Modified;

                await _context.SaveChangesAsync();
                _logger.LogInformation("Timesheet entry with id {Id} approved successfully.", id);
                return Ok(timesheetEntry);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogError(ex, "Concurrency error while approving timesheet entry with id {Id}.", id);
                return StatusCode(StatusCodes.Status509Conflict, "The timesheet entry was modified by another user. Please reload and try again.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving timesheet entry with id {Id}.", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "Error approving timesheet entry.");
            }
        }

        // POST: api/Timesheet/{id}/reject
        [HttpPost("{id}/reject")]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> RejectTimesheet(int id, [FromBody] RejectionReasonModel rejectionReasonModel)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var timesheetEntry = await _context.TimesheetEntries.FindAsync(id);

                if (timesheetEntry == null)
                {
                    _logger.LogWarning("Reject failed: Timesheet entry with id {Id} not found.", id);
                    return NotFound($"Timesheet entry with id {id} not found.");
                }

                if (timesheetEntry.Status != TimesheetStatus.Submitted)
                {
                    _logger.LogWarning("Reject failed: Timesheet entry with id {Id} is not in 'Submitted' state. Current state: {Status}", id, timesheetEntry.Status);
                    return BadRequest($"Timesheet entry with id {id} cannot be rejected because its status is '{timesheetEntry.Status}'. Only submitted timesheets can be rejected.");
                }

                timesheetEntry.Status = TimesheetStatus.Rejected;
                timesheetEntry.RejectionReason = rejectionReasonModel.Reason;
                _context.Entry(timesheetEntry).State = EntityState.Modified;

                await _context.SaveChangesAsync();
                _logger.LogInformation("Timesheet entry with id {Id} rejected successfully with reason: {Reason}", id, rejectionReasonModel.Reason);
                return Ok(timesheetEntry);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogError(ex, "Concurrency error while rejecting timesheet entry with id {Id}.", id);
                return StatusCode(StatusCodes.Status509Conflict, "The timesheet entry was modified by another user. Please reload and try again.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rejecting timesheet entry with id {Id}.", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "Error rejecting timesheet entry.");
            }
        }

        // GET: api/Timesheet/pending-approval
        [HttpGet("pending-approval")]
        [Authorize(Roles = "Manager")]
        public async Task<ActionResult<IEnumerable<TimesheetEntry>>> GetPendingApprovalTimesheets()
        {
            try
            {
                var pendingEntries = await _context.TimesheetEntries
                                                   .Where(e => e.Status == TimesheetStatus.Submitted)
                                                   .ToListAsync();

                if (!pendingEntries.Any())
                {
                    _logger.LogInformation("No timesheets found with status 'Submitted'.");
                    return Ok(new List<TimesheetEntry>()); // Return empty list, not NotFound
                }

                return Ok(pendingEntries);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving pending approval timesheets.");
                return StatusCode(StatusCodes.Status500InternalServerError, "Error retrieving data for pending approvals");
            }
        }

        // GET: api/Timesheet/5
        [HttpGet("{id}")]
        public async Task<ActionResult<TimesheetEntry>> GetTimesheetEntry(int id)
        {
            try
            {
                var timesheetEntry = await _context.TimesheetEntries.FindAsync(id);

                if (timesheetEntry == null)
                {
                    return NotFound();
                }

                return timesheetEntry;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving timesheet entry with id {Id}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "Error retrieving data");
            }
        }

        // PUT: api/Timesheet/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutTimesheetEntry(int id, TimesheetEntry timesheetEntry)
        {
            if (id != timesheetEntry.Id)
            {
                return BadRequest("ID mismatch");
            }

            _context.Entry(timesheetEntry).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!TimesheetEntryExists(id))
                {
                    return NotFound();
                }
                else
                {
                    _logger.LogError(ex, "Concurrency error while updating timesheet entry with id {Id}", id);
                    return StatusCode(StatusCodes.Status500InternalServerError, "Concurrency error");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating timesheet entry with id {Id}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "Error saving data");
            }

            return NoContent();
        }

        // POST: api/Timesheet
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<TimesheetEntry>> PostTimesheetEntry(TimesheetEntry timesheetEntry)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                _context.TimesheetEntries.Add(timesheetEntry);
                await _context.SaveChangesAsync();

                return CreatedAtAction(nameof(GetTimesheetEntry), new { id = timesheetEntry.Id }, timesheetEntry);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating new timesheet entry.");
                return StatusCode(StatusCodes.Status500InternalServerError, "Error saving data");
            }
        }

        // POST: api/Timesheet/Week?weekStartDate=YYYY-MM-DD
        [HttpPost("Week")]
        public async Task<ActionResult<IEnumerable<TimesheetEntry>>> PostTimesheetEntries([FromQuery] DateTime weekStartDate, [FromBody] List<TimesheetEntry> entries)
        {
            if (weekStartDate == DateTime.MinValue)
            {
                return BadRequest("weekStartDate is required.");
            }

            if (entries == null || !entries.Any())
            {
                // Or handle as deleting all entries for the week if that's desired
                return BadRequest("Entries list cannot be null or empty.");
            }

            if (!ModelState.IsValid) // Validates each entry in the list
            {
                return BadRequest(ModelState);
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Remove existing entries for the week
                var existingEntries = await _context.TimesheetEntries
                                                    .Where(e => e.Date.Date == weekStartDate.Date)
                                                    .ToListAsync();
                if (existingEntries.Any())
                {
                    _context.TimesheetEntries.RemoveRange(existingEntries);
                }

                // Add new entries
                foreach (var entry in entries)
                {
                    entry.Date = weekStartDate.Date; // Ensure correct date
                    entry.Id = 0; // Ensure EF Core treats them as new entries if Id was passed
                    _context.TimesheetEntries.Add(entry);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(entries);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error saving multiple timesheet entries for week {WeekStartDate}", weekStartDate);
                return StatusCode(StatusCodes.Status500InternalServerError, "Error saving multiple entries.");
            }
        }

        // POST: api/Timesheet/Copy?currentWeekStartDate=YYYY-MM-DD&previousWeekStartDate=YYYY-MM-DD
        [HttpPost("Copy")]
        public async Task<ActionResult<IEnumerable<TimesheetEntry>>> CopyTimesheetEntriesFromPreviousWeek(
            [FromQuery] DateTime currentWeekStartDate,
            [FromQuery] DateTime previousWeekStartDate)
        {
            if (currentWeekStartDate == DateTime.MinValue || previousWeekStartDate == DateTime.MinValue)
            {
                return BadRequest("Both currentWeekStartDate and previousWeekStartDate are required.");
            }

            if (currentWeekStartDate.Date == previousWeekStartDate.Date)
            {
                return BadRequest("Current and previous week start dates cannot be the same.");
            }

            try
            {
                var previousEntries = await _context.TimesheetEntries
                                                    .Where(e => e.Date.Date == previousWeekStartDate.Date)
                                                    .AsNoTracking() // No need to track these for changes
                                                    .ToListAsync();

                if (!previousEntries.Any())
                {
                    return NotFound($"No timesheet entries found for the week starting {previousWeekStartDate:yyyy-MM-dd}.");
                }

                var newEntries = new List<TimesheetEntry>();
                foreach (var prevEntry in previousEntries)
                {
                    var newEntry = new TimesheetEntry
                    {
                        Date = currentWeekStartDate.Date,
                        ProjectName = prevEntry.ProjectName,
                        PayCode = prevEntry.PayCode,
                        Hours = prevEntry.Hours?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value), // Deep copy dictionary
                        Comments = prevEntry.Comments,
                        Status = TimesheetStatus.Draft, // New entries are drafts
                        TotalHours = prevEntry.TotalHours
                        // Id will be generated by the database
                    };
                    newEntries.Add(newEntry);
                }

                // Remove existing entries for the current week before copying
                var existingCurrentWeekEntries = await _context.TimesheetEntries
                                                               .Where(e => e.Date.Date == currentWeekStartDate.Date)
                                                               .ToListAsync();
                if (existingCurrentWeekEntries.Any())
                {
                    _context.TimesheetEntries.RemoveRange(existingCurrentWeekEntries);
                }

                _context.TimesheetEntries.AddRange(newEntries);
                await _context.SaveChangesAsync();

                return Ok(newEntries);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error copying timesheet entries from {PreviousWeekStartDate} to {CurrentWeekStartDate}", previousWeekStartDate, currentWeekStartDate);
                return StatusCode(StatusCodes.Status500InternalServerError, "Error copying timesheet entries.");
            }
        }


        // DELETE: api/Timesheet/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTimesheetEntry(int id)
        {
            try
            {
                var timesheetEntry = await _context.TimesheetEntries.FindAsync(id);
                if (timesheetEntry == null)
                {
                    return NotFound();
                }

                _context.TimesheetEntries.Remove(timesheetEntry);
                await _context.SaveChangesAsync();

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting timesheet entry with id {Id}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "Error deleting data");
            }
        }

        private bool TimesheetEntryExists(int id)
        {
            return _context.TimesheetEntries.Any(e => e.Id == id);
        }
    }
}
