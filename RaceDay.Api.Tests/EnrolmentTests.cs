using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RaceDay.Api.Data;
using RaceDay.Contracts;
using Xunit;

namespace RaceDay.Api.Tests
{
    public class EnrolmentTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly HttpClient _client;

        public EnrolmentTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<RaceDayDbContext>));
                    if (descriptor != null) services.Remove(descriptor);

                    services.AddDbContext<RaceDayDbContext>(options =>
                        options.UseInMemoryDatabase("EnrolmentTestsDb_" + Guid.NewGuid()));
                });
            });
            _client = _factory.CreateClient();
        }

        private async Task<(string token, int userId)> RegisterAndLogin(string role)
        {
            var email = $"{role.ToLower()}_{Guid.NewGuid():N}@test.com";
            await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
            {
                FirstName = "Test",
                LastName = role,
                Email = email,
                Password = "Password123!",
                Role = role
            });

            var loginResp = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest
            {
                Email = email,
                Password = "Password123!"
            });
            var login = await loginResp.Content.ReadFromJsonAsync<LoginResponse>();
            return (login!.Token, login.UserId);
        }

        private async Task<(int eventId, int categoryId)> CreateEventWithCategory(string organiserToken)
        {
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", organiserToken);

            var createResp = await _client.PostAsJsonAsync("/api/events", new CreateEventRequest
            {
                EventName = "Test Race",
                EventDate = DateTime.UtcNow.AddMonths(1),
                Location = "Cape Town",
                DistanceKm = 10,
                EventTypeID = 1,
                MaxParticipants = 100
            });
            var evt = await createResp.Content.ReadFromJsonAsync<EventDto>();

            var catResp = await _client.PostAsJsonAsync($"/api/events/{evt!.EventID}/categories", new CreateCategoryRequest
            {
                CategoryName = "Open",
                EntryFee = 150,
                MaxEntries = 50
            });
            var cat = await catResp.Content.ReadFromJsonAsync<CategoryDto>();

            return (evt.EventID, cat!.CategoryID);
        }

        [Fact]
        public async Task Enrol_AsParticipant_Returns201()
        {
            var (orgToken, _) = await RegisterAndLogin("Organiser");
            var (eventId, categoryId) = await CreateEventWithCategory(orgToken);

            var (partToken, _) = await RegisterAndLogin("Participant");
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", partToken);

            var response = await _client.PostAsJsonAsync("/api/enrolments", new CreateEnrolmentRequest
            {
                EventID = eventId,
                CategoryID = categoryId
            });

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        [Fact]
        public async Task Enrol_AsOrganiser_Returns403()
        {
            var (orgToken, _) = await RegisterAndLogin("Organiser");
            var (eventId, categoryId) = await CreateEventWithCategory(orgToken);

            // same organiser tries to enrol
            var response = await _client.PostAsJsonAsync("/api/enrolments", new CreateEnrolmentRequest
            {
                EventID = eventId,
                CategoryID = categoryId
            });

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task Enrol_Duplicate_Returns409()
        {
            var (orgToken, _) = await RegisterAndLogin("Organiser");
            var (eventId, categoryId) = await CreateEventWithCategory(orgToken);

            var (partToken, _) = await RegisterAndLogin("Participant");
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", partToken);

            await _client.PostAsJsonAsync("/api/enrolments", new CreateEnrolmentRequest
            {
                EventID = eventId,
                CategoryID = categoryId
            });

            var response = await _client.PostAsJsonAsync("/api/enrolments", new CreateEnrolmentRequest
            {
                EventID = eventId,
                CategoryID = categoryId
            });

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }

        [Fact]
        public async Task GetMyEnrolments_AsParticipant_Returns200()
        {
            var (orgToken, _) = await RegisterAndLogin("Organiser");
            var (eventId, categoryId) = await CreateEventWithCategory(orgToken);

            var (partToken, _) = await RegisterAndLogin("Participant");
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", partToken);

            await _client.PostAsJsonAsync("/api/enrolments", new CreateEnrolmentRequest
            {
                EventID = eventId,
                CategoryID = categoryId
            });

            var response = await _client.GetAsync("/api/enrolments/me");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }
}
