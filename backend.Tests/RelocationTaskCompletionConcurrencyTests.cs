using backend.Common.Exceptions;
using backend.Data;
using backend.DTOs;
using backend.Entities;
using backend.Services.Implementations;
using backend.Services.Implementations.Notifications;
using Microsoft.EntityFrameworkCore;

namespace backend.Tests;

[Collection(PostgresTestCollection.Name)]
[Trait("Category", "Testcontainers")]
public class RelocationTaskCompletionConcurrencyTests(PostgresTestDatabase database)
{
    [Fact]
    public async Task CompleteSameTask_ConcurrentRequests_OneSucceedsAndOtherRejected()
    {
        // Arrange
        var setup = await PrepareTaskAsync();

        // Adres kilitlerini test tarafında tutarak iki isteğin de kilit öncesi
        // okumalarını tamamlamasını, ardından aynı anda ilerlemesini sağla.
        await using (var blocker = database.CreateContext())
        {
            await using var hold = await blocker.Database.BeginTransactionAsync();
            await blocker.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT \"Id\" FROM addresses WHERE \"Id\" IN ({setup.SourceAddressId}, {setup.TargetAddressId}) FOR UPDATE");

            var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var attempts = Enumerable.Range(0, 2)
                .Select(_ => CompleteAsync(setup.TaskId, setup.UnitBarcode, setup.TargetBarcode, start))
                .ToArray();

            start.SetResult();
            await Task.Delay(TimeSpan.FromSeconds(1));
            await hold.CommitAsync();

            var results = await Task.WhenAll(attempts).WaitAsync(TimeSpan.FromSeconds(30));

            // Assert
            Assert.Equal(1, results.Count(result => result.Success));
            var rejected = Assert.Single(results, result => !result.Success);
            var exception = Assert.IsType<BusinessRuleException>(rejected.Error);
            Assert.Equal(
                "Stok birimi başka bir işlem tarafından değiştirildi. Görevi yenileyiniz!",
                exception.Message);
        }

        await using var verify = database.CreateContext();
        var unit = await verify.Units.AsNoTracking().SingleAsync(u => u.Id == setup.UnitId);
        Assert.Equal(setup.TargetAddressId, unit.AddressId);
        Assert.Equal(2, unit.Version);
        Assert.Equal(1, await verify.Units.CountAsync(u => u.AddressId == setup.TargetAddressId));

        var task = await verify.RelocationTasks.AsNoTracking().SingleAsync(t => t.Id == setup.TaskId);
        Assert.Equal("Completed", task.Status);
        Assert.NotNull(task.CompletedAt);
    }

    private async Task<CompletionAttempt> CompleteAsync(
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
            var success = await service.CompleteTaskAsync(
                taskId,
                new CompleteRelocationTaskRequest(unitBarcode, targetAddressBarcode),
                null);
            return new CompletionAttempt(success, null);
        }
        catch (Exception exception)
        {
            return new CompletionAttempt(false, exception);
        }
    }

    private async Task<RelocationTaskSetup> PrepareTaskAsync()
    {
        await using var context = database.CreateContext();
        var chemical = new Chemical
        {
            Name = $"Relocation completion chemical {Guid.NewGuid():N}",
            ChemicalCode = $"RC-CHEM-{Guid.NewGuid():N}",
            StorageType = "GENERAL"
        };
        var batch = new Batch
        {
            BatchNo = $"RC-BATCH-{Guid.NewGuid():N}",
            Chemical = chemical,
            InitialQuantity = 1
        };
        var source = new Address
        {
            Code = $"RC-SRC-{Guid.NewGuid():N}",
            StorageType = "GENERAL",
            MaxCapacity = 5
        };
        var target = new Address
        {
            Code = $"RC-DST-{Guid.NewGuid():N}",
            StorageType = "GENERAL",
            MaxCapacity = 5
        };
        var unit = new Unit
        {
            Barcode = $"RC-UNIT-{Guid.NewGuid():N}",
            Batch = batch,
            Address = source,
            Status = "InStock",
            Version = 1
        };
        var task = new RelocationTask
        {
            Unit = unit,
            FromAddress = source,
            ToAddress = target,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };

        context.AddRange(unit, task);
        await context.SaveChangesAsync();

        return new RelocationTaskSetup(
            task.Id,
            unit.Id,
            unit.Barcode,
            source.Id,
            target.Id,
            $"ADR-{target.Code}");
    }

    private sealed record CompletionAttempt(bool Success, Exception? Error);

    private sealed record RelocationTaskSetup(
        long TaskId,
        long UnitId,
        string UnitBarcode,
        long SourceAddressId,
        long TargetAddressId,
        string TargetBarcode);
}
