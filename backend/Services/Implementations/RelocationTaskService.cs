using backend.Common.Exceptions;
using backend.Data;
using backend.DTOs;
using backend.Entities;
using backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace backend.Services.Implementations;

public class RelocationTaskService(MtsDbContext context) : IRelocationTaskService
{
    private static readonly Func<MtsDbContext, IQueryable<RelocationTaskResponse>> Project = context =>
        context.RelocationTasks.AsNoTracking().Select(t => new RelocationTaskResponse(
            t.Id, t.Status, t.CreatedAt, t.CompletedAt, t.UnitId, t.Unit.Barcode,
            t.FromAddressId, t.FromAddress.Code, t.ToAddressId, t.ToAddress.Code,
            t.CompletedByUserId, t.CompletedByUser != null ? t.CompletedByUser.Username : null));

    public async Task<List<RelocationTaskResponse>> GetAllAsync(CancellationToken ct = default)
    {
        return await context.RelocationTasks
            .AsNoTracking()
            .OrderByDescending(t => t.Id)
            .Select(t => new RelocationTaskResponse(
                t.Id,
                t.Status,
                t.CreatedAt,
                t.CompletedAt,
                t.UnitId,
                t.Unit.Barcode,
                t.FromAddressId,
                t.FromAddress.Code,
                t.ToAddressId,
                t.ToAddress.Code,
                t.CompletedByUserId,
                t.CompletedByUser != null ? t.CompletedByUser.Username : null
            ))
            .ToListAsync(ct);
    }

    public async Task<List<RelocationTargetResponse>> GetEligibleTargetsAsync(
        long unitId, CancellationToken ct = default)
    {
        var unit = await context.Units
            .AsNoTracking()
            .Where(u => u.Id == unitId)
            .Select(u => new
            {
                u.AddressId,
                u.Status,
                u.Batch.ExpirationDate,
                ChemicalStopped = u.Batch.Chemical.Stopped,
                u.Batch.Chemical.StorageType
            })
            .FirstOrDefaultAsync(ct);

        if (unit is null)
            throw new InvalidOperationException("Taşınacak stok birimi bulunamadı!");

        EnsureUnitCanBeRelocated(unit.Status, unit.ExpirationDate, unit.ChemicalStopped);

        return await context.Addresses
            .AsNoTracking()
            .Where(a => a.Id != unit.AddressId && a.StorageType == unit.StorageType)
            .Select(a => new
            {
                Address = a,
                Occupancy = a.Units.Count(u => u.Status == "InStock" || u.Status == "AVAILABLE")
            })
            .Where(x => !x.Address.MaxCapacity.HasValue || x.Occupancy < x.Address.MaxCapacity.Value)
            .OrderBy(x => x.Address.Code)
            .Select(x => new RelocationTargetResponse(
                x.Address.Id,
                x.Address.Code,
                x.Address.StorageType,
                x.Address.MaxCapacity,
                x.Occupancy,
                x.Address.MaxCapacity.HasValue ? x.Address.MaxCapacity.Value - x.Occupancy : null))
            .ToListAsync(ct);
    }

    public async Task<RelocationTaskResponse> CreateTaskAsync(
        CreateRelocationTaskRequest request, CancellationToken ct = default)
    {
        var unit = await context.Units
            .Include(u => u.Batch).ThenInclude(b => b.Chemical)
            .FirstOrDefaultAsync(u => u.Id == request.UnitId, ct);
        if (unit is null)
            throw new InvalidOperationException("Taşınacak birim (Unit) bulunamadı!");
        EnsureUnitCanBeRelocated(unit.Status, unit.Batch.ExpirationDate, unit.Batch.Chemical.Stopped);
        if (!unit.AddressId.HasValue)
            throw new InvalidOperationException("Birim şu anda hiçbir rafta kayıtlı değil!");
        if (unit.AddressId.Value == request.ToAddressId)
            throw new InvalidOperationException("Birim zaten hedef adreste bulunuyor!");

        var target = await context.Addresses
            .AsNoTracking()
            .Where(a => a.Id == request.ToAddressId)
            .Select(a => new
            {
                a.Code,
                a.StorageType,
                a.MaxCapacity,
                Occupancy = a.Units.Count(u => u.Status == "InStock" || u.Status == "AVAILABLE")
            })
            .FirstOrDefaultAsync(ct);

        if (target is null)
            throw new InvalidOperationException("Hedef adres bulunamadı!");
        if (target.MaxCapacity.HasValue && target.Occupancy >= target.MaxCapacity.Value)
            throw new InvalidOperationException($"Hedef adres ({target.Code}) maksimum kapasitesine ulaştı!");
        if (!string.Equals(target.StorageType, unit.Batch.Chemical.StorageType, StringComparison.OrdinalIgnoreCase))
            throw new BusinessRuleException(
                $"Hedef rafın türü ({target.StorageType}), kimyasalın depolama türü ({unit.Batch.Chemical.StorageType}) ile uyumlu değil!");

        var hasPendingTask = await context.RelocationTasks
            .AnyAsync(t => t.UnitId == unit.Id && t.Status == "Pending", ct);
        if (hasPendingTask)
            throw new BusinessRuleException("Bu stok birimi için zaten bekleyen bir raf taşıma görevi var!");

        var task = new RelocationTask
        {
            UnitId = unit.Id,
            FromAddressId = unit.AddressId.Value,
            ToAddressId = request.ToAddressId,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };

        context.RelocationTasks.Add(task);
        await context.SaveChangesAsync(ct);
        return await Project(context).FirstAsync(t => t.Id == task.Id, ct);
    }

