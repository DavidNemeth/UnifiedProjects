using System;
using System.Collections.Generic;
using System.Linq;
using USheets.Models;
using Xunit;

namespace USheets.Tests
{
    public static class TimesheetValidationUtil
    {
        public static double CalculateEntryTotalHours(double normalHours, double overtimeHours)
        {
            return normalHours + overtimeHours;
        }

        // Validates if combined hours for a day are within the allowed limit (e.g., 24 hours)
        public static bool IsDailyHoursValid(double normalHours, double overtimeHours)
        {
            return (normalHours + overtimeHours) <= 24;
        }
         public static bool IsDailyHoursValid(TimesheetEntry entry)
        {
            return (entry.NormalHours + entry.OvertimeHours) <= 24;
        }


        public static double CalculateTotalWeeklyHours(List<TimesheetEntry>? entries)
        {
            if (entries == null) return 0;
            return entries.Sum(e => e.NormalHours + e.OvertimeHours); // Assuming TotalHours is not yet set
        }

        public static double CalculateTotalWeeklyHoursFromEntryTotals(List<TimesheetEntry>? entries)
        {
            if (entries == null) return 0;
            return entries.Sum(e => e.TotalHours);
        }

        // Validates if the total weekly hours are sufficient for submission (e.g., > 0)
        public static bool IsTotalWeeklyHoursValidForSubmission(double totalWeeklyHours)
        {
            return totalWeeklyHours > 0;
        }
    }

    public class TimesheetValidationTests
    {
        [Fact]
        public void Test_CalculateEntryTotalHours_CorrectlySumsNormalAndOvertime()
        {
            // Arrange
            double normalHours = 8;
            double overtimeHours = 2;
            double expectedTotalHours = 10;

            // Act
            double actualTotalHours = TimesheetValidationUtil.CalculateEntryTotalHours(normalHours, overtimeHours);

            // Assert
            Assert.Equal(expectedTotalHours, actualTotalHours);
        }

        [Theory]
        [InlineData(10, 5, true)] // 15 hours, valid
        [InlineData(20, 4, true)] // 24 hours, valid
        [InlineData(20, 5, false)] // 25 hours, invalid
        [InlineData(0, 0, true)] // 0 hours, valid for a day, but not for submission weekly
        [InlineData(25, 0, false)] // 25 normal hours, invalid
        [InlineData(0, 25, false)] // 25 overtime hours, invalid
        public void Test_HomeRazor_DailyHoursValidation_HandlesOver24Hours(double normalHours, double overtimeHours, bool expectedIsValid)
        {
            // Act
            bool actualIsValid = TimesheetValidationUtil.IsDailyHoursValid(normalHours, overtimeHours);

            // Assert
            Assert.Equal(expectedIsValid, actualIsValid);
        }

        [Fact]
        public void Test_HomeRazor_SubmitValidation_PreventsZeroTotalWeeklyHours()
        {
            // Arrange
            double zeroWeeklyHours = 0;
            double positiveWeeklyHours = 10;

            // Act
            bool isZeroHoursValid = TimesheetValidationUtil.IsTotalWeeklyHoursValidForSubmission(zeroWeeklyHours);
            bool isPositiveHoursValid = TimesheetValidationUtil.IsTotalWeeklyHoursValidForSubmission(positiveWeeklyHours);

            // Assert
            Assert.False(isZeroHoursValid);
            Assert.True(isPositiveHoursValid);
        }

