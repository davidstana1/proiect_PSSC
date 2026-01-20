using Microsoft.EntityFrameworkCore;
using ProiectPSSC.Domain.Billing;
using ProiectPSSC.Domain.Orders;
using ProiectPSSC.Domain.Shipping;
using ProiectPSSC.Infrastructure.Persistence.Entities;

namespace ProiectPSSC.Infrastructure.Persistence;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Order> Orders => Set<Order>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<Shipment> Shipments => Set<Shipment>();
    public DbSet<OutboxEventEntity> OutboxEvents => Set<OutboxEventEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>(b =>
        {
            b.ToTable("orders");
            b.HasKey(x => x.Id);

            b.Property(x => x.Id)
                .HasConversion(v => v.Value, v => new OrderId(v))
                .HasColumnName("id");

            b.Property(x => x.CustomerEmail).HasColumnName("customer_email").IsRequired();
            b.Property(x => x.Status).HasColumnName("status").IsRequired();
            b.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();

            b.OwnsMany(x => x.Lines, lb =>
            {
                lb.ToTable("order_lines");
                lb.WithOwner().HasForeignKey("order_id");
                lb.Property(x => x.ProductCode).HasColumnName("product_code");
                lb.Property(x => x.Quantity).HasColumnName("quantity");
                lb.Property(x => x.UnitPrice).HasColumnName("unit_price");

                lb.HasKey("order_id", nameof(OrderLine.ProductCode));
            });
        });

        modelBuilder.Entity<Invoice>(b =>
        {
            b.ToTable("invoices");
            b.HasKey(x => x.Id);

            b.Property(x => x.Id).HasColumnName("id");
            b.Property(x => x.Number).HasColumnName("number").IsRequired();

            b.Property(x => x.OrderId)
                .HasConversion(v => v.Value, v => new OrderId(v))
                .HasColumnName("order_id")
                .IsRequired();

            b.Property(x => x.BillingEmail).HasColumnName("billing_email").IsRequired();
            b.Property(x => x.Currency).HasColumnName("currency").IsRequired();
            b.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
            b.Property(x => x.DueDate).HasColumnName("due_date").IsRequired();
            b.Property(x => x.Status).HasColumnName("status").IsRequired();

            // Amount is computed from lines; we don't store it as a column.
            b.Ignore(x => x.Amount);

            b.HasIndex(x => x.OrderId).IsUnique();
            b.HasIndex(x => x.Number).IsUnique();

            b.OwnsMany(x => x.Lines, lb =>
            {
                lb.ToTable("invoice_lines");
                lb.WithOwner().HasForeignKey("invoice_id");
                lb.Property(x => x.ProductCode).HasColumnName("product_code");
                lb.Property(x => x.Quantity).HasColumnName("quantity");
                lb.Property(x => x.UnitPrice).HasColumnName("unit_price");

                lb.HasKey("invoice_id", nameof(InvoiceLine.ProductCode));
            });
        });

        modelBuilder.Entity<Shipment>(b =>
        {
            b.ToTable("shipments");
            b.HasKey(x => x.Id);

            b.Property(x => x.Id).HasColumnName("id");

            b.Property(x => x.OrderId)
                .HasConversion(v => v.Value, v => new OrderId(v))
                .HasColumnName("order_id")
                .IsRequired();

            b.Property(x => x.TrackingNumber).HasColumnName("tracking_number");
            b.Property(x => x.Carrier).HasColumnName("carrier");
            b.Property(x => x.ShippedAt).HasColumnName("shipped_at").IsRequired();

            b.HasIndex(x => x.OrderId).IsUnique();
        });

        modelBuilder.Entity<OutboxEventEntity>(b =>
        {
            b.ToTable("outbox_events");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnName("id");
            b.Property(x => x.OccurredAt).HasColumnName("occurred_at");
            b.Property(x => x.Type).HasColumnName("type");
            b.Property(x => x.Payload).HasColumnName("payload");
            b.Property(x => x.ProcessedAt).HasColumnName("processed_at");
            b.Property(x => x.Attempts).HasColumnName("attempts");
            b.HasIndex(x => x.ProcessedAt);
        });
    }
}
