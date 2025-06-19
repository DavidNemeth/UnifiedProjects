using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace USheets.Models
{
    public class TimesheetEntry
    {
        // --- Properties for UI logic only ---
        [JsonIgnore]
        public Action? AutosaveAction { get; set; }

        [JsonIgnore]
        public double TotalHours { get; set; }

        [JsonIgnore]
        public bool HasNonStandardHoursWarning { get; set; } = false;

        // --- Deprecated properties that should not be sent to the API ---
        [JsonIgnore]
        public double NormalHours { get; set; }

        [JsonIgnore]
        public double OvertimeHours { get; set; }

        // --- Core Data Properties for the API ---
        public DateTime Date { get; set; }
        public string PayCode { get; set; } = "Regular";
        public Dictionary<DayOfWeek, double> Hours { get; set; }
        public TimesheetStatus Status { get; set; }

        private string _comments = string.Empty;
        public string Comments
        {
            get => _comments;
            set
            {
                if (_comments != value)
                {
                    _comments = value;
                    AutosaveAction?.Invoke();
                }
            }
        }

        // Constructor to initialize the object in a valid state
        public TimesheetEntry()
        {
            Hours = new Dictionary<DayOfWeek, double>
            {
                { DayOfWeek.Monday, 0 },
                { DayOfWeek.Tuesday, 0 },
                { DayOfWeek.Wednesday, 0 },
                { DayOfWeek.Thursday, 0 },
                { DayOfWeek.Friday, 0 },
                { DayOfWeek.Saturday, 0 },
                { DayOfWeek.Sunday, 0 }
            };
        }
    }
}