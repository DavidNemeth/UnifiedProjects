using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Moq.Protected;
using USheets.Models;
using USheets.Services;
using Xunit;

namespace USheets.Tests
{
    public class RestApiTimesheetServiceTests
    {
        private Mock<HttpMessageHandler> CreateMockHandler(HttpResponseMessage responseMessage)
        {
            var mockHandler = new Mock<HttpMessageHandler>();
            mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(responseMessage);
            return mockHandler;
        }

        private HttpClient CreateHttpClient(HttpMessageHandler messageHandler)
        {
            var httpClient = new HttpClient(messageHandler)
            {
                BaseAddress = new Uri("http://localhost/api/") // Base address for testing
            };
            return httpClient;
        }

        [Fact]
        public async Task GetTimesheetEntriesAsync_ReturnsEntries_WhenApiSuccessful()
        {
            // Arrange
            var weekStartDate = new DateTime(2024, 1, 1);
            var expectedEntries = new List<TimesheetEntry>
            {
                new TimesheetEntry { Id = 1, Date = weekStartDate, ProjectName = "Test Project 1" },
                new TimesheetEntry { Id = 2, Date = weekStartDate, ProjectName = "Test Project 2" }
            };
            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(expectedEntries)
            };
            var mockHandler = CreateMockHandler(responseMessage);
            var httpClient = CreateHttpClient(mockHandler.Object);
            var service = new RestApiTimesheetService(httpClient);

            // Act
            var result = await service.GetTimesheetEntriesAsync(weekStartDate);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(expectedEntries.Count, result.Count);
            Assert.Equal(expectedEntries[0].ProjectName, result[0].ProjectName);

            mockHandler.Protected().Verify(
                "SendAsync",
                Times.Exactly(1),
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Get &&
                    req.RequestUri.ToString().Contains($"/api/Timesheet?weekStartDate={weekStartDate:yyyy-MM-dd}")),
                ItExpr.IsAny<CancellationToken>()
            );
        }

        [Fact]
        public async Task GetTimesheetEntriesAsync_ReturnsEmptyList_WhenApiReturnsNoContent()
        {
            // Arrange
            var weekStartDate = new DateTime(2024, 1, 1);
            var responseMessage = new HttpResponseMessage(HttpStatusCode.NoContent);
            var mockHandler = CreateMockHandler(responseMessage);
            var httpClient = CreateHttpClient(mockHandler.Object);
            var service = new RestApiTimesheetService(httpClient);

            // Act
            var result = await service.GetTimesheetEntriesAsync(weekStartDate);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetTimesheetEntriesAsync_ReturnsNull_WhenApiReturnsError()
        {
            // Arrange
            var weekStartDate = new DateTime(2024, 1, 1);
            var responseMessage = new HttpResponseMessage(HttpStatusCode.InternalServerError);
            var mockHandler = CreateMockHandler(responseMessage);
            var httpClient = CreateHttpClient(mockHandler.Object);
            var service = new RestApiTimesheetService(httpClient);

            // Act
            var result = await service.GetTimesheetEntriesAsync(weekStartDate);

            // Assert
            Assert.Null(result);
        }


        [Fact]
        public async Task SaveTimesheetEntriesAsync_CallsPostAsJsonAsync_WithCorrectUrlAndData()
        {
            // Arrange
            var weekStartDate = new DateTime(2024, 1, 8);
            var entriesToSave = new List<TimesheetEntry>
            {
                new TimesheetEntry { ProjectName = "Save Project 1", TotalHours = 8 }
            };
            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK); // Or HttpStatusCode.NoContent depending on API
            var mockHandler = CreateMockHandler(responseMessage);
            var httpClient = CreateHttpClient(mockHandler.Object);
            var service = new RestApiTimesheetService(httpClient);

            // Act
            await service.SaveTimesheetEntriesAsync(weekStartDate, entriesToSave);

            // Assert
            mockHandler.Protected().Verify(
                "SendAsync",
                Times.Exactly(1),
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Post &&
                    req.RequestUri.ToString().Contains($"/api/Timesheet/Week?weekStartDate={weekStartDate:yyyy-MM-dd}") &&
                    req.Content.ReadAsStringAsync().Result.Contains("Save Project 1") // Basic check for content
                ),
                ItExpr.IsAny<CancellationToken>()
            );
        }

        [Fact]
        public async Task SaveTimesheetEntriesAsync_HandlesApiError()
        {
            // Arrange
            var weekStartDate = new DateTime(2024, 1, 8);
            var entriesToSave = new List<TimesheetEntry> { new TimesheetEntry { ProjectName = "P1" } };
            var responseMessage = new HttpResponseMessage(HttpStatusCode.InternalServerError);
            var mockHandler = CreateMockHandler(responseMessage);
            var httpClient = CreateHttpClient(mockHandler.Object);
            var service = new RestApiTimesheetService(httpClient);

            // Act & Assert
            // No exception should be thrown by the service method itself as it catches and logs.
            // We can't easily assert the Console.WriteLine here without more advanced logging mocks.
            // For this test, we primarily ensure it doesn't crash and completes.
            await service.SaveTimesheetEntriesAsync(weekStartDate, entriesToSave);
             mockHandler.Protected().Verify(
                "SendAsync",
                Times.Exactly(1), // Ensure the call was made
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            );
        }

        [Fact]
        public async Task CopyTimesheetEntriesFromPreviousWeekAsync_ReturnsCopiedEntries_WhenApiSuccessful()
        {
            // Arrange
            var currentWeek = new DateTime(2024, 1, 15);
            var previousWeek = new DateTime(2024, 1, 8);
            var expectedCopiedEntries = new List<TimesheetEntry>
            {
                new TimesheetEntry { Id = 3, Date = currentWeek, ProjectName = "Copied Project", Status = TimesheetStatus.Draft }
            };
            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(expectedCopiedEntries)
            };
            var mockHandler = CreateMockHandler(responseMessage);
            var httpClient = CreateHttpClient(mockHandler.Object);
            var service = new RestApiTimesheetService(httpClient);

            // Act
            var result = await service.CopyTimesheetEntriesFromPreviousWeekAsync(currentWeek, previousWeek);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal("Copied Project", result[0].ProjectName);
            Assert.Equal(TimesheetStatus.Draft, result[0].Status);

            mockHandler.Protected().Verify(
                "SendAsync",
                Times.Exactly(1),
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Post &&
                    req.RequestUri.ToString().Contains($"/api/Timesheet/Copy?currentWeekStartDate={currentWeek:yyyy-MM-dd}&previousWeekStartDate={previousWeek:yyyy-MM-dd}") &&
                    req.Content == null // As per current service implementation for this call
                ),
                ItExpr.IsAny<CancellationToken>()
            );
        }

        [Fact]
        public async Task CopyTimesheetEntriesFromPreviousWeekAsync_ReturnsNull_WhenApiReturnsError()
        {
            // Arrange
            var currentWeek = new DateTime(2024, 1, 15);
            var previousWeek = new DateTime(2024, 1, 8);
            var responseMessage = new HttpResponseMessage(HttpStatusCode.InternalServerError);
            var mockHandler = CreateMockHandler(responseMessage);
            var httpClient = CreateHttpClient(mockHandler.Object);
            var service = new RestApiTimesheetService(httpClient);

            // Act
            var result = await service.CopyTimesheetEntriesFromPreviousWeekAsync(currentWeek, previousWeek);

            // Assert
            Assert.Null(result);
        }
    }
}
