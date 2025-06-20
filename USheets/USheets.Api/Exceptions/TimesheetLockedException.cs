namespace USheets.Api.Exceptions
{
    /// <summary>
    /// Represents an error that occurs when an attempt is made to modify a timesheet
    /// that is in a locked state (e.g., Approved or Submitted).
    /// </summary>
    public class TimesheetLockedException : InvalidOperationException
    {
        public TimesheetLockedException(string message) : base(message)
        {
        }
    }
}