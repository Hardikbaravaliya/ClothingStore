using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ClothingStore.Infrastructure.Data;

/// <summary>DB stores UTC only. Values read back are marked DateTimeKind.Utc.</summary>
public sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    v => v.Kind == DateTimeKind.Local ? v.ToUniversalTime() : DateTime.SpecifyKind(v, DateTimeKind.Utc),
    v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
