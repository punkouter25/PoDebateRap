using Moq;
using Xunit;
using PoDebateRap.Shared.Models;
using PoDebateRap.Server.Services.Data;
using Microsoft.Extensions.Logging; // Required for ILogger

namespace PoDebateRap.Tests;

public class RapperRepositoryTests
{
    private readonly Mock<ITableStorageService> _mockTableStorageService;
    private readonly Mock<ILogger<RapperRepository>> _mockLogger;
    private readonly RapperRepository _repository;

    public RapperRepositoryTests()
    {
        _mockTableStorageService = new Mock<ITableStorageService>();
        _mockLogger = new Mock<ILogger<RapperRepository>>();
        _repository = new RapperRepository(_mockTableStorageService.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task UpdateWinLossRecordAsync_UpdatesBothRappersCorrectly()
    {
        // Arrange
        var winnerName = "RapperWin";
        var loserName = "RapperLose";
        var initialWinner = new Rapper(winnerName) { Wins = 5, Losses = 2, TotalDebates = 7 };
        var initialLoser = new Rapper(loserName) { Wins = 3, Losses = 4, TotalDebates = 7 };

        // Setup mock GetEntityAsync to return the initial rappers
        _mockTableStorageService.Setup(s => s.GetEntityAsync<Rapper>("Rappers", "Rapper", winnerName))
                                .ReturnsAsync(initialWinner);
        _mockTableStorageService.Setup(s => s.GetEntityAsync<Rapper>("Rappers", "Rapper", loserName))
                                .ReturnsAsync(initialLoser);

        // Setup mock UpsertEntityAsync to capture the updated entities
        Rapper? updatedWinner = null;
        Rapper? updatedLoser = null;
        _mockTableStorageService.Setup(s => s.UpsertEntityAsync("Rappers", It.IsAny<Rapper>()))
                                .Callback<string, Rapper>((tableName, entity) =>
                                {
                                    if (entity.Name == winnerName) updatedWinner = entity;
                                    if (entity.Name == loserName) updatedLoser = entity;
                                })
                                .Returns(Task.CompletedTask);

        // Act
        await _repository.UpdateWinLossRecordAsync(winnerName, loserName);

        // Assert
        // Verify GetEntityAsync was called for both rappers
        _mockTableStorageService.Verify(s => s.GetEntityAsync<Rapper>("Rappers", "Rapper", winnerName), Times.Once);
        _mockTableStorageService.Verify(s => s.GetEntityAsync<Rapper>("Rappers", "Rapper", loserName), Times.Once);

        // Verify UpsertEntityAsync was called twice (once for each rapper)
        _mockTableStorageService.Verify(s => s.UpsertEntityAsync("Rappers", It.IsAny<Rapper>()), Times.Exactly(2));

        // Check if the captured entities have the correct updated stats
        Assert.NotNull(updatedWinner);
        Assert.Equal(6, updatedWinner.Wins); // Initial 5 + 1 win
        Assert.Equal(2, updatedWinner.Losses); // Losses unchanged
        Assert.Equal(8, updatedWinner.TotalDebates); // Initial 7 + 1 debate

        Assert.NotNull(updatedLoser);
        Assert.Equal(3, updatedLoser.Wins); // Wins unchanged
        Assert.Equal(5, updatedLoser.Losses); // Initial 4 + 1 loss
        Assert.Equal(8, updatedLoser.TotalDebates); // Initial 7 + 1 debate
    }

    // TODO: Add more tests for other repository methods (GetAll, GetByName, Seed, Delete, edge cases, error handling)
}
