using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RentMate.Data;
using RentMate.Models; // Za Web modele (vključno z ApplicationUser)
using RentMate.Shared; // Za Shared modele (ItemDto, UserDto)
using Microsoft.AspNetCore.Identity;

namespace RentMate.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ItemsApiController : ControllerBase
    {
        private readonly RentMateContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ItemsApiController(RentMateContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: api/Items
        [HttpGet]
        public async Task<ActionResult<IEnumerable<RentMate.Shared.ItemDto>>> GetItems()
        {
            // Vzamemo Web modele iz baze in jih preslikamo v Shared DTO-je
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

        // POST: api/Items
        [HttpPost]
        public async Task<ActionResult<RentMate.Shared.Item>> PostItem(RentMate.Shared.Item sharedItem)
        {
            // Pretvorimo Shared model v Web model za bazo
            var webItem = new RentMate.Models.Item
            {
                Title = sharedItem.Title,
                Description = sharedItem.Description,
                Price = sharedItem.Price,
                UserId = sharedItem.UserId,
                IsListed = true,
                CreatedAt = DateTime.Now
            };

            _context.Items.Add(webItem);
            await _context.SaveChangesAsync();

            sharedItem.Id = webItem.Id;
            return CreatedAtAction(nameof(GetItem), new { id = webItem.Id }, sharedItem);
        }

        // PUT: api/Items/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutItem(int id, RentMate.Shared.Item sharedItem)
        {
            if (id != sharedItem.Id)
                return BadRequest();

            var webItem = await _context.Items.FindAsync(id);
            if (webItem == null) return NotFound();

            // Posodobimo vrednosti Web modela
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
                .Select(i => new RentMate.Shared.Item // Vrnemo Shared verzijo
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