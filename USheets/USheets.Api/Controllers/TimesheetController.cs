
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

        public TimesheetController(ApiDbContext context)
        {
            _context = context;
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
                // Log the exception (not implemented here for brevity)
                return StatusCode(StatusCodes.Status500InternalServerError, "Error retrieving data from the database");
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
                // Log the exception
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
                    // Log the exception
                    return StatusCode(StatusCodes.Status500InternalServerError, "Concurrency error");
                }
            }
            catch (Exception ex)
            {
                // Log the exception
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
                // Log the exception
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
                // Log the exception
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
                // Log the exception
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
                // Log the exception
                return StatusCode(StatusCodes.Status500InternalServerError, "Error deleting data");
            }
        }

        private bool TimesheetEntryExists(int id)
        {
            return _context.TimesheetEntries.Any(e => e.Id == id);
        }
    }
}
