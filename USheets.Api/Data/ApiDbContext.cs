using Microsoft.EntityFrameworkCore;
using USheets.Api.Models;
using System.Text.Json;
using System.Collections.Generic;

namespace USheets.Api.Data
{
    public class ApiDbContext : DbContext
    {
        public ApiDbContext(DbContextOptions<ApiDbContext> options) : base(options)
        {
        }

        public DbSet<TimesheetEntry> TimesheetEntries { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TimesheetEntry>()
                .Property(e => e.Hours)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null),
                    v => JsonSerializer.Deserialize<Dictionary<DayOfWeek, double>>(v, (JsonSerializerOptions)null),
                    new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<Dictionary<DayOfWeek, double>>(
                        (c1, c2) => c1.SequenceEqual(c2),
                        c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.Key.GetHashCode(), v.Value.GetHashCode())),
                        c => c.ToDictionary(kv => kv.Key, kv => kv.Value)));
        }
    }
}