        [Fact]
        public void Test_HomeRazor_TotalWeeklyHours_IsCalculatedCorrectly_FromIndividualHours()
        {
            // Arrange
            var entries = new List<TimesheetEntry>
            {
                new TimesheetEntry { NormalHours = 8, OvertimeHours = 0 }, // 8
                new TimesheetEntry { NormalHours = 7, OvertimeHours = 1 }, // 8
                new TimesheetEntry { NormalHours = 0, OvertimeHours = 0 }, // 0
                new TimesheetEntry { NormalHours = 10, OvertimeHours = 2 }, // 12
                new TimesheetEntry { NormalHours = 5, OvertimeHours = 5 }  // 10
            };
            double expectedTotalWeeklyHours = 38;

            // Act
            double actualTotalWeeklyHours = TimesheetValidationUtil.CalculateTotalWeeklyHours(entries);

            // Assert
            Assert.Equal(expectedTotalWeeklyHours, actualTotalWeeklyHours);
        }

        [Fact]
        public void Test_HomeRazor_TotalWeeklyHours_IsCalculatedCorrectly_FromEntryTotals()
        {
            // Arrange
            var entries = new List<TimesheetEntry>
            {
                new TimesheetEntry { TotalHours = 8 },
                new TimesheetEntry { TotalHours = 8 },
                new TimesheetEntry { TotalHours = 0 },
                new TimesheetEntry { TotalHours = 12 },
                new TimesheetEntry { TotalHours = 10 }
            };
            double expectedTotalWeeklyHours = 38;

            // Act
            // Using a slightly different helper to simulate calculation from already set TotalHours
            double actualTotalWeeklyHours = TimesheetValidationUtil.CalculateTotalWeeklyHoursFromEntryTotals(entries);

            // Assert
            Assert.Equal(expectedTotalWeeklyHours, actualTotalWeeklyHours);
        }

        [Fact]
        public void Test_HomeRazor_TotalWeeklyHours_IsZeroForNullOrEmptyEntries()
        {
            // Arrange
            List<TimesheetEntry>? nullEntries = null;
            var emptyEntries = new List<TimesheetEntry>();

            // Act
            double totalForNull = TimesheetValidationUtil.CalculateTotalWeeklyHours(nullEntries);
            double totalForEmpty = TimesheetValidationUtil.CalculateTotalWeeklyHours(emptyEntries);
            double totalForNullFromTotals = TimesheetValidationUtil.CalculateTotalWeeklyHoursFromEntryTotals(nullEntries);
            double totalForEmptyFromTotals = TimesheetValidationUtil.CalculateTotalWeeklyHoursFromEntryTotals(emptyEntries);


            // Assert
            Assert.Equal(0, totalForNull);
            Assert.Equal(0, totalForEmpty);
            Assert.Equal(0, totalForNullFromTotals);
            Assert.Equal(0, totalForEmptyFromTotals);
        }

        // --- Tests for NonStandardHoursWarning ---

        private void SimulateSetNonStandardHoursWarning(TimesheetEntry entry)
        {
            var dayOfWeek = entry.Date.DayOfWeek;
            bool isWeekday = dayOfWeek >= DayOfWeek.Monday && dayOfWeek <= DayOfWeek.Friday;

            if (isWeekday)
            {
                entry.HasNonStandardHoursWarning = (entry.NormalHours != 8 && entry.NormalHours != 0);
            }
            else
            {
                entry.HasNonStandardHoursWarning = false; // No warning for weekends
            }
        }

        [Theory]
        [InlineData(2024, 1, 1, 7, true)] // Monday (Weekday), 7 hours -> Warning
        [InlineData(2024, 1, 1, 8, false)] // Monday (Weekday), 8 hours -> No Warning
        [InlineData(2024, 1, 1, 0, false)] // Monday (Weekday), 0 hours -> No Warning
        [InlineData(2024, 1, 6, 7, false)] // Saturday (Weekend), 7 hours -> No Warning
        [InlineData(2024, 1, 7, 8, false)] // Sunday (Weekend), 8 hours -> No Warning
        [InlineData(2024, 1, 5, 7.5, true)] // Friday (Weekday), 7.5 hours -> Warning
        [InlineData(2024, 1, 5, 8.5, true)] // Friday (Weekday), 8.5 hours -> Warning
        public void Test_TimesheetEntry_HasNonStandardHoursWarning_Logic(int year, int month, int day, double normalHours, bool expectedWarning)
        {
            // Arrange
            var entry = new TimesheetEntry
            {
                Date = new DateTime(year, month, day),
                NormalHours = normalHours
            };

            // Act
            SimulateSetNonStandardHoursWarning(entry);

            // Assert
            Assert.Equal(expectedWarning, entry.HasNonStandardHoursWarning);
        }

