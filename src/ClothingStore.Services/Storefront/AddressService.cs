using ClothingStore.Contracts.Account;
using ClothingStore.Core.Common;
using ClothingStore.Core.Entities;
using ClothingStore.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ClothingStore.Services.Storefront;

public interface IAddressService
{
    Task<IReadOnlyList<AddressDto>> GetAsync(int customerId, CancellationToken ct = default);
    Task<Result<AddressDto>> AddAsync(int customerId, AddressRequest request, CancellationToken ct = default);
    Task<Result> UpdateAsync(int customerId, int addressId, AddressRequest request, CancellationToken ct = default);
    Task<Result> DeleteAsync(int customerId, int addressId, CancellationToken ct = default);
}

public sealed class AddressService(AppDbContext db) : IAddressService
{
    public const int MaxAddresses = 10;

    public async Task<IReadOnlyList<AddressDto>> GetAsync(int customerId, CancellationToken ct = default) =>
        await db.Addresses.AsNoTracking()
            .Where(a => a.CustomerId == customerId)
            .OrderByDescending(a => a.IsDefault).ThenByDescending(a => a.CreatedAt)
            .Select(a => ToDto(a))
            .ToListAsync(ct);

    public async Task<Result<AddressDto>> AddAsync(int customerId, AddressRequest request, CancellationToken ct = default)
    {
        var existing = await db.Addresses.Where(a => a.CustomerId == customerId).ToListAsync(ct);
        if (existing.Count >= MaxAddresses)
            return Result<AddressDto>.Fail($"You can save at most {MaxAddresses} addresses.");

        var address = new Address { CustomerId = customerId };
        Apply(address, request);
        address.IsDefault = request.IsDefault || existing.Count == 0;
        if (address.IsDefault)
            existing.ForEach(a => a.IsDefault = false);

        db.Addresses.Add(address);
        await db.SaveChangesAsync(ct);
        return Result<AddressDto>.Ok(ToDto(address));
    }

    public async Task<Result> UpdateAsync(int customerId, int addressId, AddressRequest request, CancellationToken ct = default)
    {
        var addresses = await db.Addresses.Where(a => a.CustomerId == customerId).ToListAsync(ct);
        var address = addresses.FirstOrDefault(a => a.Id == addressId);
        if (address is null)
            return Result.Fail("Address not found.");

        Apply(address, request);
        if (request.IsDefault)
            addresses.ForEach(a => a.IsDefault = a.Id == addressId);

        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> DeleteAsync(int customerId, int addressId, CancellationToken ct = default)
    {
        var addresses = await db.Addresses.Where(a => a.CustomerId == customerId).ToListAsync(ct);
        var address = addresses.FirstOrDefault(a => a.Id == addressId);
        if (address is null)
            return Result.Fail("Address not found.");

        db.Addresses.Remove(address);
        if (address.IsDefault && addresses.FirstOrDefault(a => a.Id != addressId) is { } next)
            next.IsDefault = true;

        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    private static void Apply(Address address, AddressRequest request)
    {
        address.FullName = request.FullName.Trim();
        address.Phone = request.Phone.Trim();
        address.Line1 = request.Line1.Trim();
        address.Line2 = request.Line2?.Trim();
        address.Landmark = request.Landmark?.Trim();
        address.City = request.City.Trim();
        address.State = request.State.Trim();
        address.Pincode = request.Pincode.Trim();
        address.Country = string.IsNullOrWhiteSpace(request.Country) ? "India" : request.Country.Trim();
    }

    private static AddressDto ToDto(Address a) =>
        new(a.Id, a.FullName, a.Phone, a.Line1, a.Line2, a.Landmark, a.City, a.State, a.Pincode, a.Country, a.IsDefault);
}
