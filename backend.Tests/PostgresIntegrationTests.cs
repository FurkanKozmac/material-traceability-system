using System.Security.Cryptography;
using System.Text;
using backend.Common.Exceptions;
using backend.DTOs;
using backend.Entities;
using backend.Services.Implementations;
using Microsoft.EntityFrameworkCore;

namespace backend.Tests;

[Collection(PostgresTestCollection.Name)]
public class PostgresIntegrationTests(PostgresTestDatabase database)
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Refresh_ValidRefreshToken_RotatesTokenPair()
    {
        // Arrange
        await using var context = database.CreateContext();
        var user = await AddUserAsync(context, $"refresh-{Guid.NewGuid():N}");
        var authService = AuthServiceTests.CreateService(context);
        var login = await authService.LoginAsync(new LoginRequest(user.Username, "password", "DB"));

        // Act
        // JWT expiry claims have second-level precision; wait so the rotated token is distinct.
        await Task.Delay(TimeSpan.FromSeconds(1.1));
        var refreshed = await authService.RefreshAsync(login.RefreshToken);

        // Assert
        Assert.NotEqual(login.AccessToken, refreshed.AccessToken);
        Assert.NotEqual(login.RefreshToken, refreshed.RefreshToken);
        var oldHash = HashToken(login.RefreshToken);
        var oldToken = await context.RefreshTokens.SingleAsync(token => token.TokenHash == oldHash);
        Assert.NotNull(oldToken.RevokedAt);
        Assert.Equal(HashToken(refreshed.RefreshToken), oldToken.ReplacedByTokenHash);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Refresh_AlreadyUsedRefreshToken_RejectsReuse()
    {
        // Arrange
        await using var context = database.CreateContext();
        var user = await AddUserAsync(context, $"reuse-{Guid.NewGuid():N}");
        var authService = AuthServiceTests.CreateService(context);
        var login = await authService.LoginAsync(new LoginRequest(user.Username, "password", "DB"));
        // JWT expiry claims have second-level precision; wait so the first rotation can complete.
        await Task.Delay(TimeSpan.FromSeconds(1.1));
        await authService.RefreshAsync(login.RefreshToken);

        // Act
        var act = () => authService.RefreshAsync(login.RefreshToken);

        // Assert
        await Assert.ThrowsAsync<BusinessRuleException>(act);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task CreateBatch_ValidBatch_CreatesInitialUnits()
    {
        // Arrange
        await using var context = database.CreateContext();
        var chemical = new Chemical
        {
            Name = $"Batch chemical {Guid.NewGuid():N}",
            ChemicalCode = $"BC-{Guid.NewGuid():N}",
            StorageType = "GENERAL"
        };
        var address = new Address
        {
            Code = $"Batch rack {Guid.NewGuid():N}",
            StorageType = chemical.StorageType,
            MaxCapacity = 10
        };
        context.AddRange(chemical, address);
        await context.SaveChangesAsync();
        var service = new BatchService(context);
        const int initialQuantity = 3;

        // Act
        var result = await service.CreateAsync(new CreateBatchRequest(
            $"LOT-{Guid.NewGuid():N}", "Supplier", initialQuantity, null, chemical.Id));

        // Assert
        Assert.Equal(initialQuantity, await context.Units.CountAsync(unit => unit.BatchId == result.Id));
        Assert.Equal(initialQuantity, result.UnitCount);
        var unitAddressIds = await context.Units
            .Where(unit => unit.BatchId == result.Id)
            .Select(unit => unit.AddressId)
            .Distinct()
            .ToListAsync();
        Assert.Single(unitAddressIds);
        var acceptedAddressStorageType = await context.Addresses
            .Where(candidate => candidate.Id == unitAddressIds[0])
            .Select(candidate => candidate.StorageType)
            .SingleAsync();
        Assert.Equal(chemical.StorageType, acceptedAddressStorageType);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task CompleteTask_WrongTargetAddressBarcode_RejectsCompletion()
    {
        // Arrange
        await using var context = database.CreateContext();
        var (_, _, source, target, unit, task) = await AddRelocationAsync(context);
        var service = new RelocationTaskService(context);

        // Act
        var act = () => service.CompleteTaskAsync(task.Id, new CompleteRelocationTaskRequest(
            unit.Barcode, "ADR-WRONG"), null);

        // Assert
        await Assert.ThrowsAsync<BusinessRuleException>(act);
        Assert.Equal(source.Id, unit.AddressId);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task CompleteTask_FullTargetAddress_RejectsCompletion()
    {
        // Arrange
        await using var context = database.CreateContext();
        var (_, _, _, target, unit, task) = await AddRelocationAsync(context, targetCapacity: 1);
        context.Units.Add(new Unit
        {
            Barcode = $"FILLER-{Guid.NewGuid():N}",
            BatchId = unit.BatchId,
            AddressId = target.Id,
            Status = "InStock",
            Version = 1
        });
        await context.SaveChangesAsync();
        var service = new RelocationTaskService(context);

        // Act
        var act = () => service.CompleteTaskAsync(task.Id, new CompleteRelocationTaskRequest(
            unit.Barcode, $"ADR-{target.Code}"), null);

        // Assert
        await Assert.ThrowsAsync<BusinessRuleException>(act);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task CompleteTask_ValidUnitAndTarget_MovesUnitAndCompletesTask()
    {
        // Arrange
        await using var context = database.CreateContext();
        var (_, _, _, target, unit, task) = await AddRelocationAsync(context);
        var service = new RelocationTaskService(context);

        // Act
        var completed = await service.CompleteTaskAsync(task.Id, new CompleteRelocationTaskRequest(
            unit.Barcode, $"ADR-{target.Code}"), null);

        // Assert
        Assert.True(completed);
        Assert.Equal(target.Id, unit.AddressId);
        Assert.Equal("Completed", task.Status);
        Assert.NotNull(task.CompletedAt);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task AddPendingTask_DuplicateUnitPendingStatus_RejectsFilteredUniqueIndex()
    {
        // Arrange
        await using var context = database.CreateContext();
        var (_, _, source, target, unit, _) = await AddRelocationAsync(context, createTask: false);
        context.RelocationTasks.Add(new RelocationTask
        {
            UnitId = unit.Id,
            FromAddressId = source.Id,
            ToAddressId = target.Id,
            Status = "Pending"
        });
        await context.SaveChangesAsync();
        context.RelocationTasks.Add(new RelocationTask
        {
            UnitId = unit.Id,
            FromAddressId = source.Id,
            ToAddressId = target.Id,
            Status = "Pending"
        });

        // Act
        var act = () => context.SaveChangesAsync();

        // Assert
        await Assert.ThrowsAsync<DbUpdateException>(act);
    }

    private static async Task<User> AddUserAsync(
        backend.Data.MtsDbContext context, string username)
    {
        var user = new User { Username = username, Active = true };
        user.Password = new Microsoft.AspNetCore.Identity.PasswordHasher<User>()
            .HashPassword(user, "password");
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    private static async Task<(Chemical Chemical, Batch Batch, Address Source, Address Target, Unit Unit, RelocationTask Task)>
        AddRelocationAsync(
            backend.Data.MtsDbContext context,
            int targetCapacity = 3,
            bool createTask = true)
    {
        var chemical = new Chemical
        {
            Name = $"Relocation chemical {Guid.NewGuid():N}",
            ChemicalCode = $"RC-{Guid.NewGuid():N}",
            StorageType = "GENERAL"
        };
        var batch = new Batch
        {
            BatchNo = $"RLOT-{Guid.NewGuid():N}",
            Chemical = chemical,
            InitialQuantity = 1
        };
        var source = new Address
        {
            Code = $"SOURCE-{Guid.NewGuid():N}",
            StorageType = chemical.StorageType,
            MaxCapacity = 3
        };
        var target = new Address
        {
            Code = $"TARGET-{Guid.NewGuid():N}",
            StorageType = chemical.StorageType,
            MaxCapacity = targetCapacity
        };
        var unit = new Unit
        {
            Barcode = $"UNIT-{Guid.NewGuid():N}",
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
            Status = "Pending"
        };
        context.Units.Add(unit);
        if (createTask)
            context.RelocationTasks.Add(task);
        else
            context.Addresses.Add(target);
        await context.SaveChangesAsync();
        return (chemical, batch, source, target, unit, task);
    }

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
