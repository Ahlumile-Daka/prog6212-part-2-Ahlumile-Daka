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
    [Route("api")]
    public class EnrolmentsController : ControllerBase
    {
        private readonly RaceDayDbContext _context;

        public EnrolmentsController(RaceDayDbContext context)
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
        /// Allows a Participant to enrol in an event and select a category.
        /// </summary>
        [HttpPost("enrolments")]
        [Authorize(Roles = "Participant")]
        [ProducesResponseType(typeof(EnrolmentDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CreateEnrolment([FromBody] CreateEnrolmentRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            var evt = await _context.Events
                .Include(e => e.Enrolments)
                .FirstOrDefaultAsync(e => e.EventID == request.EventID);
            if (evt == null)
                return NotFound(new { message = "Event not found." });

            var category = await _context.Categories
                .FirstOrDefaultAsync(c => c.CategoryID == request.CategoryID && c.EventID == request.EventID);
            if (category == null)
                return NotFound(new { message = "Category not found for this event." });

            if (await _context.Enrolments.AnyAsync(en =>
                en.ParticipantID == userId.Value && en.EventID == request.EventID && en.Status == "Confirmed"))
                return Conflict(new { message = "Already enrolled in this event." });

            if (evt.MaxParticipants.HasValue)
            {
                var currentCount = evt.Enrolments.Count(en => en.Status == "Confirmed");
                if (currentCount >= evt.MaxParticipants.Value)
                    return Conflict(new { message = "Event is full." });
            }

            if (category.MaxEntries.HasValue)
            {
                var catCount = await _context.Enrolments.CountAsync(en =>
                    en.CategoryID == request.CategoryID && en.Status == "Confirmed");
                if (catCount >= category.MaxEntries.Value)
                    return Conflict(new { message = "Category is full." });
            }

            var enrolment = new Enrolment
            {
                ParticipantID = userId.Value,
                EventID = request.EventID,
                CategoryID = request.CategoryID,
                EnrolmentDate = DateTime.UtcNow,
                Status = "Confirmed"
            };

            _context.Enrolments.Add(enrolment);
            await _context.SaveChangesAsync();

            await _context.Entry(enrolment).Reference(e => e.Participant).LoadAsync();
            await _context.Entry(enrolment).Reference(e => e.Event).LoadAsync();
            await _context.Entry(enrolment).Reference(e => e.Category).LoadAsync();

            return CreatedAtAction(nameof(GetEnrolment), new { id = enrolment.EnrolmentID }, MapToDto(enrolment));
        }

        /// <summary>
        /// Returns all enrolments belonging to the logged-in Participant.
        /// </summary>
        [HttpGet("enrolments/me")]
        [Authorize(Roles = "Participant")]
        [ProducesResponseType(typeof(IEnumerable<EnrolmentDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetMyEnrolments()
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            var enrolments = await _context.Enrolments
                .Include(e => e.Participant)
                .Include(e => e.Event)
                .Include(e => e.Category)
                .Where(e => e.ParticipantID == userId.Value)
                .ToListAsync();

            return Ok(enrolments.Select(MapToDto));
        }

        /// <summary>
        /// Returns all enrolments for a specific event (Organiser only).
        /// </summary>
        [HttpGet("events/{eventId:int}/enrolments")]
        [Authorize(Roles = "Organiser")]
        [ProducesResponseType(typeof(IEnumerable<EnrolmentDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetEventEnrolments(int eventId)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            var evt = await _context.Events.FindAsync(eventId);
            if (evt == null)
                return NotFound(new { message = "Event not found." });

            if (evt.OrganiserID != userId.Value)
                return Forbid();

            var enrolments = await _context.Enrolments
                .Include(e => e.Participant)
                .Include(e => e.Event)
                .Include(e => e.Category)
                .Where(e => e.EventID == eventId)
                .ToListAsync();

            return Ok(enrolments.Select(MapToDto));
        }

        /// <summary>
        /// Returns a single enrolment.
        /// </summary>
        [HttpGet("enrolments/{id:int}")]
        [Authorize]
        [ProducesResponseType(typeof(EnrolmentDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetEnrolment(int id)
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentRole();
            if (userId == null) return Unauthorized();

            var enrolment = await _context.Enrolments
                .Include(e => e.Participant)
                .Include(e => e.Event)
                .Include(e => e.Category)
                .FirstOrDefaultAsync(e => e.EnrolmentID == id);

            if (enrolment == null)
                return NotFound(new { message = "Enrolment not found." });

            // Participant can view own; Organiser can view if they own the event
            if (role == "Participant" && enrolment.ParticipantID != userId.Value)
                return Forbid();
            if (role == "Organiser" && enrolment.Event!.OrganiserID != userId.Value)
                return Forbid();

            return Ok(MapToDto(enrolment));
        }

        /// <summary>
        /// Cancels an enrolment (Participant can cancel own; Organiser can cancel any).
        /// </summary>
        [HttpPut("enrolments/{id:int}/cancel")]
        [Authorize]
        [ProducesResponseType(typeof(EnrolmentDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CancelEnrolment(int id)
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentRole();
            if (userId == null) return Unauthorized();

            var enrolment = await _context.Enrolments
                .Include(e => e.Participant)
                .Include(e => e.Event)
                .Include(e => e.Category)
                .FirstOrDefaultAsync(e => e.EnrolmentID == id);

            if (enrolment == null)
                return NotFound(new { message = "Enrolment not found." });

            if (role == "Participant" && enrolment.ParticipantID != userId.Value)
                return Forbid();
            if (role == "Organiser" && enrolment.Event!.OrganiserID != userId.Value)
                return Forbid();

            enrolment.Status = "Cancelled";
            await _context.SaveChangesAsync();

            return Ok(MapToDto(enrolment));
        }

        private static EnrolmentDto MapToDto(Enrolment e) => new()
        {
            EnrolmentID = e.EnrolmentID,
            ParticipantID = e.ParticipantID,
            ParticipantName = e.Participant != null ? $"{e.Participant.FirstName} {e.Participant.LastName}" : null,
            EventID = e.EventID,
            EventName = e.Event?.EventName,
            CategoryID = e.CategoryID,
            CategoryName = e.Category?.CategoryName,
            EnrolmentDate = e.EnrolmentDate,
            Status = e.Status
        };
    }
}
