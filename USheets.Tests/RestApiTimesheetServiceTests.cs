using System;
using System.Collections.Generic;
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

// TODO: Create a .csproj file for this test project and add necessary package references:
// - Microsoft.NET.Sdk
// - xunit
// - xunit.runner.visualstudio
// - Moq

namespace USheets.Tests
{
    public class RestApiTimesheetServiceTests
    {
        private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
        private readonly HttpClient _httpClient;
        private readonly RestApiTimesheetService _timesheetService;

        public RestApiTimesheetServiceTests()
        {
            _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            _httpClient = new HttpClient(_mockHttpMessageHandler.Object)
            {
                BaseAddress = new Uri("http://localhost/api/") // Base address for the HttpClient
            };
            _timesheetService = new RestApiTimesheetService(_httpClient);
        }

        private void SetupMockHttpResponse(HttpResponseMessage responseMessage)
        {
            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(responseMessage);
        }

        private void VerifyHttpRequest(HttpMethod method, string expectedUri)
        {
            _mockHttpMessageHandler
                .Protected()
                .Verify(
                    "SendAsync",
                    Times.Once(),
                    ItExpr.Is<HttpRequestMessage>(req =>
                        req.Method == method &&
                        req.RequestUri!.ToString() == expectedUri),
                    ItExpr.IsAny<CancellationToken>()
                );
        }

        private void VerifyHttpRequestWithContent<T>(HttpMethod method, string expectedUri, T expectedContent)
        {
            _mockHttpMessageHandler
                .Protected()
                .Verify(
                    "SendAsync",
                    Times.Once(),
                    ItExpr.Is<HttpRequestMessage>(req =>
                        req.Method == method &&
                        req.RequestUri!.ToString() == expectedUri &&
                        ContentMatches(req.Content, expectedContent)
                    ),
                    ItExpr.IsAny<CancellationToken>()
                );
        }

        private bool ContentMatches<T>(HttpContent? httpContent, T expectedContent)
        {
            if (httpContent == null) return false;
            var jsonContent = httpContent.ReadAsStringAsync().Result;
            // Deserialize both to ensure structural equality, ignoring formatting differences.
            // Using a common JsonSerializerOptions if specific ones are used in the service.
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var actualContentObject = JsonSerializer.Deserialize<T>(jsonContent, options);
            var expectedContentJson = JsonSerializer.Serialize(expectedContent, options); // Normalize expected
            var actualContentJson = JsonSerializer.Serialize(actualContentObject, options); // Normalize actual
            return actualContentJson == expectedContentJson;
        }


        [Fact]
        public async Task GetPendingApprovalTimesheetsAsync_Success_ReturnsTimesheetList()
        {
            // Arrange
            var expectedTimesheets = new List<TimesheetEntry>
            {
                new TimesheetEntry { Id = 1, Status = TimesheetStatus.Submitted, UserId = "user1" },
                new TimesheetEntry { Id = 2, Status = TimesheetStatus.Submitted, UserId = "user2" }
            };
            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(expectedTimesheets)
            };
            SetupMockHttpResponse(responseMessage);

            // Act
            var result = await _timesheetService.GetPendingApprovalTimesheetsAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            VerifyHttpRequest(HttpMethod.Get, "http://localhost/api/Timesheet/pending-approval");
        }

        [Fact]
        public async Task GetPendingApprovalTimesheetsAsync_ApiError_ThrowsException()
        {
            // Arrange
            var responseMessage = new HttpResponseMessage(HttpStatusCode.InternalServerError);
            SetupMockHttpResponse(responseMessage);

            // Act & Assert
            await Assert.ThrowsAsync<HttpRequestException>(() => _timesheetService.GetPendingApprovalTimesheetsAsync());
            VerifyHttpRequest(HttpMethod.Get, "http://localhost/api/Timesheet/pending-approval");
        }

        [Fact]
        public async Task ApproveTimesheetAsync_Success_ReturnsUpdatedTimesheet()
        {
            // Arrange
            var timesheetId = 1;
            var expectedTimesheet = new TimesheetEntry { Id = timesheetId, Status = TimesheetStatus.Approved };
            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(expectedTimesheet)
            };
            SetupMockHttpResponse(responseMessage);

            // Act
            var result = await _timesheetService.ApproveTimesheetAsync(timesheetId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(TimesheetStatus.Approved, result.Status);
            VerifyHttpRequest(HttpMethod.Post, $"http://localhost/api/Timesheet/{timesheetId}/approve");
        }

        [Fact]
        public async Task ApproveTimesheetAsync_NotFound_ReturnsNull()
        {
            // Arrange
            var timesheetId = 99;
            // API might return 404 with or without content. Service should handle it.
            // RestApiTimesheetService's ExecuteRequestAsync<T> returns default(T) for non-success if content is empty or not JSON.
            // If it throws for 404, then this test needs to be Assert.ThrowsAsync
            var responseMessage = new HttpResponseMessage(HttpStatusCode.NotFound);
            SetupMockHttpResponse(responseMessage);

            // Act & Assert
            // Based on current ExecuteRequestAsync, non-success that isn't an exception from SendAsync (like network error)
            // will throw HttpRequestException. So, we expect that.
             await Assert.ThrowsAsync<HttpRequestException>(() => _timesheetService.ApproveTimesheetAsync(timesheetId));
            VerifyHttpRequest(HttpMethod.Post, $"http://localhost/api/Timesheet/{timesheetId}/approve");
        }


        [Fact]
        public async Task RejectTimesheetAsync_Success_ReturnsUpdatedTimesheet()
        {
            // Arrange
            var timesheetId = 1;
            var reason = "Incorrect entries";
            var expectedTimesheet = new TimesheetEntry { Id = timesheetId, Status = TimesheetStatus.Rejected, RejectionReason = reason };
            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(expectedTimesheet)
            };
            SetupMockHttpResponse(responseMessage);
            var expectedPayload = new { Reason = reason };


            // Act
            var result = await _timesheetService.RejectTimesheetAsync(timesheetId, reason);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(TimesheetStatus.Rejected, result.Status);
            Assert.Equal(reason, result.RejectionReason);
            VerifyHttpRequestWithContent(HttpMethod.Post, $"http://localhost/api/Timesheet/{timesheetId}/reject", expectedPayload);
        }

        [Fact]
        public async Task RejectTimesheetAsync_ApiError_ThrowsException()
        {
            // Arrange
            var timesheetId = 1;
            var reason = "Test";
            var responseMessage = new HttpResponseMessage(HttpStatusCode.InternalServerError);
            SetupMockHttpResponse(responseMessage);
            var expectedPayload = new { Reason = reason };

            // Act & Assert
            await Assert.ThrowsAsync<HttpRequestException>(() => _timesheetService.RejectTimesheetAsync(timesheetId, reason));
            VerifyHttpRequestWithContent(HttpMethod.Post, $"http://localhost/api/Timesheet/{timesheetId}/reject", expectedPayload);
        }
    }
}
