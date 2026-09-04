using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MuuBoi.Infrastructure.Data;
using MuuBoi.Application.DTOs;
using MuuBoi.Domain.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace MuuBoi.Api.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IConfiguration _configuration;
        private readonly ApplicationDbContext _context;

        public AuthController(
            UserManager<ApplicationUser> userManager,
            IConfiguration configuration,
            ApplicationDbContext context)
        {
            _userManager = userManager;
            _configuration = configuration;
            _context = context;
        }

        [HttpPost("register")]
        public async Task<ActionResult<AuthResponseDto>> Register([FromBody] RegisterDto model)
        {
            var existingUser = await _userManager.FindByEmailAsync(model.Email);
            if (existingUser != null)
                return Conflict(new { message = "Email já cadastrado." });

            await using var transaction = await _context.Database.BeginTransactionAsync();

            var property = new Property
            {
                Name = model.PropertyName
            };
            _context.Properties.Add(property);
            await _context.SaveChangesAsync();

            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                Name = model.Name,
                PropertyId = property.Id,
                IsActive = true
            };

            var result = await _userManager.CreateAsync(user, model.Password);
            if (!result.Succeeded)
            {
                await transaction.RollbackAsync();
                var errors = result.Errors.Select(e => e.Description);
                return BadRequest(new { message = "Erro ao criar usuário.", errors });
            }

            // Every new farm starts with the default vaccine catalog.
            await VaccineCatalogSeeder.SeedForPropertyAsync(_context, property.Id);

            await transaction.CommitAsync();

            var token = GenerateJwtToken(user, property);
            return StatusCode(201, new AuthResponseDto
            {
                AccessToken = token.Token,
                ExpiresAt = token.ExpiresAt,
                User = new UserSummaryDto { Id = user.Id, Name = user.Name },
                Property = new PropertySummaryDto { Id = property.Id, Name = property.Name }
            });
        }

        [HttpPost("login")]
        public async Task<ActionResult<AuthResponseDto>> Login([FromBody] LoginDto model)
        {
            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
                return Unauthorized(new { message = "Credenciais inválidas." });

            if (!user.IsActive)
                return StatusCode(403, new { message = "Usuário desativado." });

            var passwordValid = await _userManager.CheckPasswordAsync(user, model.Password);
            if (!passwordValid)
                return Unauthorized(new { message = "Credenciais inválidas." });

            var property = await _context.Properties.FindAsync(user.PropertyId);
            if (property == null)
                return StatusCode(500, new { message = "Propriedade não encontrada." });

            var token = GenerateJwtToken(user, property);
            return Ok(new AuthResponseDto
            {
                AccessToken = token.Token,
                ExpiresAt = token.ExpiresAt,
                User = new UserSummaryDto { Id = user.Id, Name = user.Name },
                Property = new PropertySummaryDto { Id = property.Id, Name = property.Name }
            });
        }

        [HttpGet("me")]
        [Authorize]
        public async Task<ActionResult<CurrentUserResponseDto>> Me()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var user = await _context.Users
                .Include(u => u.Property)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
                return Unauthorized();

            return Ok(new CurrentUserResponseDto
            {
                Id = user.Id,
                Name = user.Name,
                Email = user.Email!,
                Property = new PropertySummaryDto
                {
                    Id = user.Property!.Id,
                    Name = user.Property.Name
                }
            });
        }

        private (string Token, DateTime ExpiresAt) GenerateJwtToken(ApplicationUser user, Property property)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Email, user.Email!),
                new Claim("property_id", property.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var key = _configuration["Jwt:Key"]!;
            var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
            var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

            var expiresAt = DateTime.UtcNow.AddHours(24);

            var token = new JwtSecurityToken(
                claims: claims,
                expires: expiresAt,
                signingCredentials: credentials
            );

            return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
        }
    }
}
