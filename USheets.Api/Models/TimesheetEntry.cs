using System;
using System.Collections.Generic;

namespace USheets.Api.Models
{
    public class TimesheetEntry
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public string? ProjectName { get; set; }
        public string? PayCode { get; set; }
        public Dictionary<DayOfWeek, double>? Hours { get; set; } // Stored as JSON string
        public string? Comments { get; set; }
        public TimesheetStatus Status { get; set; }
        public double TotalHours { get; set; }
    }
}
