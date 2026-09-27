using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MuuBoi.Application.Interfaces;
using MuuBoi.Infrastructure.Data;
using MuuBoi.Application.DTOs;
using MuuBoi.Domain.Enums;
using MuuBoi.Domain.Exceptions;
using MuuBoi.Domain.Models;

namespace MuuBoi.Api.Controllers
{
    [ApiController]
    [Route("api/users")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public class UsersController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly ITenantProvider _tenantProvider;
        private readonly IMapper _mapper;

        public UsersController(
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context,
            ITenantProvider tenantProvider,
            IMapper mapper)
        {
            _userManager = userManager;
            _context = context;
            _tenantProvider = tenantProvider;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<UserListItemDto>>> GetAll([FromQuery] UserFilterDto filter)
        {
            var propertyId = _tenantProvider.PropertyId;
            var query = _context.Users.Where(u => u.PropertyId == propertyId);

            if (filter.IsActive.HasValue)
                query = query.Where(u => u.IsActive == filter.IsActive.Value);

            var users = await query
                .OrderBy(u => u.Name)
                .AsNoTracking()
                .ToListAsync();

            return Ok(_mapper.Map<List<UserListItemDto>>(users));
        }

        [HttpPost]
        public async Task<ActionResult<UserResponseDto>> Create([FromBody] CreateUserDto dto)
        {
            var propertyId = _tenantProvider.PropertyId;

            var existing = await _userManager.FindByEmailAsync(dto.Email);
            if (existing != null)
                throw new ConflictException("Email já cadastrado.");

            var user = new ApplicationUser
            {
                UserName = dto.Email,
                Email = dto.Email,
                Name = dto.Name,
                PropertyId = propertyId,
                IsActive = true,
                Role = dto.Role ?? UserRole.Member
            };

            var result = await _userManager.CreateAsync(user, dto.TemporaryPassword);
            if (!result.Succeeded)
            {
                var errors = result.Errors.Select(e => e.Description);
                return BadRequest(new { message = "Erro ao criar usuário.", errors });
            }

            return StatusCode(201, _mapper.Map<UserResponseDto>(user));
        }

        [HttpPatch("{id}")]
        public async Task<ActionResult<UserResponseDto>> Update(string id, [FromBody] UpdateUserDto dto)
        {
            var user = await GetUserInPropertyAsync(id);

            if (dto.Role.HasValue && dto.Role.Value != UserRole.Admin && user.Role == UserRole.Admin && user.IsActive)
                await EnsureNotLastActiveAdminAsync(user, "Não é possível rebaixar o último administrador ativo da propriedade.");

            if (dto.Name != null)
                user.Name = dto.Name;

            if (dto.Role.HasValue)
                user.Role = dto.Role.Value;

            await _context.SaveChangesAsync();

            return Ok(_mapper.Map<UserResponseDto>(user));
        }

        [HttpPatch("{id}/revoke")]
        public async Task<ActionResult<UserResponseDto>> Revoke(string id)
        {
            var user = await GetUserInPropertyAsync(id);

            if (!user.IsActive)
                throw new ConflictException("O acesso deste membro já está revogado.");

            if (user.Role == UserRole.Admin)
                await EnsureNotLastActiveAdminAsync(user, "Não é possível revogar o acesso do último administrador ativo da propriedade.");

            user.IsActive = false;
            await _context.SaveChangesAsync();

            return Ok(_mapper.Map<UserResponseDto>(user));
        }

        [HttpPatch("{id}/reactivate")]
        public async Task<ActionResult<UserResponseDto>> Reactivate(string id)
        {
            var user = await GetUserInPropertyAsync(id);

            if (user.IsActive)
                throw new ConflictException("Este membro já está ativo.");

            user.IsActive = true;
            await _context.SaveChangesAsync();

            return Ok(_mapper.Map<UserResponseDto>(user));
        }

        private async Task<ApplicationUser> GetUserInPropertyAsync(string id)
        {
            var propertyId = _tenantProvider.PropertyId;

            return await _context.Users.FirstOrDefaultAsync(u => u.Id == id && u.PropertyId == propertyId)
                ?? throw new NotFoundException("Membro não encontrado.");
        }

        private async Task EnsureNotLastActiveAdminAsync(ApplicationUser user, string message)
        {
            var otherActiveAdmins = await _context.Users.CountAsync(u =>
                u.PropertyId == user.PropertyId &&
                u.Id != user.Id &&
                u.IsActive &&
                u.Role == UserRole.Admin);

            if (otherActiveAdmins == 0)
                throw new BusinessRuleException(message);
        }
    }
}
