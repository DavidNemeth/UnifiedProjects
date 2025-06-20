using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using USheets.Api.Controllers;
using USheets.Api.Data;
using USheets.Api.Models;
using Xunit;
using Microsoft.Extensions.Logging;
using Moq;

namespace USheets.Api.Tests
{
    public class TimesheetControllerTests
    {
        private readonly DbContextOptions<ApiDbContext> _dbContextOptions;

        public TimesheetControllerTests()
        {
            _dbContextOptions = new DbContextOptionsBuilder<ApiDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()) // Unique name for each test run
                .Options;
        }

        private ApiDbContext CreateContext() => new ApiDbContext(_dbContextOptions);

        // Updated CreateController to include a mock logger by default
        private TimesheetController CreateController(ApiDbContext context, ILogger<TimesheetController> logger = null)
        {
            var mockLogger = logger ?? new Mock<ILogger<TimesheetController>>().Object;
            return new TimesheetController(context, mockLogger);
        }

        [Fact]
        public async Task GetTimesheetEntries_ReturnsEmptyList_WhenNoEntriesForWeek()
        {
            // Arrange
            using var context = CreateContext();
            var controller = CreateController(context);
            var weekStartDate = new DateTime(2024, 1, 1);

            // Act
            var result = await controller.GetTimesheetEntries(weekStartDate);

            // Assert
            Assert.NotNull(result);
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var entries = Assert.IsAssignableFrom<IEnumerable<TimesheetEntry>>(okResult.Value);
            Assert.Empty(entries);
        }

        [Fact]
        public async Task GetTimesheetEntries_ReturnsEntries_WhenEntriesExistForWeek()
        {
            // Arrange
            using var context = CreateContext();
            var weekStartDate = new DateTime(2024, 1, 1);
            var testEntries = new List<TimesheetEntry>
            {
                new TimesheetEntry { Id = 1, Date = weekStartDate, ProjectName = "Project A", TotalHours = 8 },
                new TimesheetEntry { Id = 2, Date = weekStartDate, ProjectName = "Project B", TotalHours = 4 },
                new TimesheetEntry { Id = 3, Date = new DateTime(2024, 1, 8), ProjectName = "Project C", TotalHours = 7 } // Different week
            };
            context.TimesheetEntries.AddRange(testEntries);
            await context.SaveChangesAsync();

            var controller = CreateController(context);

            // Act
            var result = await controller.GetTimesheetEntries(weekStartDate);

            // Assert
            Assert.NotNull(result);
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var entries = Assert.IsAssignableFrom<IEnumerable<TimesheetEntry>>(okResult.Value);
            Assert.Equal(2, entries.Count());
            Assert.Contains(entries, e => e.ProjectName == "Project A");
            Assert.Contains(entries, e => e.ProjectName == "Project B");
        }

        [Fact]
        public async Task GetTimesheetEntries_BadRequest_WhenWeekStartDateIsMinValue()
        {
            // Arrange
            using var context = CreateContext();
            var controller = CreateController(context);

            // Act
            var result = await controller.GetTimesheetEntries(DateTime.MinValue);

            // Assert
            Assert.NotNull(result);
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal("weekStartDate is required.", badRequestResult.Value);
        }

