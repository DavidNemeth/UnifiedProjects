using USheets.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace USheets.Services
{
    public interface ITimesheetService
    {
        Task<List<TimesheetEntry>?> GetTimesheetEntriesAsync(DateTime weekStartDate);
        Task SaveTimesheetEntriesAsync(DateTime weekStartDate, List<TimesheetEntry> entries);
        Task<List<TimesheetEntry>?> CopyTimesheetEntriesFromPreviousWeekAsync(DateTime currentWeekStartDate, DateTime previousWeekStartDate);
    }
}
