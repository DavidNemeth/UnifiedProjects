using USheets.Dtos;

namespace USheets.Services
{
    public interface ITimesheetService
    {
        /// <summary>
        /// Gets the entire weekly timesheet for a given start date.
        /// Returns null if no timesheet exists for that week.
        /// </summary>
        Task<TimesheetDto?> GetTimesheetAsync(DateTime weekStartDate);

        /// <summary>
        /// Saves a weekly timesheet. This handles both creating a new timesheet
        /// and updating an existing one.
        /// </summary>
        Task<TimesheetDto> SaveTimesheetAsync(TimesheetCreateUpdateDto dto);

        /// <summary>
        /// [Manager] Gets all timesheets pending approval.
        /// </summary>
        Task<List<TimesheetDto>> GetPendingApprovalTimesheetsAsync();

        /// <summary>
        /// [Manager] Approves a timesheet.
        /// </summary>
        Task<TimesheetDto> ApproveTimesheetAsync(int timesheetId);

        /// <summary>
        /// [Manager] Rejects a timesheet with a reason.
        /// </summary>
        Task<TimesheetDto> RejectTimesheetAsync(int timesheetId, string reason);
    }
}