        [Fact]
        public async Task PostTimesheetEntries_SavesNewEntriesAndRemovesOld()
        {
            // Arrange
            using var context = CreateContext();
            var weekStartDate = new DateTime(2024, 1, 15);
            var existingEntry = new TimesheetEntry { Id = 1, Date = weekStartDate, ProjectName = "Old Project", TotalHours = 5 };
            context.TimesheetEntries.Add(existingEntry);
            await context.SaveChangesAsync();

            var controller = CreateController(context);
            var newEntriesToSave = new List<TimesheetEntry>
            {
                new TimesheetEntry { ProjectName = "New Project 1", PayCode = "PC1", Hours = new Dictionary<DayOfWeek, double> { { DayOfWeek.Monday, 8 } }, TotalHours = 8 },
                new TimesheetEntry { ProjectName = "New Project 2", PayCode = "PC2", Hours = new Dictionary<DayOfWeek, double> { { DayOfWeek.Tuesday, 4 } }, TotalHours = 4 }
            };

            // Act
            var result = await controller.PostTimesheetEntries(weekStartDate, newEntriesToSave);

            // Assert
            Assert.NotNull(result);
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnedEntries = Assert.IsAssignableFrom<List<TimesheetEntry>>(okResult.Value);
            Assert.Equal(2, returnedEntries.Count);

            var dbEntries = await context.TimesheetEntries.Where(e => e.Date.Date == weekStartDate.Date).ToListAsync();
            Assert.Equal(2, dbEntries.Count);
            Assert.DoesNotContain(dbEntries, e => e.ProjectName == "Old Project");
            Assert.Contains(dbEntries, e => e.ProjectName == "New Project 1" && e.Date.Date == weekStartDate.Date);
            Assert.Contains(dbEntries, e => e.ProjectName == "New Project 2" && e.Date.Date == weekStartDate.Date);
            Assert.All(dbEntries, e => Assert.NotEqual(0, e.Id)); // Ensure new IDs were generated
        }

        [Fact]
        public async Task CopyTimesheetEntries_CopiesEntriesToNewWeekAndSetsToDraft()
        {
            // Arrange
            using var context = CreateContext();
            var previousWeekStartDate = new DateTime(2024, 1, 22);
            var currentWeekStartDate = new DateTime(2024, 1, 29);

            var entriesToCopy = new List<TimesheetEntry>
            {
                new TimesheetEntry { Id = 1, Date = previousWeekStartDate, ProjectName = "Copy Project A", PayCode = "CPA", Hours = new Dictionary<DayOfWeek, double>{{DayOfWeek.Wednesday, 7}}, Comments = "Prev comment", Status = TimesheetStatus.Approved, TotalHours = 7 },
                new TimesheetEntry { Id = 2, Date = previousWeekStartDate, ProjectName = "Copy Project B", PayCode = "CPB", Hours = new Dictionary<DayOfWeek, double>{{DayOfWeek.Thursday, 3}}, Comments = "Another prev comment", Status = TimesheetStatus.Submitted, TotalHours = 3 }
            };
            context.TimesheetEntries.AddRange(entriesToCopy);
             // Add an existing entry for the current week to ensure it's removed
            var existingCurrentWeekEntry = new TimesheetEntry { Id = 3, Date = currentWeekStartDate, ProjectName = "Existing Current Week Project", TotalHours = 5 };
            context.TimesheetEntries.Add(existingCurrentWeekEntry);
            await context.SaveChangesAsync();

            var controller = CreateController(context);

            // Act
            var result = await controller.CopyTimesheetEntriesFromPreviousWeek(currentWeekStartDate, previousWeekStartDate);

            // Assert
            Assert.NotNull(result);
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var copiedEntries = Assert.IsAssignableFrom<List<TimesheetEntry>>(okResult.Value);
            Assert.Equal(2, copiedEntries.Count);

            var currentWeekDbEntries = await context.TimesheetEntries.Where(e => e.Date.Date == currentWeekStartDate.Date).ToListAsync();
            Assert.Equal(2, currentWeekDbEntries.Count); // Ensure only copied entries exist
            Assert.DoesNotContain(currentWeekDbEntries, e => e.ProjectName == "Existing Current Week Project");


            foreach (var copiedEntry in copiedEntries)
            {
                Assert.Equal(currentWeekStartDate.Date, copiedEntry.Date.Date);
                Assert.Equal(TimesheetStatus.Draft, copiedEntry.Status);
                Assert.NotEqual(0, copiedEntry.Id); // Should have new IDs
                var original = entriesToCopy.First(e => e.ProjectName == copiedEntry.ProjectName);
                Assert.NotEqual(original.Id, copiedEntry.Id);
                Assert.Equal(original.PayCode, copiedEntry.PayCode);
                Assert.Equal(original.Comments, copiedEntry.Comments);
                Assert.Equal(original.Hours, copiedEntry.Hours);
            }
        }

