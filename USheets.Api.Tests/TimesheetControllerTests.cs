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

namespace USheets.Api.Tests
{
    public class TimesheetControllerTests
    {
        private DbContextOptions<ApiDbContext> _dbContextOptions;

        public TimesheetControllerTests()
        {
            _dbContextOptions = new DbContextOptionsBuilder<ApiDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()) // Unique name for each test run
                .Options;
        }

        private ApiDbContext CreateContext() => new ApiDbContext(_dbContextOptions);

        private TimesheetController CreateController(ApiDbContext context) => new TimesheetController(context);

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
    }
}
