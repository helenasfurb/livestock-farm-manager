using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MuuBoi.Application.Interfaces;
using MuuBoi.Application.Services;

namespace MuuBoi.Tests.Services;

public class AccountServiceTests
{
    private readonly Guid _propertyId = Guid.NewGuid();
    private readonly Mock<IAccountRepository> _accountRepository = new();
    private readonly Mock<ISessionValidator> _sessionValidator = new();
    private readonly Mock<IFileStorageService> _fileStorage = new();
    private readonly AccountService _service;

    public AccountServiceTests()
    {
        _service = new AccountService(
            _accountRepository.Object,
            _sessionValidator.Object,
            _fileStorage.Object,
            NullLogger<AccountService>.Instance);
        _accountRepository.Setup(r => r.GetUserIdsByPropertyAsync(_propertyId)).ReturnsAsync(new List<string> { "user-1", "user-2" });
        _accountRepository.Setup(r => r.DeletePropertyAsync(_propertyId)).ReturnsAsync(true);
    }

    [Fact]
    public async Task DeletePropertyAsync_WithUsers_InvalidatesEverySession()
    {
        await _service.DeletePropertyAsync(_propertyId);
        _sessionValidator.Verify(v => v.Invalidate("user-1"), Times.Once);
        _sessionValidator.Verify(v => v.Invalidate("user-2"), Times.Once);
    }

    [Fact]
    public async Task DeletePropertyAsync_AfterDeletingData_DeletesPropertyFiles()
    {
        var calls = new List<string>();
        _accountRepository.Setup(r => r.DeletePropertyAsync(_propertyId)).Callback(() => calls.Add("database")).ReturnsAsync(true);
        _fileStorage.Setup(s => s.DeleteByPrefixAsync(It.IsAny<string>())).Callback((string prefix) => calls.Add(prefix)).ReturnsAsync(4);
        await _service.DeletePropertyAsync(_propertyId);
        Assert.Equal(new[] { "database", $"{_propertyId}/" }, calls);
    }

    [Fact]
    public async Task DeletePropertyAsync_WhenFileDeletionFails_ReturnsTrue()
    {
        _fileStorage.Setup(s => s.DeleteByPrefixAsync(It.IsAny<string>())).ThrowsAsync(new InvalidOperationException());
        var result = await _service.DeletePropertyAsync(_propertyId);
        Assert.True(result);
    }

    [Fact]
    public async Task DeletePropertyAsync_WhenDatabaseDeletionFails_DoesNotDeleteFiles()
    {
        _accountRepository.Setup(r => r.DeletePropertyAsync(_propertyId)).ThrowsAsync(new InvalidOperationException());
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.DeletePropertyAsync(_propertyId));
        _fileStorage.Verify(s => s.DeleteByPrefixAsync(It.IsAny<string>()), Times.Never);
    }
}
