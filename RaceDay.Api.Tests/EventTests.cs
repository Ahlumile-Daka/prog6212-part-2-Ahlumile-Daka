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
    public class EventTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly HttpClient _client;

        public EventTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<RaceDayDbContext>));
                    if (descriptor != null) services.Remove(descriptor);

                    services.AddDbContext<RaceDayDbContext>(options =>
                        options.UseInMemoryDatabase("EventTestsDb_" + Guid.NewGuid()));
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

        [Fact]
        public async Task CreateEvent_AsOrganiser_Returns201()
        {
            var (token, _) = await RegisterAndLogin("Organiser");
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _client.PostAsJsonAsync("/api/events", new CreateEventRequest
            {
                EventName = "Cape Town Marathon",
                Description = "Annual marathon",
                EventDate = DateTime.UtcNow.AddMonths(2),
                Location = "Cape Town",
                DistanceKm = 42.2m,
                EventTypeID = 1,
                MaxParticipants = 500
            });

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        [Fact]
        public async Task CreateEvent_AsParticipant_Returns403()
        {
            var (token, _) = await RegisterAndLogin("Participant");
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _client.PostAsJsonAsync("/api/events", new CreateEventRequest
            {
                EventName = "Should Fail",
                EventDate = DateTime.UtcNow.AddMonths(1),
                Location = "Joburg",
                DistanceKm = 10,
                EventTypeID = 1
            });

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task GetEvents_Anonymous_Returns200()
        {
            var response = await _client.GetAsync("/api/events");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task UpdateEvent_NotOwner_Returns403()
        {
            var (token1, _) = await RegisterAndLogin("Organiser");
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token1);

            var createResp = await _client.PostAsJsonAsync("/api/events", new CreateEventRequest
            {
                EventName = "Owned Event",
                EventDate = DateTime.UtcNow.AddMonths(1),
                Location = "Durban",
                DistanceKm = 21.1m,
                EventTypeID = 1
            });
            var created = await createResp.Content.ReadFromJsonAsync<EventDto>();

            var (token2, _) = await RegisterAndLogin("Organiser");
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token2);

            var response = await _client.PutAsJsonAsync($"/api/events/{created!.EventID}", new UpdateEventRequest
            {
                EventName = "Hacked Name"
            });

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }
}
