using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MuuBoi.Infrastructure.Data;
using MuuBoi.Application.DTOs;
using MuuBoi.Application.Helpers;
using MuuBoi.Application.Interfaces;
using MuuBoi.Domain.Enums;
using MuuBoi.Domain.Exceptions;
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
        private readonly IAccountService _accountService;
        private readonly ISessionValidator _sessionValidator;

        public AuthController(
            UserManager<ApplicationUser> userManager,
            IConfiguration configuration,
            ApplicationDbContext context,
            IAccountService accountService,
            ISessionValidator sessionValidator)
        {
            _userManager = userManager;
            _configuration = configuration;
            _context = context;
            _accountService = accountService;
            _sessionValidator = sessionValidator;
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
                IsActive = true,
                Role = UserRole.Admin
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

            return StatusCode(201, BuildAuthResponse(user, property));
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

            if (string.IsNullOrEmpty(user.SecurityStamp))
                await _userManager.UpdateSecurityStampAsync(user);

            return Ok(BuildAuthResponse(user, property));
        }

        [HttpGet("me")]
        [Authorize]
        public async Task<ActionResult<CurrentUserResponseDto>> Me()
        {
            var user = await GetCurrentUserAsync();
            if (user == null)
                return Unauthorized();

            return Ok(BuildCurrentUserResponse(user));
        }

        [HttpPatch("me")]
        [Authorize]
        public async Task<ActionResult<CurrentUserResponseDto>> UpdateProfile([FromBody] UpdateProfileDto dto)
        {
            var user = await GetCurrentUserAsync();
            if (user == null)
                return Unauthorized();

            if (dto.Name != null)
                user.Name = dto.Name.Trim();

            if (dto.PhoneNumber != null)
                user.PhoneNumber = dto.PhoneNumber == string.Empty ? null : dto.PhoneNumber;

            await _userManager.UpdateAsync(user);

            return Ok(BuildCurrentUserResponse(user));
        }

        [HttpPatch("me/password")]
        [Authorize]
        public async Task<ActionResult<AuthResponseDto>> ChangePassword([FromBody] ChangePasswordDto dto)
        {
            var user = await GetCurrentUserAsync();
            if (user == null)
                return Unauthorized();

            var result = await _userManager.ChangePasswordAsync(user, dto.CurrentPassword, dto.NewPassword);
            if (!result.Succeeded)
            {
                if (result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.PasswordMismatch)))
                    throw new BusinessRuleException("Senha atual incorreta.");

                var errors = result.Errors.Select(e => e.Description);
                return BadRequest(new { message = "Erro ao alterar a senha.", errors });
            }

            _sessionValidator.Invalidate(user.Id);

            return Ok(BuildAuthResponse(user, user.Property!));
        }

        [HttpDelete("me")]
        [Authorize(Roles = nameof(UserRole.Admin))]
        public async Task<IActionResult> DeleteAccount([FromBody] DeleteAccountDto dto)
        {
            var user = await GetCurrentUserAsync();
            if (user == null)
                return Unauthorized();

            if (!await _userManager.CheckPasswordAsync(user, dto.Password))
                throw new BusinessRuleException("Senha incorreta.");

            await _accountService.DeletePropertyAsync(user.PropertyId);

            return NoContent();
        }

        private async Task<ApplicationUser?> GetCurrentUserAsync()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            return await _context.Users
                .Include(u => u.Property)
                .FirstOrDefaultAsync(u => u.Id == userId);
        }

        private static CurrentUserResponseDto BuildCurrentUserResponse(ApplicationUser user) => new()
        {
            Id = user.Id,
            Name = user.Name,
            Email = user.Email!,
            PhoneNumber = user.PhoneNumber,
            Role = user.Role.ToEnumValue(),
            Property = new PropertySummaryDto
            {
                Id = user.Property!.Id,
                Name = user.Property.Name
            }
        };

        private AuthResponseDto BuildAuthResponse(ApplicationUser user, Property property)
        {
            var token = GenerateJwtToken(user, property);

            return new AuthResponseDto
            {
                AccessToken = token.Token,
                ExpiresAt = token.ExpiresAt,
                User = new UserSummaryDto { Id = user.Id, Name = user.Name, Role = user.Role.ToEnumValue() },
                Property = new PropertySummaryDto { Id = property.Id, Name = property.Name }
            };
        }

        private (string Token, DateTime ExpiresAt) GenerateJwtToken(ApplicationUser user, Property property)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Email, user.Email!),
                new Claim("property_id", property.Id.ToString()),
                new Claim(ClaimTypes.Role, user.Role.ToString()),
                new Claim("security_stamp", user.SecurityStamp!),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var key = _configuration["Jwt:Key"]!;
            var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
            var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

            var expiresAt = DateTime.UtcNow.AddMonths(6);

            var token = new JwtSecurityToken(
                claims: claims,
                expires: expiresAt,
                signingCredentials: credentials
            );

            return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
        }
    }
}
