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
    public class CategoriesController : ControllerBase
    {
        private readonly RaceDayDbContext _context;

        public CategoriesController(RaceDayDbContext context)
        {
            _context = context;
        }

        private int? GetCurrentUserId()
        {
            var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return claim != null ? int.Parse(claim) : null;
        }

        /// <summary>
        /// Returns all categories belonging to a specific event.
        /// </summary>
        [HttpGet("events/{eventId:int}/categories")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(IEnumerable<CategoryDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetCategoriesForEvent(int eventId)
        {
            var evt = await _context.Events.FindAsync(eventId);
            if (evt == null)
                return NotFound(new { message = "Event does not exist." });

            var categories = await _context.Categories
                .Where(c => c.EventID == eventId)
                .ToListAsync();

            return Ok(categories.Select(MapToDto));
        }

        /// <summary>
        /// Returns a single category.
        /// </summary>
        [HttpGet("categories/{id:int}")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetCategory(int id)
        {
            var cat = await _context.Categories.FindAsync(id);
            if (cat == null)
                return NotFound(new { message = "Category not found." });

            return Ok(MapToDto(cat));
        }

        /// <summary>
        /// Creates a new category for an event.
        /// </summary>
        [HttpPost("events/{eventId:int}/categories")]
        [Authorize(Roles = "Organiser")]
        [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> CreateCategory(int eventId, [FromBody] CreateCategoryRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            var evt = await _context.Events.FindAsync(eventId);
            if (evt == null)
                return NotFound(new { message = "Event does not exist." });

            if (evt.OrganiserID != userId.Value)
                return Forbid();

            var cat = new Category
            {
                EventID = eventId,
                CategoryName = request.CategoryName,
                MinAge = request.MinAge,
                MaxAge = request.MaxAge,
                EntryFee = request.EntryFee,
                MaxEntries = request.MaxEntries
            };

            _context.Categories.Add(cat);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetCategory), new { id = cat.CategoryID }, MapToDto(cat));
        }

        /// <summary>
        /// Updates an existing category.
        /// </summary>
        [HttpPut("categories/{id:int}")]
        [Authorize(Roles = "Organiser")]
        [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateCategory(int id, [FromBody] UpdateCategoryRequest request)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            var cat = await _context.Categories
                .Include(c => c.Event)
                .FirstOrDefaultAsync(c => c.CategoryID == id);

            if (cat == null)
                return NotFound(new { message = "Category not found." });

            if (cat.Event!.OrganiserID != userId.Value)
                return Forbid();

            if (request.CategoryName != null) cat.CategoryName = request.CategoryName;
            if (request.MinAge.HasValue) cat.MinAge = request.MinAge;
            if (request.MaxAge.HasValue) cat.MaxAge = request.MaxAge;
            if (request.EntryFee.HasValue) cat.EntryFee = request.EntryFee.Value;
            if (request.MaxEntries.HasValue) cat.MaxEntries = request.MaxEntries;

            await _context.SaveChangesAsync();
            return Ok(MapToDto(cat));
        }

        /// <summary>
        /// Deletes a category (only if no enrolments exist).
        /// </summary>
        [HttpDelete("categories/{id:int}")]
        [Authorize(Roles = "Organiser")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            var cat = await _context.Categories
                .Include(c => c.Event)
                .Include(c => c.Enrolments)
                .FirstOrDefaultAsync(c => c.CategoryID == id);

            if (cat == null)
                return NotFound(new { message = "Category not found." });

            if (cat.Event!.OrganiserID != userId.Value)
                return Forbid();

            if (cat.Enrolments.Any(en => en.Status == "Confirmed"))
                return Conflict(new { message = "Category has enrolments." });

            _context.Categories.Remove(cat);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private static CategoryDto MapToDto(Category c) => new()
        {
            CategoryID = c.CategoryID,
            EventID = c.EventID,
            CategoryName = c.CategoryName,
            MinAge = c.MinAge,
            MaxAge = c.MaxAge,
            EntryFee = c.EntryFee,
            MaxEntries = c.MaxEntries
        };
    }
}
