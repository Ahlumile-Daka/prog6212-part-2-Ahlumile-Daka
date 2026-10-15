using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Models;
using RaceDay.Contracts;

namespace RaceDay.Api.Controllers
{
    [ApiController]
    [Route("api/events")]
    public class EventsController : ControllerBase
    {
        private readonly RaceDayDbContext _context;

        public EventsController(RaceDayDbContext context)
        {
            _context = context;
        }

        private int? GetCurrentUserId()
        {
            var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return claim != null ? int.Parse(claim) : null;
        }

        private string? GetCurrentRole() => User.FindFirstValue(ClaimTypes.Role);

        /// <summary>
        /// Returns a list of all events (optional query filters: status, type, date).
        /// </summary>
        [HttpGet]
        [AllowAnonymous]
        [ProducesResponseType(typeof(IEnumerable<EventDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetEvents(
            [FromQuery] string? status = null,
            [FromQuery] int? type = null,
            [FromQuery] DateTime? date = null)
        {
            var query = _context.Events
                .Include(e => e.EventType)
                .Include(e => e.Organiser)
                .AsQueryable();

            if (!string.IsNullOrEmpty(status))
                query = query.Where(e => e.Status == status);
            if (type.HasValue)
                query = query.Where(e => e.EventTypeID == type.Value);
            if (date.HasValue)
                query = query.Where(e => e.EventDate.Date == date.Value.Date);

            var events = await query.OrderBy(e => e.EventDate).ToListAsync();

            return Ok(events.Select(MapToDto));
        }

        /// <summary>
        /// Returns full details of a single event including its categories.
        /// </summary>
        [HttpGet("{id:int}")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(EventDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetEvent(int id)
        {
            var evt = await _context.Events
                .Include(e => e.EventType)
                .Include(e => e.Organiser)
                .Include(e => e.Categories)
                .FirstOrDefaultAsync(e => e.EventID == id);

            if (evt == null)
                return NotFound(new { message = "Event not found." });

            var dto = MapToDto(evt);
            dto.Categories = evt.Categories.Select(c => new CategoryDto
            {
                CategoryID = c.CategoryID,
                EventID = c.EventID,
                CategoryName = c.CategoryName,
                MinAge = c.MinAge,
                MaxAge = c.MaxAge,
                EntryFee = c.EntryFee,
                MaxEntries = c.MaxEntries
            }).ToList();

            return Ok(dto);
        }

        /// <summary>
        /// Creates a new event. The logged-in user becomes the Organiser.
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Organiser")]
        [ProducesResponseType(typeof(EventDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> CreateEvent([FromBody] CreateEventRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            var eventTypeExists = await _context.EventTypes.AnyAsync(et => et.EventTypeID == request.EventTypeID);
            if (!eventTypeExists)
                return BadRequest(new { message = "Invalid EventTypeID." });

            var evt = new Event
            {
                EventName = request.EventName,
                Description = request.Description,
                EventDate = request.EventDate,
                Location = request.Location,
                DistanceKm = request.DistanceKm,
                EventTypeID = request.EventTypeID,
                OrganiserID = userId.Value,
                MaxParticipants = request.MaxParticipants,
                Status = request.Status ?? "Open",
                CreatedDate = DateTime.UtcNow
            };

            _context.Events.Add(evt);
            await _context.SaveChangesAsync();

            await _context.Entry(evt).Reference(e => e.EventType).LoadAsync();
            await _context.Entry(evt).Reference(e => e.Organiser).LoadAsync();

            return CreatedAtAction(nameof(GetEvent), new { id = evt.EventID }, MapToDto(evt));
        }

        /// <summary>
        /// Updates an existing event. Only the owning Organiser may update.
        /// </summary>
        [HttpPut("{id:int}")]
        [Authorize(Roles = "Organiser")]
        [ProducesResponseType(typeof(EventDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateEvent(int id, [FromBody] UpdateEventRequest request)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            var evt = await _context.Events
                .Include(e => e.EventType)
                .Include(e => e.Organiser)
                .FirstOrDefaultAsync(e => e.EventID == id);

            if (evt == null)
                return NotFound(new { message = "Event not found." });

            if (evt.OrganiserID != userId.Value)
                return Forbid();

            if (request.EventName != null) evt.EventName = request.EventName;
            if (request.Description != null) evt.Description = request.Description;
            if (request.EventDate.HasValue) evt.EventDate = request.EventDate.Value;
            if (request.Location != null) evt.Location = request.Location;
            if (request.DistanceKm.HasValue) evt.DistanceKm = request.DistanceKm.Value;
            if (request.EventTypeID.HasValue)
            {
                if (!await _context.EventTypes.AnyAsync(et => et.EventTypeID == request.EventTypeID.Value))
                    return BadRequest(new { message = "Invalid EventTypeID." });
                evt.EventTypeID = request.EventTypeID.Value;
            }
            if (request.MaxParticipants.HasValue) evt.MaxParticipants = request.MaxParticipants;
            if (request.Status != null) evt.Status = request.Status;

            await _context.SaveChangesAsync();
            await _context.Entry(evt).Reference(e => e.EventType).LoadAsync();

            return Ok(MapToDto(evt));
        }

        /// <summary>
        /// Removes an event. Only the owning Organiser may delete.
        /// </summary>
        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Organiser")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> DeleteEvent(int id)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            var evt = await _context.Events
                .Include(e => e.Enrolments)
                .FirstOrDefaultAsync(e => e.EventID == id);

            if (evt == null)
                return NotFound(new { message = "Event not found." });

            if (evt.OrganiserID != userId.Value)
                return Forbid();

            if (evt.Enrolments.Any(en => en.Status == "Confirmed"))
                return Conflict(new { message = "Cannot delete event with existing enrolments." });

            _context.Events.Remove(evt);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private static EventDto MapToDto(Event e) => new()
        {
            EventID = e.EventID,
            EventName = e.EventName,
            Description = e.Description,
            EventDate = e.EventDate,
            Location = e.Location,
            DistanceKm = e.DistanceKm,
            EventTypeID = e.EventTypeID,
            EventTypeName = e.EventType?.Name,
            OrganiserID = e.OrganiserID,
            OrganiserName = e.Organiser != null ? $"{e.Organiser.FirstName} {e.Organiser.LastName}" : null,
            MaxParticipants = e.MaxParticipants,
            Status = e.Status,
            CreatedDate = e.CreatedDate
        };
    }
}
