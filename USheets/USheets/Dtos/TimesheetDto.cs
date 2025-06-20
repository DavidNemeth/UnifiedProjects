using System.Text.Json.Serialization;

namespace USheets.Dtos
{
    public class TimesheetDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public DateTime WeekStartDate { get; set; }
        public TimesheetStatus Status { get; set; }
        public string? Comments { get; set; }
        public string? RejectionReason { get; set; }
        public List<TimesheetLineDto> Lines { get; set; } = new();
        public double TotalHours => Lines.Sum(l => l.TotalHours);
        [JsonIgnore]
        public string? EmployeeName { get; set; }
        [JsonIgnore]
        public bool IsExpanded { get; set; }
    }

    public class TimesheetLineDto
    {
        public int Id { get; set; }
        public string? ProjectName { get; set; }
        public string? PayCode { get; set; }
        public Dictionary<DayOfWeek, double> Hours { get; set; } = new();
        public double TotalHours { get; set; }
    }
    public class TimesheetCreateUpdateDto
    {
        public DateTime WeekStartDate { get; set; }
        public string? Comments { get; set; }
        public TimesheetStatus Status { get; set; }
        public List<TimesheetLineDto> Lines { get; set; } = new();
    }

    public class RejectionReasonModel
    {
        public string? Reason { get; set; }
    }

    public static class PayCodes
    {
        public const string Regular = "Regular";
        public const string Sick = "Sick";
        public const string Overtime = "Overtime";
    }

    public enum TimesheetStatus
    {
        Draft,
        Submitted,
        Approved,
        Rejected
    }
}