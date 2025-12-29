using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RentMate.Data;
using RentMate.Models; // For Web models (including ApplicationUser)
using RentMate.Shared; // For Shared models (ItemDto, UserDto)
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Localization;

namespace RentMate.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class ItemsApiController : ControllerBase
    {
        private readonly RentMateContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IStringLocalizer<ItemsApiController> _localizer;

        public ItemsApiController(
            RentMateContext context, 
            UserManager<ApplicationUser> userManager,
            IStringLocalizer<ItemsApiController> localizer)
        {
            _context = context;
            _userManager = userManager;
            _localizer = localizer;
        }

        // GET: api/Items
        [HttpGet]
        public async Task<ActionResult<IEnumerable<RentMate.Shared.ItemDto>>> GetItems()
        {
            // Map Web models from database to Shared DTOs
            var items = await _context.Items
                .Include(i => i.User)
                .Select(i => new RentMate.Shared.ItemDto
                {
                    Id = i.Id,
                    Title = i.Title,
                    Description = i.Description,
                    Price = i.Price,
                    UserId = i.UserId,
                    IsListed = i.IsListed,
                    Location = i.Location ?? i.User.City,
                    ImageUrl = i.ImageUrl,
                    CreatedAt = i.CreatedAt,
                    User = i.User != null ? new UserDto
                    {
                        Id = i.User.Id,
                        UserName = i.User.UserName,
                        Email = i.User.Email
                    } : null
                })
                .ToListAsync();

            return Ok(items);
        }

        // GET: api/Items/5
        [HttpGet("{id}")]
        public async Task<ActionResult<RentMate.Shared.ItemDto>> GetItem(int id)
        {
            var item = await _context.Items
                .Include(i => i.User)
                .FirstOrDefaultAsync(i => i.Id == id);

            // simple request logging for debugging mobile fetch issues
            Console.WriteLine($"[API] GetItem requested id={id}, found={(item != null)}");

            if (item == null)
                return NotFound();

            var itemDto = new RentMate.Shared.ItemDto
            {
                Id = item.Id,
                Title = item.Title,
                Description = item.Description,
                Price = item.Price,
                UserId = item.UserId,
                User = item.User != null ? new UserDto { Id = item.User.Id, UserName = item.User.UserName } : null
            };

            return Ok(itemDto);
        }

        [HttpPost]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<ActionResult<RentMate.Models.Item>> PostItem(RentMate.Shared.Item sharedItem)
        {
            // 1. Attempt to get ID in two ways
            string? userId = _userManager.GetUserId(User) ?? sharedItem.UserId;
            
            // If UserManager failed, try directly from Claims
            if (string.IsNullOrEmpty(userId))
            {
                userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            }

            // 2. If still NULL, we cannot proceed to the database
            if (string.IsNullOrEmpty(userId))
            {
                // Return 401 so MAUI can see that the token was not read correctly
                return Unauthorized(_localizer["Error: Server cannot find your ID in the token. Dashboard will show 0."].Value);
            }

            var webItem = new RentMate.Models.Item
            {
                Title = sharedItem.Title,
                Description = sharedItem.Description,
                Price = sharedItem.Price,
                IsListed = sharedItem.IsListed,
                Category = sharedItem.Category,
                ImageUrl = sharedItem.ImageUrl,
                UserId = userId, // Now 100% sure it's not null
                CreatedAt = DateTime.UtcNow
            };

            try 
            {
                _context.Items.Add(webItem);
                await _context.SaveChangesAsync();
                return CreatedAtAction("GetItem", new { id = webItem.Id }, webItem);
            }
            catch (Exception ex)
            {
                string errorMessage = _localizer["Error while saving: {0}", ex.InnerException?.Message ?? ex.Message].Value;
                return BadRequest(errorMessage);
            }
        }

        // PUT: api/Items/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutItem(int id, RentMate.Shared.Item sharedItem)
        {
            if (id != sharedItem.Id)
                return BadRequest();

            var webItem = await _context.Items.FindAsync(id);
            if (webItem == null) return NotFound();

            // Update Web model values
            webItem.Title = sharedItem.Title;
            webItem.Description = sharedItem.Description;
            webItem.Price = sharedItem.Price;
            webItem.IsListed = sharedItem.IsListed;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Items.Any(e => e.Id == id)) return NotFound();
                else throw;
            }

            return NoContent();
        }

        // DELETE: api/Items/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteItem(int id)
        {
            var item = await _context.Items.FindAsync(id);
            if (item == null)
                return NotFound();

            _context.Items.Remove(item);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpGet("myitems")]
        public async Task<ActionResult<IEnumerable<RentMate.Shared.Item>>> GetMyItems()
        {
            var userId = _userManager.GetUserId(User);
            return await _context.Items
                .Where(i => i.UserId == userId)
                .Select(i => new RentMate.Shared.Item // Return Shared version
                {
                    Id = i.Id,
                    Title = i.Title,
                    Description = i.Description,
                    Price = i.Price,
                    UserId = i.UserId
                })
                .ToListAsync();
        }
    }
}