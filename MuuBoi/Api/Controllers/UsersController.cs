using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MuuBoi.Application.Interfaces;
using MuuBoi.Data;
using MuuBoi.DTOs;
using MuuBoi.Models;

namespace MuuBoi.Api.Controllers
{
    [ApiController]
    [Route("api/users")]
    [Authorize]
    public class UsersController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly ITenantProvider _tenantProvider;

        public UsersController(
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context,
            ITenantProvider tenantProvider)
        {
            _userManager = userManager;
            _context = context;
            _tenantProvider = tenantProvider;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<UserResponseDto>>> GetAll()
        {
            var propertyId = _tenantProvider.PropertyId;
            var users = await _context.Users
                .Where(u => u.PropertyId == propertyId && u.IsActive)
                .Select(u => new UserResponseDto { Id = u.Id, Name = u.Name, Email = u.Email! })
                .ToListAsync();

            return Ok(users);
        }

        [HttpPost]
        public async Task<ActionResult<UserResponseDto>> Create([FromBody] CreateUserDto dto)
        {
            var propertyId = _tenantProvider.PropertyId;

            var existing = await _userManager.FindByEmailAsync(dto.Email);
            if (existing != null)
                return Conflict(new { message = "Email já cadastrado." });

            var user = new ApplicationUser
            {
                UserName = dto.Email,
                Email = dto.Email,
                Name = dto.Name,
                PropertyId = propertyId,
                IsActive = true
            };

            var result = await _userManager.CreateAsync(user, dto.TemporaryPassword);
            if (!result.Succeeded)
            {
                var errors = result.Errors.Select(e => e.Description);
                return BadRequest(new { message = "Erro ao criar usuário.", errors });
            }

            return StatusCode(201, new UserResponseDto
            {
                Id = user.Id,
                Name = user.Name,
                Email = user.Email!
            });
        }
    }
}
