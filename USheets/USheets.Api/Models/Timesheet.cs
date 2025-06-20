// USheets.Api.Models/Timesheet.cs

namespace USheets.Api.Models
{
    /// <summary>
    /// Represents a weekly timesheet submission for a user. This is the parent entity.
    /// </summary>
    public class Timesheet
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public DateTime WeekStartDate { get; set; }
        public TimesheetStatus Status { get; set; }
        public string? Comments { get; set; }
        public string? RejectionReason { get; set; }

        // Navigation property to its child entries
        public ICollection<TimesheetLine> Lines { get; set; } = new List<TimesheetLine>();
    }
}