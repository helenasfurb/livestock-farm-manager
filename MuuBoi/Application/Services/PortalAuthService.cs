using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MuuBoi.Application.DTOs;
using MuuBoi.Application.Helpers;
using MuuBoi.Application.Interfaces;
using MuuBoi.Application.Options;
using MuuBoi.Domain.Exceptions;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace MuuBoi.Application.Services
{
    public class PortalAuthService : IPortalAuthService
    {
        private static readonly PasswordHasher<object> Hasher = new();
        private readonly PortalOptions _options;

        public PortalAuthService(IOptions<PortalOptions> options)
        {
            _options = options.Value;
        }

        public PortalTokenDto Login(PortalLoginDto dto)
        {
            if (string.IsNullOrEmpty(_options.Username)
                || string.IsNullOrEmpty(_options.PasswordHash)
                || string.IsNullOrEmpty(_options.JwtKey))
                throw new UnauthorizedException("Credenciais inválidas.");

            var usernameValid = CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(dto.Username),
                Encoding.UTF8.GetBytes(_options.Username));

            var passwordValid = Hasher.VerifyHashedPassword(null!, _options.PasswordHash, dto.Password)
                != PasswordVerificationResult.Failed;

            if (!usernameValid || !passwordValid)
                throw new UnauthorizedException("Credenciais inválidas.");

            var expiresAt = DateTime.UtcNow.AddHours(2);

            var claims = new[]
            {
                new Claim(ClaimTypes.Name, _options.Username),
                new Claim(ClaimTypes.Role, PortalAuthDefaults.Role),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.JwtKey));

            var token = new JwtSecurityToken(
                audience: PortalAuthDefaults.Audience,
                claims: claims,
                expires: expiresAt,
                signingCredentials: new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256));

            return new PortalTokenDto
            {
                Token = new JwtSecurityTokenHandler().WriteToken(token),
                ExpiresAt = expiresAt
            };
        }
    }
}
