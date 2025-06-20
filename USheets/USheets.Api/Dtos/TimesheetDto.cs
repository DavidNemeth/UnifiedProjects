using System.ComponentModel.DataAnnotations;
using USheets.Api.Models;

namespace USheets.Api.Dtos
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
        [Required]
        public DateTime WeekStartDate { get; set; }
        public int UserId { get; set; }
        public string? Comments { get; set; }
        public TimesheetStatus Status { get; set; }
        public List<TimesheetLineDto> Lines { get; set; } = new();
    }
}