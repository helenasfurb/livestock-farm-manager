using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using MuuBoi.Application.Interfaces;
using MuuBoi.Application.Services;
using MuuBoi.Domain.Models;

namespace MuuBoi.Tests.Services;

public class PasswordRecoveryServiceTests
{
    private const string InvalidTokenCode = nameof(IdentityErrorDescriber.InvalidToken);

    private readonly Mock<UserManager<ApplicationUser>> _userManager;
    private readonly Mock<IEmailService> _emailService = new();
    private readonly Mock<ISessionValidator> _sessionValidator = new();
    private readonly PasswordRecoveryService _service;

    public PasswordRecoveryServiceTests()
    {
        _userManager = new Mock<UserManager<ApplicationUser>>(
            Mock.Of<IUserStore<ApplicationUser>>(), null!, null!, null!, null!, null!, null!, null!, null!);
        _service = new PasswordRecoveryService(
            _userManager.Object,
            _emailService.Object,
            _sessionValidator.Object,
            new MemoryCache(new MemoryCacheOptions()));
        _userManager.Setup(m => m.GeneratePasswordResetTokenAsync(It.IsAny<ApplicationUser>())).ReturnsAsync("482915");
        _userManager.Setup(m => m.ResetPasswordAsync(It.IsAny<ApplicationUser>(), "482915", It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);
        _userManager.Setup(m => m.ResetPasswordAsync(It.IsAny<ApplicationUser>(), It.Is<string>(c => c != "482915"), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Code = InvalidTokenCode }));
        _userManager.Setup(m => m.UpdateAsync(It.IsAny<ApplicationUser>())).ReturnsAsync(IdentityResult.Success);
        _emailService.Setup(e => e.SendPasswordResetCodeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);
    }

    private ApplicationUser SetupUser(bool isActive = true, string? securityStamp = "stamp", bool emailConfirmed = true)
    {
        var user = new ApplicationUser
        {
            Id = "user-1",
            Email = "maria@exemplo.com",
            Name = "Maria",
            IsActive = isActive,
            SecurityStamp = securityStamp,
            EmailConfirmed = emailConfirmed
        };
        _userManager.Setup(m => m.FindByEmailAsync(user.Email)).ReturnsAsync(user);
        return user;
    }

    [Fact]
    public async Task RequestResetAsync_WithActiveUser_SendsCodeAndReturnsTrue()
    {
        SetupUser();
        var result = await _service.RequestResetAsync("maria@exemplo.com");
        Assert.True(result);
        _emailService.Verify(e => e.SendPasswordResetCodeAsync("maria@exemplo.com", "Maria", "482915"), Times.Once);
    }

