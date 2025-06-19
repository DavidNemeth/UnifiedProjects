using USheets.Models;
using Microsoft.JSInterop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace USheets.Services
{
    public class LocalStorageTimesheetService : ITimesheetService
    {
        private readonly IJSRuntime _jsRuntime;

        public LocalStorageTimesheetService(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime;
        }

        private string GetLocalStorageKey(DateTime weekStart)
        {
            return $"timesheet-{weekStart:yyyy-MM-dd}";
        }

        public async Task<List<TimesheetEntry>?> GetTimesheetEntriesAsync(DateTime weekStartDate)
        {
            var key = GetLocalStorageKey(weekStartDate);
            try
            {
                var jsonData = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", key);
                if (!string.IsNullOrEmpty(jsonData))
                {
                    // Ensure Hours dictionary is initialized for entries deserialized from older data
                    var entries = JsonSerializer.Deserialize<List<TimesheetEntry>>(jsonData);
                    if (entries != null)
                    {
                        foreach (var entry in entries)
                        {
                            if (entry.Hours == null) // Check if Hours is null (e.g. from old data structure)
                            {
                                entry.Hours = new Dictionary<DayOfWeek, double>
                                {
                                    { DayOfWeek.Sunday, 0 }, { DayOfWeek.Monday, 0 }, { DayOfWeek.Tuesday, 0 },
                                    { DayOfWeek.Wednesday, 0 }, { DayOfWeek.Thursday, 0 }, { DayOfWeek.Friday, 0 },
                                    { DayOfWeek.Saturday, 0 }
                                };
                                // Potentially migrate NormalHours/OvertimeHours to Hours[entry.Date.DayOfWeek] if needed,
                                // but for new structure, Hours dict is primary.
                                // For now, just ensure Hours is not null.
                            }
                        }
                    }
                    return entries;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading timesheet data from local storage: {ex.Message}");
                // Optionally, notify the user or handle corrupted data
            }
            // If no data found or error, Home.razor will create a single blank new entry.
            // This service method should return null or empty list if nothing is found,
            // to let the caller decide how to handle it.
            return null; // Or return new List<TimesheetEntry>();
        }

        public async Task SaveTimesheetEntriesAsync(DateTime weekStartDate, List<TimesheetEntry> entries)
        {
            if (entries == null) return;

            var key = GetLocalStorageKey(weekStartDate);
            try
            {
                var jsonData = JsonSerializer.Serialize(entries);
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", key, jsonData);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving timesheet data to local storage: {ex.Message}");
                // Optionally, notify the user of the save error
            }
        }

        public async Task<List<TimesheetEntry>?> CopyTimesheetEntriesFromPreviousWeekAsync(DateTime currentWeekStartDate, DateTime previousWeekStartDate)
        {
            var previousWeekEntries = await GetTimesheetEntriesAsync(previousWeekStartDate);

            if (previousWeekEntries == null || !previousWeekEntries.Any())
            {
                return new List<TimesheetEntry>(); // Return empty list if nothing to copy
            }

            var currentWeekEntries = new List<TimesheetEntry>();
            foreach (var prevEntry in previousWeekEntries)
            {
                var newEntry = new TimesheetEntry // Constructor initializes Hours dictionary, PayCode, Comments, Status
                {
                    Date = currentWeekStartDate, // Set Date to mark the week this entry belongs to
                    PayCode = prevEntry.PayCode,    // Copy actual PayCode
                    Comments = prevEntry.Comments,  // Copy Comments
                    Status = TimesheetStatus.Draft  // New entries are drafts
                    // NormalHours and OvertimeHours are not explicitly copied as they are deprecated.
                };

                // Explicitly copy the Hours dictionary content from prevEntry to newEntry
                if (prevEntry.Hours != null) // Ensure prevEntry.Hours is not null
                {
                    foreach(var dayHourPair in prevEntry.Hours)
                    {
                        newEntry.Hours[dayHourPair.Key] = dayHourPair.Value;
                    }
                }
                newEntry.TotalHours = newEntry.Hours.Values.Sum(); // Recalculate based on copied hours

                currentWeekEntries.Add(newEntry);
            }

            await SaveTimesheetEntriesAsync(currentWeekStartDate, currentWeekEntries);
            return currentWeekEntries;
        }
    }
}
