using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RaceDay.Api.Data;
using RaceDay.Contracts;
using Xunit;

namespace RaceDay.Api.Tests
{
    public class AuthTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly HttpClient _client;

        public AuthTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<RaceDayDbContext>));
                    if (descriptor != null) services.Remove(descriptor);

                    services.AddDbContext<RaceDayDbContext>(options =>
                        options.UseInMemoryDatabase("AuthTestsDb_" + Guid.NewGuid()));
                });
            });
            _client = _factory.CreateClient();
        }

        [Fact]
        public async Task Register_ValidOrganiser_Returns201()
        {
            var request = new RegisterRequest
            {
                FirstName = "Test",
                LastName = "Organiser",
                Email = $"org_{Guid.NewGuid():N}@test.com",
                Password = "Password123!",
                Role = "Organiser",
                Phone = "0821234567"
            };

            var response = await _client.PostAsJsonAsync("/api/auth/register", request);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        [Fact]
        public async Task Register_ValidParticipant_Returns201()
        {
            var request = new RegisterRequest
            {
                FirstName = "Test",
                LastName = "Participant",
                Email = $"part_{Guid.NewGuid():N}@test.com",
                Password = "Password123!",
                Role = "Participant"
            };

            var response = await _client.PostAsJsonAsync("/api/auth/register", request);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        [Fact]
        public async Task Register_DuplicateEmail_Returns409()
        {
            var email = $"dup_{Guid.NewGuid():N}@test.com";
            var request = new RegisterRequest
            {
                FirstName = "Dup",
                LastName = "User",
                Email = email,
                Password = "Password123!",
                Role = "Participant"
            };

            await _client.PostAsJsonAsync("/api/auth/register", request);
            var response = await _client.PostAsJsonAsync("/api/auth/register", request);
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }

        [Fact]
        public async Task Register_InvalidRole_Returns400()
        {
            var request = new RegisterRequest
            {
                FirstName = "Bad",
                LastName = "Role",
                Email = $"bad_{Guid.NewGuid():N}@test.com",
                Password = "Password123!",
                Role = "Admin"
            };

            var response = await _client.PostAsJsonAsync("/api/auth/register", request);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task Login_ValidCredentials_Returns200AndToken()
        {
            var email = $"login_{Guid.NewGuid():N}@test.com";
            var password = "Password123!";
            await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
            {
                FirstName = "Login",
                LastName = "User",
                Email = email,
                Password = password,
                Role = "Participant"
            });

            var response = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest
            {
                Email = email,
                Password = password
            });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
            Assert.NotNull(body);
            Assert.False(string.IsNullOrEmpty(body.Token));
            Assert.Equal("Participant", body.Role);
        }

        [Fact]
        public async Task Login_InvalidCredentials_Returns401()
        {
            var response = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest
            {
                Email = "nonexistent@test.com",
                Password = "wrong"
            });
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task AuthenticatedEndpoint_WithoutToken_Returns401()
        {
            var response = await _client.GetAsync("/api/users/me");
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }
}