        // --- Tests for Weekend and Public Holiday Identification ---

        private bool IsWeekend(DateTime date)
        {
            return date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday;
        }

        [Theory]
        [InlineData(2024, 1, 1, false)] // Monday
        [InlineData(2024, 1, 5, false)] // Friday
        [InlineData(2024, 1, 6, true)]  // Saturday
        [InlineData(2024, 1, 7, true)]  // Sunday
        public void Test_IsWeekend_IdentifiesWeekendsCorrectly(int year, int month, int day, bool expectedIsWeekend)
        {
            // Arrange
            var date = new DateTime(year, month, day);

            // Act
            bool actualIsWeekend = IsWeekend(date);

            // Assert
            Assert.Equal(expectedIsWeekend, actualIsWeekend);
        }

        private readonly List<DateTime> samplePublicHolidays = new List<DateTime>
        {
            new DateTime(2024, 1, 1),   // New Year's Day
            new DateTime(2024, 12, 25), // Christmas Day
            new DateTime(2024, 7, 4)    // Sample US Independence Day
        };

        private bool IsPublicHoliday(DateTime date, List<DateTime> holidays)
        {
            return holidays.Any(ph => ph.Date == date.Date);
        }

        [Theory]
        [InlineData(2024, 1, 1, true)]   // New Year's Day
        [InlineData(2024, 12, 25, true)] // Christmas Day
        [InlineData(2024, 7, 4, true)]   // Sample US Independence Day
        [InlineData(2024, 1, 2, false)]   // Not a public holiday
        [InlineData(2024, 7, 5, false)]   // Not a public holiday
        public void Test_IsPublicHoliday_IdentifiesPublicHolidaysCorrectly(int year, int month, int day, bool expectedIsPublicHoliday)
        {
            // Arrange
            var date = new DateTime(year, month, day);

            // Act
            bool actualIsPublicHoliday = IsPublicHoliday(date, samplePublicHolidays);

            // Assert
            Assert.Equal(expectedIsPublicHoliday, actualIsPublicHoliday);
        }

        // --- Tests for Submit Button Text/State Logic (Simulation) ---

        private string SimulateGetSubmitButtonText(TimesheetStatus status)
        {
            return status switch
            {
                TimesheetStatus.Draft => "Submit Timesheet",
                TimesheetStatus.Submitted => "Resubmit Timesheet",
                TimesheetStatus.Approved => "Timesheet Approved",
                TimesheetStatus.Rejected => "Resubmit Rejected Timesheet",
                _ => "Submit Timesheet"
            };
        }

        private bool SimulateIsSubmitButtonDisabled(TimesheetStatus status)
        {
            return status == TimesheetStatus.Approved;
        }

        [Theory]
        [InlineData(TimesheetStatus.Draft, "Submit Timesheet", false)]
        [InlineData(TimesheetStatus.Submitted, "Resubmit Timesheet", false)]
        [InlineData(TimesheetStatus.Approved, "Timesheet Approved", true)]
        [InlineData(TimesheetStatus.Rejected, "Resubmit Rejected Timesheet", false)]
        public void Test_SubmitButtonLogic_Simulated(TimesheetStatus status, string expectedText, bool expectedDisabled)
        {
            // Act
            string actualText = SimulateGetSubmitButtonText(status);
            bool actualDisabled = SimulateIsSubmitButtonDisabled(status);

            // Assert
            Assert.Equal(expectedText, actualText);
            Assert.Equal(expectedDisabled, actualDisabled);
        }
    }
}
