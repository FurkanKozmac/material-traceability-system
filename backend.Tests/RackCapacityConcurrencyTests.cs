using backend.Common.Exceptions;
using backend.Data;
using backend.DTOs;
using backend.Entities;
using backend.Hubs;
using backend.Services.Implementations;
using backend.Services.Implementations.Notifications;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace backend.Tests;

[Collection(PostgresTestCollection.Name)]
[Trait("Category", "Testcontainers")]
public class RackCapacityConcurrencyTests(PostgresTestDatabase database)
{
    [Fact]
    public async Task UnitCreate_ConcurrentRequests_DoNotExceedCapacity()
    {
        const int capacity = 3;
        var (batchId, addressId) = await PreparePlacementAsync(capacity);
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var hub = CreateHub();

        var tasks = Enumerable.Range(0, capacity + 1).Select(async index =>
        {
            await using var context = database.CreateContext();
            var service = new UnitService(context, hub.Object, new LowStockChannel());
            await start.Task;
            try
            {
                await service.CreateAsync(new CreateUnitRequest(
                    $"RACE-UNIT-{Guid.NewGuid():N}-{index}", batchId, addressId));
                return true;
            }
            catch (BusinessRuleException)
            {
                return false;
            }
        }).ToArray();

        start.SetResult();
        var results = await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(20));

        Assert.Equal(capacity, results.Count(success => success));
    }

    [Fact]
    public async Task RelocationToCapacityOneTarget_ConcurrentCompletions_OnlyOneSucceeds()
    {
        var setup = await PrepareRelocationsAsync(targetCapacity: 1, crossDirection: false);
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var first = CompleteAsync(setup.FirstTaskId, setup.FirstUnitBarcode, setup.TargetBarcode, start);
        var second = CompleteAsync(setup.SecondTaskId, setup.SecondUnitBarcode, setup.TargetBarcode, start);

        start.SetResult();
        var results = await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(20));

        Assert.Equal(1, results.Count(success => success));
    }

    [Fact]
    public async Task CrossRelocations_ABAndBA_CompleteWithoutDeadlock()
    {
        var setup = await PrepareRelocationsAsync(targetCapacity: 2, crossDirection: true);
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var first = CompleteAsync(setup.FirstTaskId, setup.FirstUnitBarcode, setup.SecondTargetBarcode, start);
        var second = CompleteAsync(setup.SecondTaskId, setup.SecondUnitBarcode, setup.FirstTargetBarcode, start);

        start.SetResult();
        var results = await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(20));

        Assert.All(results, result => Assert.True(result));
    }

    private async Task<bool> CompleteAsync(
        long taskId,
        string unitBarcode,
        string targetAddressBarcode,
        TaskCompletionSource start)
    {
        await using var context = database.CreateContext();
        var service = new RelocationTaskService(context, new LowStockChannel());
        await start.Task;
        try
        {
            return await service.CompleteTaskAsync(
                taskId,
                new CompleteRelocationTaskRequest(unitBarcode, targetAddressBarcode),
                null);
        }
        catch (BusinessRuleException)
        {
            return false;
        }
    }

    private async Task<(long BatchId, long AddressId)> PreparePlacementAsync(int capacity)
    {
        await using var context = database.CreateContext();
        var chemical = new Chemical
        {
            Name = $"Race chemical {Guid.NewGuid():N}",
            ChemicalCode = $"RACE-CHEM-{Guid.NewGuid():N}",
            StorageType = "GENERAL"
        };
        var batch = new Batch
        {
            BatchNo = $"RACE-BATCH-{Guid.NewGuid():N}",
            Chemical = chemical,
            InitialQuantity = capacity + 1
        };
        var address = new Address
        {
            Code = $"RACE-RACK-{Guid.NewGuid():N}",
            StorageType = "GENERAL",
            MaxCapacity = capacity
        };
        context.AddRange(batch, address);
        await context.SaveChangesAsync();
        return (batch.Id, address.Id);
    }

    private async Task<RelocationSetup> PrepareRelocationsAsync(
        int targetCapacity,
        bool crossDirection)
    {
        await using var context = database.CreateContext();
        var chemical = new Chemical
        {
            Name = $"Relocation race chemical {Guid.NewGuid():N}",
            ChemicalCode = $"RELOC-CHEM-{Guid.NewGuid():N}",
            StorageType = "GENERAL"
        };
        var batch = new Batch
        {
            BatchNo = $"RELOC-BATCH-{Guid.NewGuid():N}",
            Chemical = chemical,
            InitialQuantity = 2
        };
        var sourceA = new Address
        {
            Code = $"RELOC-A-{Guid.NewGuid():N}",
            StorageType = "GENERAL",
            MaxCapacity = 5
        };
        var sourceB = new Address
        {
            Code = $"RELOC-B-{Guid.NewGuid():N}",
            StorageType = "GENERAL",
            MaxCapacity = 5
        };
        var target = new Address
        {
            Code = $"RELOC-T-{Guid.NewGuid():N}",
            StorageType = "GENERAL",
            MaxCapacity = targetCapacity
        };

        var firstUnit = new Unit
        {
            Barcode = $"RELOC-UNIT-A-{Guid.NewGuid():N}",
            Batch = batch,
            Address = sourceA,
            Status = "InStock",
            Version = 1
        };
        var secondUnit = new Unit
        {
            Barcode = $"RELOC-UNIT-B-{Guid.NewGuid():N}",
            Batch = batch,
            Address = crossDirection ? sourceB : sourceA,
            Status = "InStock",
            Version = 1
        };
        var firstTask = new RelocationTask
        {
            Unit = firstUnit,
            FromAddress = sourceA,
            ToAddress = crossDirection ? sourceB : target,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };
        var secondTask = new RelocationTask
        {
            Unit = secondUnit,
            FromAddress = crossDirection ? sourceB : sourceA,
            ToAddress = crossDirection ? sourceA : target,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };
        context.AddRange(firstUnit, secondUnit, firstTask, secondTask);
        await context.SaveChangesAsync();

        return new RelocationSetup(
            firstTask.Id,
            firstUnit.Barcode,
            secondTask.Id,
            secondUnit.Barcode,
            $"ADR-{target.Code}",
            $"ADR-{sourceA.Code}",
            $"ADR-{sourceB.Code}");
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

    private sealed record RelocationSetup(
        long FirstTaskId,
        string FirstUnitBarcode,
        long SecondTaskId,
        string SecondUnitBarcode,
        string TargetBarcode,
        string FirstTargetBarcode,
        string SecondTargetBarcode);
}
