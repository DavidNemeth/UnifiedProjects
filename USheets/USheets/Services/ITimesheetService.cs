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

        // Methods for Manager Dashboard
        Task<List<TimesheetEntry>?> GetPendingApprovalTimesheetsAsync();
        Task<TimesheetEntry?> ApproveTimesheetAsync(int timesheetId); // Return updated entry
        Task<TimesheetEntry?> RejectTimesheetAsync(int timesheetId, string reason); // Return updated entry
    }
}