        [Fact]
        public async Task CopyTimesheetEntries_ReturnsNotFound_WhenPreviousWeekHasNoEntries()
        {
            // Arrange
            using var context = CreateContext();
            var previousWeekStartDate = new DateTime(2024, 2, 5);
            var currentWeekStartDate = new DateTime(2024, 2, 12);
            var controller = CreateController(context);

            // Act
            var result = await controller.CopyTimesheetEntriesFromPreviousWeek(currentWeekStartDate, previousWeekStartDate);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<NotFoundObjectResult>(result.Result);
        }

        // --- Tests for Deleting All Timesheet Lines ---
        [Fact]
        public async Task PostTimesheetEntries_WithEmptyList_DeletesExistingEntriesAndReturnsOk()
        {
            // Arrange
            using var context = CreateContext();
            var weekStartDate = new DateTime(2024, 3, 4); // Example date
            var existingEntries = new List<TimesheetEntry>
            {
                new TimesheetEntry { Id = 1, Date = weekStartDate, ProjectName = "Project X", TotalHours = 8, Status = TimesheetStatus.Draft },
                new TimesheetEntry { Id = 2, Date = weekStartDate, ProjectName = "Project Y", TotalHours = 4, Status = TimesheetStatus.Draft }
            };
            context.TimesheetEntries.AddRange(existingEntries);
            await context.SaveChangesAsync();

            var controller = CreateController(context);

            // Act
            var result = await controller.PostTimesheetEntries(weekStartDate, new List<TimesheetEntry>());

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnedEntries = Assert.IsAssignableFrom<List<TimesheetEntry>>(okResult.Value);
            Assert.Empty(returnedEntries);

            var dbEntries = await context.TimesheetEntries.Where(e => e.Date.Date == weekStartDate.Date).ToListAsync();
            Assert.Empty(dbEntries);
        }

        // --- Tests for Uneditable Approved/Submitted Timesheets ---

        [Fact]
        public async Task PostTimesheetEntries_WhenWeekIsApproved_ReturnsBadRequest()
        {
            // Arrange
            using var context = CreateContext();
            var weekStartDate = new DateTime(2024, 3, 11);
            context.TimesheetEntries.Add(new TimesheetEntry { Id = 1, Date = weekStartDate, ProjectName = "Approved Project", TotalHours = 8, Status = TimesheetStatus.Approved });
            await context.SaveChangesAsync();

            var controller = CreateController(context);
            var newEntries = new List<TimesheetEntry> { new TimesheetEntry { Date = weekStartDate, ProjectName = "New Data" } };

            // Act
            var result = await controller.PostTimesheetEntries(weekStartDate, newEntries);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal("Approved or Submitted timesheets cannot be modified.", badRequestResult.Value);
        }

        [Fact]
        public async Task PostTimesheetEntries_WhenWeekIsSubmitted_ReturnsBadRequest()
        {
            // Arrange
            using var context = CreateContext();
            var weekStartDate = new DateTime(2024, 3, 18);
            context.TimesheetEntries.Add(new TimesheetEntry { Id = 1, Date = weekStartDate, ProjectName = "Submitted Project", TotalHours = 8, Status = TimesheetStatus.Submitted });
            await context.SaveChangesAsync();

            var controller = CreateController(context);
            var newEntries = new List<TimesheetEntry> { new TimesheetEntry { Date = weekStartDate, ProjectName = "New Data" } };

            // Act
            var result = await controller.PostTimesheetEntries(weekStartDate, newEntries);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal("Approved or Submitted timesheets cannot be modified.", badRequestResult.Value);
        }

