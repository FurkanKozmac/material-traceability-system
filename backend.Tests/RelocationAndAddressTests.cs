using backend.Common.Exceptions;
using backend.DTOs;
using backend.Entities;
using backend.Services.Implementations;

namespace backend.Tests;

public class RelocationAndAddressTests
{
    [Fact]
    public async Task CompleteTask_WrongUnitBarcode_RejectsCompletion()
    {
        // Arrange
        await using var database = await SqliteTestDatabase.CreateAsync();
        var context = database.Context;
        var (_, _, source, unit) = await TestData.AddUnitAsync(context);
        var target = new Address
        {
            Code = $"TARGET-{Guid.NewGuid():N}",
            MaxCapacity = 3,
            StorageType = source.StorageType
        };
        var task = new RelocationTask
        {
            Unit = unit,
            FromAddress = source,
            ToAddress = target,
            Status = "Pending"
        };
        context.RelocationTasks.Add(task);
        await context.SaveChangesAsync();
        var service = new RelocationTaskService(context);

        // Act
        var act = () => service.CompleteTaskAsync(task.Id, new CompleteRelocationTaskRequest(
            "NOT-THE-UNIT", $"ADR-{target.Code}"), null);

        // Assert
        await Assert.ThrowsAsync<BusinessRuleException>(act);
    }

    [Fact]
    public async Task UpdateCapacity_BelowActiveStock_RejectsCapacity()
    {
        // Arrange
        await using var database = await SqliteTestDatabase.CreateAsync();
        var context = database.Context;
        var (_, batch, address, _) = await TestData.AddUnitAsync(context, addressCapacity: 3);
        context.Units.Add(new Unit
        {
            Barcode = $"SECOND-{Guid.NewGuid():N}",
            BatchId = batch.Id,
            AddressId = address.Id,
            Status = "InStock",
            Version = 1
        });
        await context.SaveChangesAsync();
        var service = new AddressService(context);

        // Act
        var act = () => service.UpdateCapacityAsync(address.Id, 1);

        // Assert
        await Assert.ThrowsAsync<BusinessRuleException>(act);
    }

    [Fact]
    public async Task DeleteAddress_WithActiveStock_RejectsDeletion()
    {
        // Arrange
        await using var database = await SqliteTestDatabase.CreateAsync();
        var context = database.Context;
        var (_, _, address, _) = await TestData.AddUnitAsync(context);
        var service = new AddressService(context);

        // Act
        var act = () => service.DeleteAsync(address.Id);

        // Assert
        await Assert.ThrowsAsync<BusinessRuleException>(act);
    }
}
