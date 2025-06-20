// USheets.Api.Data/ApiDbContext.cs

using Microsoft.EntityFrameworkCore;
using USheets.Api.Models;
using System.Text.Json;

namespace USheets.Api.Data
{
    public class ApiDbContext : DbContext
    {
        public ApiDbContext(DbContextOptions<ApiDbContext> options) : base(options)
        {
        }

        public DbSet<Timesheet> Timesheets { get; set; }
        public DbSet<TimesheetLine> TimesheetLines { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Configure the Timesheet (Parent) entity
            modelBuilder.Entity<Timesheet>(entity =>
            {
                // Create a unique index to prevent a user from creating more than
                // one timesheet for the same week. This enforces data integrity.
                entity.HasIndex(t => new { t.UserId, t.WeekStartDate }).IsUnique();
            });

            // Configure the TimesheetLine (Child) entity
            modelBuilder.Entity<TimesheetLine>(entity =>
            {
                // The JSON conversion for the Hours dictionary now applies to TimesheetLine
                entity.Property(e => e.Hours)
                    .HasConversion(
                        v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null),
                        v => JsonSerializer.Deserialize<Dictionary<DayOfWeek, double>>(v, (JsonSerializerOptions)null)!,
                        new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<Dictionary<DayOfWeek, double>>(
                            (c1, c2) => c1!.SequenceEqual(c2!),
                            c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.Key.GetHashCode(), v.Value.GetHashCode())),
                            c => c.ToDictionary(kv => kv.Key, kv => kv.Value)));

                // Configure the one-to-many relationship
                entity.HasOne(l => l.Timesheet)
                      .WithMany(t => t.Lines)
                      .HasForeignKey(l => l.TimesheetId)
                      .OnDelete(DeleteBehavior.Cascade); // Deleting a timesheet will also delete its lines
            });
        }
    }
}