        [Fact]
        public async Task PutTimesheetEntry_WhenEntryIsApproved_ReturnsBadRequest()
        {
            // Arrange
            using var context = CreateContext();
            var entryId = 1;
            var entryDate = new DateTime(2024, 3, 25);
            context.TimesheetEntries.Add(new TimesheetEntry { Id = entryId, Date = entryDate, ProjectName = "Approved Entry", TotalHours = 8, Status = TimesheetStatus.Approved });
            await context.SaveChangesAsync();
            // Detach to avoid tracking issues when AsNoTracking is used in controller and then we try to update
            var existingEntry = context.TimesheetEntries.Local.Single(e => e.Id == entryId);
            context.Entry(existingEntry).State = EntityState.Detached;


            var controller = CreateController(context);
            var updatedEntry = new TimesheetEntry { Id = entryId, Date = entryDate, ProjectName = "Updated Name", TotalHours = 9, Status = TimesheetStatus.Approved };


            // Act
            var result = await controller.PutTimesheetEntry(entryId, updatedEntry);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal("Approved or Submitted timesheets cannot be modified.", badRequestResult.Value);
        }

        [Fact]
        public async Task PutTimesheetEntry_WhenEntryIsSubmitted_ReturnsBadRequest()
        {
            // Arrange
            using var context = CreateContext();
            var entryId = 1;
            var entryDate = new DateTime(2024, 4, 1);
            context.TimesheetEntries.Add(new TimesheetEntry { Id = entryId, Date = entryDate, ProjectName = "Submitted Entry", TotalHours = 8, Status = TimesheetStatus.Submitted });
            await context.SaveChangesAsync();
            var existingEntry = context.TimesheetEntries.Local.Single(e => e.Id == entryId);
            context.Entry(existingEntry).State = EntityState.Detached;


            var controller = CreateController(context);
            var updatedEntry = new TimesheetEntry { Id = entryId, Date = entryDate, ProjectName = "Updated Name", TotalHours = 9, Status = TimesheetStatus.Submitted };

            // Act
            var result = await controller.PutTimesheetEntry(entryId, updatedEntry);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal("Approved or Submitted timesheets cannot be modified.", badRequestResult.Value);
        }

        [Fact]
        public async Task PostTimesheetEntry_WhenWeekIsApproved_ReturnsBadRequest()
        {
            // Arrange
            using var context = CreateContext();
            var entryDate = new DateTime(2024, 4, 8);
            context.TimesheetEntries.Add(new TimesheetEntry { Id = 1, Date = entryDate, ProjectName = "Existing Approved", TotalHours = 8, Status = TimesheetStatus.Approved });
            await context.SaveChangesAsync();

            var controller = CreateController(context);
            var newEntry = new TimesheetEntry { Date = entryDate, ProjectName = "New Entry to Approved Week", TotalHours = 2, Status = TimesheetStatus.Draft };

            // Act
            var result = await controller.PostTimesheetEntry(newEntry);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal("Cannot add new entries to an Approved or Submitted week.", badRequestResult.Value);
        }

        [Fact]
        public async Task PostTimesheetEntry_WhenWeekIsSubmitted_ReturnsBadRequest()
        {
            // Arrange
            using var context = CreateContext();
            var entryDate = new DateTime(2024, 4, 15);
            context.TimesheetEntries.Add(new TimesheetEntry { Id = 1, Date = entryDate, ProjectName = "Existing Submitted", TotalHours = 8, Status = TimesheetStatus.Submitted });
            await context.SaveChangesAsync();

            var controller = CreateController(context);
            var newEntry = new TimesheetEntry { Date = entryDate, ProjectName = "New Entry to Submitted Week", TotalHours = 2, Status = TimesheetStatus.Draft };

            // Act
            var result = await controller.PostTimesheetEntry(newEntry);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal("Cannot add new entries to an Approved or Submitted week.", badRequestResult.Value);
        }
    }
}
