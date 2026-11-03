using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using MuuBoi.Application.Interfaces;
using MuuBoi.Application.Services;
using MuuBoi.Domain.Models;

namespace MuuBoi.Tests.Services;

public class EmailConfirmationServiceTests
{
    private const string InvalidTokenCode = nameof(IdentityErrorDescriber.InvalidToken);
    private const string ValidCode = "731045";

    private readonly Mock<UserManager<ApplicationUser>> _userManager;
    private readonly Mock<IEmailService> _emailService = new();
    private readonly EmailConfirmationService _service;

    public EmailConfirmationServiceTests()
    {
        _userManager = new Mock<UserManager<ApplicationUser>>(
            Mock.Of<IUserStore<ApplicationUser>>(), null!, null!, null!, null!, null!, null!, null!, null!);
        _service = new EmailConfirmationService(_userManager.Object, _emailService.Object, new MemoryCache(new MemoryCacheOptions()));
        _userManager.Setup(m => m.GenerateEmailConfirmationTokenAsync(It.IsAny<ApplicationUser>())).ReturnsAsync(ValidCode);
        _userManager.Setup(m => m.ConfirmEmailAsync(It.IsAny<ApplicationUser>(), ValidCode)).ReturnsAsync(IdentityResult.Success);
        _userManager.Setup(m => m.ConfirmEmailAsync(It.IsAny<ApplicationUser>(), It.Is<string>(c => c != ValidCode)))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Code = InvalidTokenCode }));
        _userManager.Setup(m => m.VerifyUserTokenAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>(), UserManager<ApplicationUser>.ConfirmEmailTokenPurpose, It.IsAny<string>()))
            .ReturnsAsync((ApplicationUser _, string _, string _, string code) => code == ValidCode);
        _emailService.Setup(e => e.SendEmailConfirmationCodeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);
    }

    private ApplicationUser SetupUser(bool emailConfirmed = false, bool isActive = true, string? securityStamp = "stamp")
    {
        var user = new ApplicationUser
        {
            Id = "user-1",
            Email = "maria@exemplo.com",
            Name = "Maria",
            IsActive = isActive,
            EmailConfirmed = emailConfirmed,
            SecurityStamp = securityStamp
        };
        _userManager.Setup(m => m.FindByEmailAsync(user.Email)).ReturnsAsync(user);
        return user;
    }

    private void VerifyNoEmailSent() =>
        _emailService.Verify(e => e.SendEmailConfirmationCodeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);

    [Fact]
    public async Task SendCodeAsync_WithUser_SendsCode()
    {
        var user = SetupUser();
        var result = await _service.SendCodeAsync(user);
        Assert.True(result);
        _emailService.Verify(e => e.SendEmailConfirmationCodeAsync("maria@exemplo.com", "Maria", ValidCode), Times.Once);
    }

    [Fact]
    public async Task SendCodeAsync_WithinCooldown_DoesNotSendAgain()
    {
        var user = SetupUser();
        await _service.SendCodeAsync(user);
        var result = await _service.SendCodeAsync(user);
        Assert.False(result);
        _emailService.Verify(e => e.SendEmailConfirmationCodeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task SendCodeAsync_WhenEmailFails_AllowsImmediateRetry()
    {
        var user = SetupUser();
        _emailService.SetupSequence(e => e.SendEmailConfirmationCodeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(false)
            .ReturnsAsync(true);
        var first = await _service.SendCodeAsync(user);
        var second = await _service.SendCodeAsync(user);
        Assert.False(first);
        Assert.True(second);
    }

    [Fact]
    public async Task SendCodeAsync_WithEmptySecurityStamp_UpdatesStampBeforeGenerating()
    {
        var user = SetupUser(securityStamp: null);
        var calls = new List<string>();
        _userManager.Setup(m => m.UpdateSecurityStampAsync(user))
            .Callback(() => calls.Add("stamp"))
            .ReturnsAsync(IdentityResult.Success);
        _userManager.Setup(m => m.GenerateEmailConfirmationTokenAsync(user))
            .Callback(() => calls.Add("generate"))
            .ReturnsAsync(ValidCode);
        await _service.SendCodeAsync(user);
        Assert.Equal(new[] { "stamp", "generate" }, calls);
    }

    [Fact]
    public async Task ResendAsync_WithUnconfirmedUser_SendsCode()
    {
        SetupUser();
        var result = await _service.ResendAsync("maria@exemplo.com");
        Assert.True(result);
        _emailService.Verify(e => e.SendEmailConfirmationCodeAsync("maria@exemplo.com", "Maria", ValidCode), Times.Once);
    }

    [Fact]
    public async Task ResendAsync_WithConfirmedUser_DoesNotSend()
    {
        SetupUser(emailConfirmed: true);
        var result = await _service.ResendAsync("maria@exemplo.com");
        Assert.False(result);
        VerifyNoEmailSent();
    }

    [Fact]
    public async Task ResendAsync_WithUnknownEmail_DoesNotSend()
    {
        var result = await _service.ResendAsync("ninguem@exemplo.com");
        Assert.False(result);
        VerifyNoEmailSent();
    }

    [Fact]
    public async Task ResendAsync_WithInactiveUser_DoesNotSend()
    {
        SetupUser(isActive: false);
        var result = await _service.ResendAsync("maria@exemplo.com");
        Assert.False(result);
        VerifyNoEmailSent();
    }

    [Fact]
    public async Task ConfirmAsync_WithValidCode_Succeeds()
    {
        var user = SetupUser();
        var result = await _service.ConfirmAsync("maria@exemplo.com", ValidCode);
        Assert.True(result.Succeeded);
        _userManager.Verify(m => m.ConfirmEmailAsync(user, ValidCode), Times.Once);
    }

    [Fact]
    public async Task ConfirmAsync_WithInvalidCode_ReturnsInvalidCodeError()
    {
        SetupUser();
        var result = await _service.ConfirmAsync("maria@exemplo.com", "000000");
        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Code == InvalidTokenCode);
    }

    [Fact]
    public async Task ConfirmAsync_WithUnknownEmail_ReturnsInvalidCodeError()
    {
        var result = await _service.ConfirmAsync("ninguem@exemplo.com", ValidCode);
        Assert.Contains(result.Errors, e => e.Code == InvalidTokenCode);
        _userManager.Verify(m => m.ConfirmEmailAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ConfirmAsync_AlreadyConfirmedWithValidCode_Succeeds()
    {
        SetupUser(emailConfirmed: true);
        var result = await _service.ConfirmAsync("maria@exemplo.com", ValidCode);
        Assert.True(result.Succeeded);
        _userManager.Verify(m => m.ConfirmEmailAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ConfirmAsync_AlreadyConfirmedWithInvalidCode_ReturnsInvalidCodeError()
    {
        SetupUser(emailConfirmed: true);
        var result = await _service.ConfirmAsync("maria@exemplo.com", "000000");
        Assert.Contains(result.Errors, e => e.Code == InvalidTokenCode);
    }

    [Fact]
    public async Task ConfirmAsync_AfterFiveInvalidCodes_RejectsWithoutCheckingCode()
    {
        SetupUser();
        for (var i = 0; i < 5; i++)
            await _service.ConfirmAsync("maria@exemplo.com", "000000");
        var result = await _service.ConfirmAsync("maria@exemplo.com", ValidCode);
        Assert.Contains(result.Errors, e => e.Code == InvalidTokenCode);
        _userManager.Verify(m => m.ConfirmEmailAsync(It.IsAny<ApplicationUser>(), ValidCode), Times.Never);
    }
}