    public async Task<bool> CompleteTaskAsync(
        long taskId, CompleteRelocationTaskRequest request, long? completedByUserId, CancellationToken ct = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(ct);
        var task = await context.RelocationTasks
            .Include(t => t.Unit).ThenInclude(u => u.Batch).ThenInclude(b => b.Chemical)
            .FirstOrDefaultAsync(t => t.Id == taskId, ct);

        if (task is null || task.Status != "Pending")
            return false;

        if (!string.Equals(request.UnitBarcode.Trim(), task.Unit.Barcode, StringComparison.OrdinalIgnoreCase))
            throw new BusinessRuleException("Okutulan stok birimi bu taşıma görevine ait değil!");

        EnsureUnitCanBeRelocated(task.Unit.Status, task.Unit.Batch.ExpirationDate, task.Unit.Batch.Chemical.Stopped);

        if (task.Unit.AddressId != task.FromAddressId)
            throw new BusinessRuleException("Stok birimi artık görevin kaynak rafında değil. Görev yenilenmelidir!");

        var targetAddress = await context.Addresses
            .FromSqlInterpolated($"SELECT * FROM addresses WHERE \"Id\" = {task.ToAddressId} FOR UPDATE")
            .Include(a => a.Units)
            .SingleOrDefaultAsync(ct);

        if (targetAddress is null)
            throw new BusinessRuleException("Hedef raf bulunamadı!");

        var expectedAddressBarcode = $"ADR-{targetAddress.Code}";
        if (!string.Equals(request.TargetAddressBarcode.Trim(), expectedAddressBarcode, StringComparison.OrdinalIgnoreCase))
            throw new BusinessRuleException($"Yanlış raf okutuldu. Beklenen hedef raf: {targetAddress.Code}");

        if (!string.Equals(targetAddress.StorageType, task.Unit.Batch.Chemical.StorageType, StringComparison.OrdinalIgnoreCase))
            throw new BusinessRuleException("Hedef raf kimyasalın depolama türüyle uyumlu değil!");

        if (targetAddress.MaxCapacity.HasValue &&
            targetAddress.Units.Count(u => u.Status == "InStock" || u.Status == "AVAILABLE") >= targetAddress.MaxCapacity.Value)
        {
            throw new BusinessRuleException(
                $"Hedef raf ({targetAddress.Code}) maksimum kapasitesine ulaştığı için taşıma tamamlanamaz!");
        }

        task.Unit.AddressId = task.ToAddressId;
        task.Unit.Version++;
        task.Status = "Completed";
        task.CompletedAt = DateTime.UtcNow;
        task.CompletedByUserId = completedByUserId;
        try
        {
            await context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new BusinessRuleException("Stok birimi başka bir işlem tarafından değiştirildi. Görevi yenileyiniz!");
        }
        return true;
    }

    public async Task<bool> CancelTaskAsync(long taskId, CancellationToken ct = default)
    {
        var task = await context.RelocationTasks.FirstOrDefaultAsync(t => t.Id == taskId, ct);
        if (task is null || task.Status != "Pending")
            return false;

        task.Status = "Cancelled";
        task.CompletedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(ct);
        return true;
    }

    private static void EnsureUnitCanBeRelocated(string status, DateTime? expirationDate, bool chemicalStopped)
    {
        if (status != "InStock" && status != "AVAILABLE")
            throw new BusinessRuleException("Yalnızca stokta bulunan bir varil taşınabilir!");
        if (expirationDate.HasValue && expirationDate.Value < DateTime.UtcNow)
            throw new BusinessRuleException("Son kullanma tarihi geçmiş bir varil taşınamaz!");
        if (chemicalStopped)
            throw new BusinessRuleException("Durdurulmuş kimyasala ait bir varil taşınamaz!");
    }
}
