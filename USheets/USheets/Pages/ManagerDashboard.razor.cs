using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using USheets.Models;
using USheets.Services; // Assuming ITimesheetService and IUserService are here

namespace USheets.Pages
{
    [Authorize(Roles = "Manager")]
    public partial class ManagerDashboard : ComponentBase
    {
        [Inject]
        protected ITimesheetService? TimesheetService { get; set; }

        // [Inject] // TODO: Uncomment when IUserService is available
        // protected IUserService? UserService { get; set; }

        protected List<TimesheetEntryViewModel>? pendingTimesheets; // Using a ViewModel
        protected bool isLoading = true;
        protected string? errorMessage;

        // For rejection modal
        protected bool showRejectionModal = false;
        protected int timesheetIdToReject;
        protected string rejectionInput = "";
        protected string? rejectionModalErrorMessage;

        protected override async Task OnInitializedAsync()
        {
            await LoadPendingTimesheets();
        }

        protected async Task LoadPendingTimesheets()
        {
            isLoading = true;
            errorMessage = null;
            try
            {
                if (TimesheetService == null)
                {
                    errorMessage = "Timesheet service is not available.";
                    isLoading = false;
                    return;
                }

                var timesheetsFromApi = await TimesheetService.GetPendingApprovalTimesheetsAsync();
                if (timesheetsFromApi != null)
                {
                    // TODO: Enhance with UserService to get Employee Names
                    // For now, we'll create a simple ViewModel or use TimesheetEntry directly
                    // if it has all necessary fields (like UserId to display for now)
                    pendingTimesheets = timesheetsFromApi.Select(ts => new TimesheetEntryViewModel
                    {
                        Id = ts.Id, // Assuming TimesheetEntry from API has an Id
                        UserId = ts.UserId, // Assuming TimesheetEntry from API has a UserId or similar
                        Date = ts.Date,
                        TotalHours = ts.TotalHours,
                        Status = ts.Status,
                        RejectionReason = ts.RejectionReason
                    }).ToList();
                }
                else
                {
                    pendingTimesheets = new List<TimesheetEntryViewModel>();
                }
            }
            catch (Exception ex)
            {
                // Log exception (not shown here for brevity)
                errorMessage = $"An error occurred while loading timesheets: {ex.Message}";
            }
            finally
            {
                isLoading = false;
            }
        }

        protected async Task ApproveTimesheetClicked(int timesheetId)
        {
            errorMessage = null;
            try
            {
                if (TimesheetService == null)
                {
                    errorMessage = "Timesheet service is not available.";
                    return;
                }
                var updatedTimesheet = await TimesheetService.ApproveTimesheetAsync(timesheetId);
                if (updatedTimesheet != null)
                {
                    // Remove from local list or reload
                    var timesheet = pendingTimesheets?.FirstOrDefault(ts => ts.Id == timesheetId);
                    if (timesheet != null)
                    {
                        pendingTimesheets?.Remove(timesheet);
                    }
                    // Or uncomment to reload all:
                    // await LoadPendingTimesheets();
                }
                else
                {
                    errorMessage = "Failed to approve the timesheet. It might have been modified, is no longer in a submittable state, or an API error occurred.";
                    // Optionally, reload to get the latest state, as the item might have been processed by another manager
                    await LoadPendingTimesheets();
                }
            }
            catch (Exception ex)
            {
                errorMessage = $"An error occurred while approving the timesheet: {ex.Message}";
            }
            StateHasChanged(); // Notify Blazor to re-render
        }

        protected void RejectTimesheetClicked(int timesheetId)
        {
            timesheetIdToReject = timesheetId;
            rejectionInput = ""; // Clear previous input
            rejectionModalErrorMessage = null;
            showRejectionModal = true;
        }

        protected void CloseRejectionModal()
        {
            showRejectionModal = false;
            rejectionInput = "";
            rejectionModalErrorMessage = null;
        }

        protected async Task ConfirmRejectTimesheet()
        {
            if (string.IsNullOrWhiteSpace(rejectionInput))
            {
                rejectionModalErrorMessage = "Rejection reason cannot be empty.";
                return;
            }

            errorMessage = null;
            rejectionModalErrorMessage = null;

            try
            {
                if (TimesheetService == null)
                {
                    errorMessage = "Timesheet service is not available.";
                    CloseRejectionModal();
                    return;
                }

                var updatedTimesheet = await TimesheetService.RejectTimesheetAsync(timesheetIdToReject, rejectionInput);
                if (updatedTimesheet != null)
                {
                    // Remove from local list or reload
                     var timesheet = pendingTimesheets?.FirstOrDefault(ts => ts.Id == timesheetIdToReject);
                    if (timesheet != null)
                    {
                        pendingTimesheets?.Remove(timesheet);
                    }
                    // Or uncomment to reload all:
                    // await LoadPendingTimesheets();
                }
                else
                {
                    errorMessage = "Failed to reject the timesheet. It might have been modified, is no longer in a submittable state, or an API error occurred.";
                    // Optionally, reload to get the latest state
                    await LoadPendingTimesheets();
                }
            }
            catch (Exception ex)
            {
                errorMessage = $"An error occurred while rejecting the timesheet: {ex.Message}";
            }
            finally
            {
                CloseRejectionModal();
                StateHasChanged(); // Notify Blazor to re-render
            }
        }
    }

    // ViewModel to represent a timesheet entry in the dashboard
    // This can be expanded or replaced by your actual TimesheetEntry model if it's suitable
    // and if you have a way to map UserId to UserName.
    public class TimesheetEntryViewModel
    {
        public int Id { get; set; }
        public int UserId { get; set; } 
        public string? EmployeeName { get; set; } // To be populated by IUserService
        public DateTime Date { get; set; }
        public double TotalHours { get; set; }
        public TimesheetStatus Status { get; set; }
        public string? RejectionReason { get; set; }
    }
}