    [Fact]
    public async Task RequestResetAsync_WithUnknownEmail_DoesNotSendAndReturnsFalse()
    {
        var result = await _service.RequestResetAsync("ninguem@exemplo.com");
        Assert.False(result);
        _emailService.Verify(e => e.SendPasswordResetCodeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RequestResetAsync_WithInactiveUser_DoesNotSendAndReturnsFalse()
    {
        SetupUser(isActive: false);
        var result = await _service.RequestResetAsync("maria@exemplo.com");
        Assert.False(result);
        _emailService.Verify(e => e.SendPasswordResetCodeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RequestResetAsync_WhenEmailFails_ReturnsFalse()
    {
        SetupUser();
        _emailService.Setup(e => e.SendPasswordResetCodeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(false);
        var result = await _service.RequestResetAsync("maria@exemplo.com");
        Assert.False(result);
    }

    [Fact]
    public async Task RequestResetAsync_WithEmptySecurityStamp_UpdatesStampBeforeGenerating()
    {
        var user = SetupUser(securityStamp: null);
        var calls = new List<string>();
        _userManager.Setup(m => m.UpdateSecurityStampAsync(user))
            .Callback(() => calls.Add("stamp"))
            .ReturnsAsync(IdentityResult.Success);
        _userManager.Setup(m => m.GeneratePasswordResetTokenAsync(user))
            .Callback(() => calls.Add("generate"))
            .ReturnsAsync("482915");
        await _service.RequestResetAsync("maria@exemplo.com");
        Assert.Equal(new[] { "stamp", "generate" }, calls);
    }

    [Fact]
    public async Task ResetPasswordAsync_WithValidCode_SucceedsAndInvalidatesSessions()
    {
        SetupUser();
        var result = await _service.ResetPasswordAsync("maria@exemplo.com", "482915", "NovaSenha1!");
        Assert.True(result.Succeeded);
        _sessionValidator.Verify(s => s.Invalidate("user-1"), Times.Once);
    }

    [Fact]
    public async Task ResetPasswordAsync_WithInvalidCode_ReturnsInvalidCodeError()
    {
        SetupUser();
        var result = await _service.ResetPasswordAsync("maria@exemplo.com", "000000", "NovaSenha1!");
        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Code == InvalidTokenCode);
        _sessionValidator.Verify(s => s.Invalidate(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ResetPasswordAsync_WithUnknownEmail_ReturnsInvalidCodeErrorWithoutResetting()
    {
        var result = await _service.ResetPasswordAsync("ninguem@exemplo.com", "482915", "NovaSenha1!");
        Assert.Contains(result.Errors, e => e.Code == InvalidTokenCode);
        _userManager.Verify(m => m.ResetPasswordAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ResetPasswordAsync_WithInactiveUser_ReturnsInvalidCodeErrorWithoutResetting()
    {
        SetupUser(isActive: false);
        var result = await _service.ResetPasswordAsync("maria@exemplo.com", "482915", "NovaSenha1!");
        Assert.Contains(result.Errors, e => e.Code == InvalidTokenCode);
        _userManager.Verify(m => m.ResetPasswordAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ResetPasswordAsync_WithWeakPassword_ReturnsPasswordErrorsWithoutInvalidatingSessions()
    {
        SetupUser();
        _userManager.Setup(m => m.ResetPasswordAsync(It.IsAny<ApplicationUser>(), "482915", "abcdef"))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Code = "PasswordRequiresDigit", Description = "Passwords must have at least one digit ('0'-'9')." }));
        var result = await _service.ResetPasswordAsync("maria@exemplo.com", "482915", "abcdef");
        Assert.Contains(result.Errors, e => e.Code == "PasswordRequiresDigit");
        Assert.DoesNotContain(result.Errors, e => e.Code == InvalidTokenCode);
        _sessionValidator.Verify(s => s.Invalidate(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ResetPasswordAsync_AfterFiveInvalidCodes_RejectsWithoutCheckingCode()
    {
        SetupUser();
        for (var i = 0; i < 5; i++)
            await _service.ResetPasswordAsync("maria@exemplo.com", "000000", "NovaSenha1!");
        var result = await _service.ResetPasswordAsync("maria@exemplo.com", "482915", "NovaSenha1!");
        Assert.Contains(result.Errors, e => e.Code == InvalidTokenCode);
        _userManager.Verify(m => m.ResetPasswordAsync(It.IsAny<ApplicationUser>(), "482915", It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ResetPasswordAsync_WithUnconfirmedEmail_ConfirmsEmail()
    {
        var user = SetupUser(emailConfirmed: false);
        await _service.ResetPasswordAsync("maria@exemplo.com", "482915", "NovaSenha1!");
        Assert.True(user.EmailConfirmed);
        _userManager.Verify(m => m.UpdateAsync(user), Times.Once);
    }

    [Fact]
    public async Task ResetPasswordAsync_WithValidCode_ClearsFailedAttempts()
    {
        SetupUser();
        for (var i = 0; i < 4; i++)
            await _service.ResetPasswordAsync("maria@exemplo.com", "000000", "NovaSenha1!");
        await _service.ResetPasswordAsync("maria@exemplo.com", "482915", "NovaSenha1!");
        await _service.ResetPasswordAsync("maria@exemplo.com", "000000", "NovaSenha1!");
        var result = await _service.ResetPasswordAsync("maria@exemplo.com", "482915", "NovaSenha1!");
        Assert.True(result.Succeeded);
    }
}
