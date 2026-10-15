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
    public class ResultsController : ControllerBase
    {
        private readonly RaceDayDbContext _context;

        public ResultsController(RaceDayDbContext context)
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
        /// Captures finish time and position for an enrolment.
        /// </summary>
        [HttpPost("results")]
        [Authorize(Roles = "Organiser")]
        [ProducesResponseType(typeof(ResultDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CreateResult([FromBody] CreateResultRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            var enrolment = await _context.Enrolments
                .Include(e => e.Event)
                .Include(e => e.Participant)
                .Include(e => e.Category)
                .FirstOrDefaultAsync(e => e.EnrolmentID == request.EnrolmentID);

            if (enrolment == null)
                return NotFound(new { message = "Enrolment not found." });

            if (enrolment.Event!.OrganiserID != userId.Value)
                return Forbid();

            if (await _context.Results.AnyAsync(r => r.EnrolmentID == request.EnrolmentID))
                return Conflict(new { message = "Result already exists for this enrolment." });

            var result = new Result
            {
                EnrolmentID = request.EnrolmentID,
                FinishTime = request.FinishTime,
                Position = request.Position,
                Notes = request.Notes,
                CapturedBy = userId.Value,
                CapturedDate = DateTime.UtcNow
            };

            _context.Results.Add(result);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetResult), new { id = result.ResultID }, MapToDto(result, enrolment));
        }

        /// <summary>
        /// Updates an existing result.
        /// </summary>
        [HttpPut("results/{id:int}")]
        [Authorize(Roles = "Organiser")]
        [ProducesResponseType(typeof(ResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateResult(int id, [FromBody] UpdateResultRequest request)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            var result = await _context.Results
                .Include(r => r.Enrolment)!.ThenInclude(e => e!.Event)
                .Include(r => r.Enrolment)!.ThenInclude(e => e!.Participant)
                .Include(r => r.Enrolment)!.ThenInclude(e => e!.Category)
                .FirstOrDefaultAsync(r => r.ResultID == id);

            if (result == null)
                return NotFound(new { message = "Result not found." });

            if (result.Enrolment!.Event!.OrganiserID != userId.Value)
                return Forbid();

            if (request.FinishTime.HasValue) result.FinishTime = request.FinishTime;
            if (request.Position.HasValue) result.Position = request.Position;
            if (request.Notes != null) result.Notes = request.Notes;

            await _context.SaveChangesAsync();

            return Ok(MapToDto(result, result.Enrolment));
        }

        /// <summary>
        /// Returns all results belonging to the logged-in Participant.
        /// </summary>
        [HttpGet("results/me")]
        [Authorize(Roles = "Participant")]
        [ProducesResponseType(typeof(IEnumerable<ResultDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetMyResults()
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            var results = await _context.Results
                .Include(r => r.Enrolment)!.ThenInclude(e => e!.Participant)
                .Include(r => r.Enrolment)!.ThenInclude(e => e!.Event)
                .Include(r => r.Enrolment)!.ThenInclude(e => e!.Category)
                .Where(r => r.Enrolment!.ParticipantID == userId.Value)
                .ToListAsync();

            return Ok(results.Select(r => MapToDto(r, r.Enrolment!)));
        }

        /// <summary>
        /// Returns the results list for a specific event (public leaderboard style).
        /// </summary>
        [HttpGet("events/{eventId:int}/results")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(IEnumerable<ResultDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetEventResults(int eventId)
        {
            var evtExists = await _context.Events.AnyAsync(e => e.EventID == eventId);
            if (!evtExists)
                return NotFound(new { message = "Event not found." });

            var results = await _context.Results
                .Include(r => r.Enrolment)!.ThenInclude(e => e!.Participant)
                .Include(r => r.Enrolment)!.ThenInclude(e => e!.Event)
                .Include(r => r.Enrolment)!.ThenInclude(e => e!.Category)
                .Where(r => r.Enrolment!.EventID == eventId)
                .OrderBy(r => r.Position)
                .ToListAsync();

            return Ok(results.Select(r => MapToDto(r, r.Enrolment!)));
        }

        /// <summary>
        /// Returns a single result.
        /// </summary>
        [HttpGet("results/{id:int}")]
        [Authorize]
        [ProducesResponseType(typeof(ResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetResult(int id)
        {
            var result = await _context.Results
                .Include(r => r.Enrolment)!.ThenInclude(e => e!.Participant)
                .Include(r => r.Enrolment)!.ThenInclude(e => e!.Event)
                .Include(r => r.Enrolment)!.ThenInclude(e => e!.Category)
                .FirstOrDefaultAsync(r => r.ResultID == id);

            if (result == null)
                return NotFound(new { message = "Result not found." });

            return Ok(MapToDto(result, result.Enrolment!));
        }

        private static ResultDto MapToDto(Result r, Enrolment en) => new()
        {
            ResultID = r.ResultID,
            EnrolmentID = r.EnrolmentID,
            ParticipantName = en.Participant != null ? $"{en.Participant.FirstName} {en.Participant.LastName}" : null,
            EventName = en.Event?.EventName,
            CategoryName = en.Category?.CategoryName,
            FinishTime = r.FinishTime,
            Position = r.Position,
            Notes = r.Notes,
            CapturedBy = r.CapturedBy,
            CapturedDate = r.CapturedDate
        };
    }
}
