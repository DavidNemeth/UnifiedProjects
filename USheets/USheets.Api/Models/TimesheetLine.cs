// USheets.Api.Models/TimesheetLine.cs

using System.ComponentModel.DataAnnotations.Schema;

namespace USheets.Api.Models
{
    /// <summary>
    /// Represents a single line item within a weekly Timesheet, detailing hours for a specific pay code.
    /// </summary>
    public class TimesheetLine
    {
        public int Id { get; set; }
        public string? ProjectName { get; set; }
        public string? PayCode { get; set; }
        public Dictionary<DayOfWeek, double> Hours { get; set; } = new();

        [NotMapped] // Calculated property, not stored in the database
        public double TotalHours => Hours?.Values.Sum() ?? 0;

        // Foreign key back to the parent Timesheet
        public int TimesheetId { get; set; }
        public Timesheet Timesheet { get; set; } = null!;
    }
}