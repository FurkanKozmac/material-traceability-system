using backend.Common.Exceptions;
using backend.DTOs;
using backend.Entities;
using backend.Hubs;
using backend.Services.Implementations;
using Microsoft.AspNetCore.SignalR;
using Moq;

namespace backend.Tests;

public class BatchUnitServiceTests
{
    [Fact]
    public async Task CreateUnit_StorageTypeMismatch_RejectsPlacement()
    {
        // Arrange
        await using var database = await SqliteTestDatabase.CreateAsync();
        var context = database.Context;
        var (_, batch, address, _) = await TestData.AddUnitAsync(context, storageType: "PAINT");
        address.StorageType = "GENERAL";
        await context.SaveChangesAsync();
        var service = new UnitService(context, CreateHub().Object);

        // Act
        var act = () => service.CreateAsync(new CreateUnitRequest(
            $"NEW-{Guid.NewGuid():N}", batch.Id, address.Id));

        // Assert
        await Assert.ThrowsAsync<BusinessRuleException>(act);
    }

    [Fact]
    public async Task CreateUnit_FullAddress_RejectsPlacement()
    {
        // Arrange
        await using var database = await SqliteTestDatabase.CreateAsync();
        var context = database.Context;
        var (_, batch, address, _) = await TestData.AddUnitAsync(context, addressCapacity: 1);
        var service = new UnitService(context, CreateHub().Object);

        // Act
        var act = () => service.CreateAsync(new CreateUnitRequest(
            $"NEW-{Guid.NewGuid():N}", batch.Id, address.Id));

        // Assert
        await Assert.ThrowsAsync<BusinessRuleException>(act);
    }

    [Fact]
    public async Task Consume_ExpiredUnit_RejectsConsumption()
    {
        // Arrange
        await using var database = await SqliteTestDatabase.CreateAsync();
        var context = database.Context;
        var (_, _, _, unit) = await TestData.AddUnitAsync(
            context, expirationDate: DateTime.UtcNow.AddDays(-1));
        var service = new UnitService(context, CreateHub().Object);

        // Act
        var act = () => service.ConsumeAsync(unit.Barcode);

        // Assert
        await Assert.ThrowsAsync<BusinessRuleException>(act);
    }

    [Fact]
    public async Task Consume_StoppedChemical_RejectsConsumption()
    {
        // Arrange
        await using var database = await SqliteTestDatabase.CreateAsync();
        var context = database.Context;
        var (_, _, _, unit) = await TestData.AddUnitAsync(context, chemicalStopped: true);
        var service = new UnitService(context, CreateHub().Object);

        // Act
        var act = () => service.ConsumeAsync(unit.Barcode);

        // Assert
        await Assert.ThrowsAsync<BusinessRuleException>(act);
    }

    [Fact]
    public async Task Consume_StockedUnit_DepletesUnitAndCancelsPendingTasks()
    {
        // Arrange
        await using var database = await SqliteTestDatabase.CreateAsync();
        var context = database.Context;
        var (_, _, address, unit) = await TestData.AddUnitAsync(context);
        var target = new Address
        {
            Code = $"TARGET-{Guid.NewGuid():N}",
            MaxCapacity = 2,
            StorageType = address.StorageType
        };
        var task = new RelocationTask
        {
            Unit = unit,
            FromAddress = address,
            ToAddress = target,
            Status = "Pending"
        };
        context.RelocationTasks.Add(task);
        await context.SaveChangesAsync();
        var hub = CreateHub();
        var service = new UnitService(context, hub.Object);

        // Act
        var result = await service.ConsumeAsync(unit.Barcode);

        // Assert
        Assert.Equal("Depleted", result.Status);
        Assert.Null(result.AddressId);
        Assert.Equal("Cancelled", task.Status);
        Assert.NotNull(task.CompletedAt);
        hub.Verify(value => value.Clients.All.SendCoreAsync(
            "UnitConsumed",
            It.IsAny<object?[]>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static Mock<IHubContext<TraceabilityHub>> CreateHub()
    {
        var proxy = new Mock<IClientProxy>();
        var clients = new Mock<IHubClients>();
        clients.Setup(value => value.All).Returns(proxy.Object);
        proxy.Setup(value => value.SendCoreAsync(
                It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var hub = new Mock<IHubContext<TraceabilityHub>>();
        hub.Setup(value => value.Clients).Returns(clients.Object);
        return hub;
    }
}
