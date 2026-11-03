using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using MuuBoi.Application.DTOs;
using MuuBoi.Application.Helpers;
using MuuBoi.Application.Options;
using MuuBoi.Application.Services;
using MuuBoi.Domain.Exceptions;

namespace MuuBoi.Tests.Services;

public class PortalAuthServiceTests
{
    private const string Username = "operator";
    private const string Password = "correct-password";

    private static PortalOptions BuildOptions() => new()
    {
        Username = Username,
        PasswordHash = new PasswordHasher<object>().HashPassword(null!, Password),
        JwtKey = "test-signing-key-with-at-least-32-bytes!"
    };

    private static PortalAuthService BuildService(PortalOptions options) => new(Options.Create(options));

    [Fact]
    public void Login_WithValidCredentials_ReturnsPortalAdminToken()
    {
        var service = BuildService(BuildOptions());
        var result = service.Login(new PortalLoginDto { Username = Username, Password = Password });
        var token = new JwtSecurityTokenHandler().ReadJwtToken(result.Token);
        Assert.Contains(PortalAuthDefaults.Audience, token.Audiences);
        Assert.Contains(token.Claims, c => c.Type == ClaimTypes.Role && c.Value == PortalAuthDefaults.Role);
    }

    [Fact]
    public void Login_WithValidCredentials_ExpiresInTwoHours()
    {
        var service = BuildService(BuildOptions());
        var before = DateTime.UtcNow;
        var result = service.Login(new PortalLoginDto { Username = Username, Password = Password });
        Assert.InRange(result.ExpiresAt, before.AddHours(2), DateTime.UtcNow.AddHours(2));
    }

    [Fact]
    public void Login_WithWrongPassword_ThrowsUnauthorizedException()
    {
        var service = BuildService(BuildOptions());
        Assert.Throws<UnauthorizedException>(() => service.Login(new PortalLoginDto { Username = Username, Password = "wrong" }));
    }

    [Fact]
    public void Login_WithWrongUsername_ThrowsUnauthorizedException()
    {
        var service = BuildService(BuildOptions());
        Assert.Throws<UnauthorizedException>(() => service.Login(new PortalLoginDto { Username = "someone-else", Password = Password }));
    }

    [Fact]
    public void Login_WithoutJwtKey_ThrowsUnauthorizedException()
    {
        var options = BuildOptions();
        options.JwtKey = string.Empty;
        var service = BuildService(options);
        Assert.Throws<UnauthorizedException>(() => service.Login(new PortalLoginDto { Username = Username, Password = Password }));
    }
}
