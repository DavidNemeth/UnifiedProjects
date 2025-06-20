
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
                return StatusCode(StatusCodes.Status409Conflict, "The timesheet entry was modified by another user. Please reload and try again.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving timesheet entry with id {Id}.", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "Error approving timesheet entry.");
            }
        }

        // POST: api/Timesheet/{id}/reject
        [HttpPost("{id}/reject")]
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
                return StatusCode(StatusCodes.Status409Conflict, "The timesheet entry was modified by another user. Please reload and try again.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rejecting timesheet entry with id {Id}.", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "Error rejecting timesheet entry.");
            }
        }

        // GET: api/Timesheet/pending-approval
        [HttpGet("pending-approval")]
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

            // Retrieve the existing entry to check its status
            var existingEntry = await _context.TimesheetEntries.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id);
            if (existingEntry == null)
            {
                // This case should ideally be caught by TimesheetEntryExists later,
                // but good to handle explicitly if we're checking status.
                return NotFound();
            }

            // Check if the existing entry is Approved or Submitted
            if (existingEntry.Status == TimesheetStatus.Approved || existingEntry.Status == TimesheetStatus.Submitted)
            {
                _logger.LogWarning("Attempt to modify an {Status} timesheet entry with id {Id}.", existingEntry.Status, id);
                return BadRequest("Approved or Submitted timesheets cannot be modified.");
            }

            // Prevent changing status of other statuses to Draft or New if it's not already Draft/New - this seems overly restrictive.
            // The primary goal is to protect Approved/Submitted.
            // If an entry is, say, Rejected, it should be fine to move it back to Draft.
            // The original requirement: "ensure that the incoming timesheetEntry.Status cannot be changed to Draft or New if the existing status is Approved or Submitted."
            // This is already covered by the check above. If existing is Approved/Submitted, we don't proceed.

            _context.Entry(timesheetEntry).State = EntityState.Modified;

            try
            {
                // Ensure the status from the payload is respected, unless it's an invalid transition
                // For now, the main protection is for Approved/Submitted.
                // If timesheetEntry.Status is different from existingEntry.Status, it will be updated.
                // We might need more granular status transition rules later, but this meets current req.

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
                    _logger.LogError(id, "Concurrency error while updating timesheet entry with id {Id}", id);
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
                // Check if the week for the new entry is already Approved or Submitted
                var entriesForDate = await _context.TimesheetEntries
                                                   .Where(e => e.Date.Date == timesheetEntry.Date.Date)
                                                   .ToListAsync();

                if (entriesForDate.Any(e => e.Status == TimesheetStatus.Approved || e.Status == TimesheetStatus.Submitted))
                {
                    _logger.LogWarning("Attempt to add a new entry to an Approved or Submitted week on {Date}.", timesheetEntry.Date.Date);
                    return BadRequest("Cannot add new entries to an Approved or Submitted week.");
                }

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

            // ModelState validation should occur before any other logic
            if (!ModelState.IsValid) // Validates each entry in the list
            {
                return BadRequest(ModelState);
            }

            // First, check if the week already contains Approved or Submitted entries
            var entriesToCheckStatus = await _context.TimesheetEntries
                                                .Where(e => e.Date.Date == weekStartDate.Date)
                                                .AsNoTracking() // No need to track these for this check
                                                .ToListAsync();

            if (entriesToCheckStatus.Any(e => e.Status == TimesheetStatus.Approved || e.Status == TimesheetStatus.Submitted))
            {
                _logger.LogWarning("Attempt to modify an Approved or Submitted week starting {WeekStartDate}.", weekStartDate.Date);
                return BadRequest("Approved or Submitted timesheets cannot be modified.");
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                if (entries == null || !entries.Any())
                {
                    // Handle as deleting all entries for the week
                    var existingEntriesToDelete = await _context.TimesheetEntries
                                                        .Where(e => e.Date.Date == weekStartDate.Date)
                                                        .ToListAsync();
                    if (existingEntriesToDelete.Any())
                    {
                        _context.TimesheetEntries.RemoveRange(existingEntriesToDelete);
                        await _context.SaveChangesAsync();
                    }
                    await transaction.CommitAsync(); // Commit transaction after successful deletion
                    return Ok(new List<TimesheetEntry>()); // Return OK with empty list
                }

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
