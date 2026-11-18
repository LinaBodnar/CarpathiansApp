using Carpathians.Dal.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Carpathians.Dal.Configurations;

public class IntegrationEventLogEntryConfiguration : IEntityTypeConfiguration<IntegrationEventLogEntry>
{
    public void Configure(EntityTypeBuilder<IntegrationEventLogEntry> builder)
    {
        builder.ToTable("IntegrationEventLog");

        builder.HasKey(e => e.EventId);
        builder.Property(e => e.EventId).ValueGeneratedNever();

        builder.Property(e => e.EventType).IsRequired().HasMaxLength(200);
        builder.Property(e => e.Exchange).IsRequired().HasMaxLength(200);
        builder.Property(e => e.RoutingKey).IsRequired().HasMaxLength(200);
        builder.Property(e => e.Content).IsRequired();
        builder.Property(e => e.CorrelationId).HasMaxLength(100);
        builder.Property(e => e.TraceParent).HasMaxLength(100);
        builder.Property(e => e.LastError).HasMaxLength(1000);
        builder.Property(e => e.State).HasConversion<int>();
        builder.Property(e => e.OccurredAt).HasColumnType("datetime2");
        builder.Property(e => e.PublishedAt).HasColumnType("datetime2");

        // фоновий publisher вибирає неопубліковані події в порядку виникнення
        builder.HasIndex(e => new { e.State, e.OccurredAt })
            .HasDatabaseName("IX_IntegrationEventLog_State_OccurredAt");
    }
}
