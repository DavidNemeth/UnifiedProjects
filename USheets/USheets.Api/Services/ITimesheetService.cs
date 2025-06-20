
using USheets.Api.Dtos;

namespace USheets.Api.Services
{
    public interface ITimesheetService
    {
        /// <summary>
        /// Retrieves a weekly timesheet for a specific user and week.
        /// </summary>
        Task<TimesheetDto?> GetTimesheetAsync(int userId, DateTime weekStartDate);

        /// <summary>
        /// Retrieves all timesheets with a 'Submitted' status for manager approval.
        /// </summary>
        Task<IEnumerable<TimesheetDto>> GetPendingApprovalTimesheetsAsync();

        /// <summary>
        /// Creates a new timesheet or updates an existing one for a given week.
        /// This is the primary method for saving a user's weekly work.
        /// </summary>
        Task<TimesheetDto> CreateOrUpdateTimesheetAsync(int userId, TimesheetCreateUpdateDto timesheetDto);

        /// <summary>
        /// Approves a submitted timesheet.
        /// </summary>
        Task<TimesheetDto?> ApproveTimesheetAsync(int timesheetId);

        /// <summary>
        /// Rejects a submitted timesheet with a reason.
        /// </summary>
        Task<TimesheetDto?> RejectTimesheetAsync(int timesheetId, string reason);
    }
}