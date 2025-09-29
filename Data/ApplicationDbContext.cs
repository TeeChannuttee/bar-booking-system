using BarBookingSystem.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BarBookingSystem.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

        public DbSet<Branch> Branches { get; set; }
        public DbSet<Table> Tables { get; set; }
        public DbSet<Booking> Bookings { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<PromoCode> PromoCodes { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Set schema for PostgreSQL (Supabase)
            builder.HasDefaultSchema("public");

            // ✅ กำหนดค่า DateTime columns สำหรับ PostgreSQL
            ConfigureDateTimeColumns(builder);

            // Configure relationships
            ConfigureRelationships(builder);

            // Configure indexes for performance
            ConfigureIndexes(builder);

            // Configure decimal precision for PostgreSQL
            ConfigureDecimalPrecision(builder);
        }

        private void ConfigureDateTimeColumns(ModelBuilder builder)
        {
            // ✅ Booking entity DateTime configurations
            builder.Entity<Booking>(entity =>
            {
                // BookingDate เป็นวันที่เท่านั้น (ไม่มีเวลา)
                entity.Property(e => e.BookingDate)
                      .HasColumnType("date");

                // StartTime และ EndTime เป็น time type
                entity.Property(e => e.StartTime)
                      .HasColumnType("time");

                entity.Property(e => e.EndTime)
                      .HasColumnType("time");

                // CreatedAt เก็บ timestamp พร้อม timezone
                entity.Property(e => e.CreatedAt)
                      .HasColumnType("timestamp with time zone");
            });

            // ✅ Payment entity DateTime configurations
            builder.Entity<Payment>(entity =>
            {
                entity.Property(p => p.PaymentDate)
                .HasColumnType("timestamp with time zone");

                entity.Property(p => p.Amount)
                      .HasPrecision(10, 2);

                entity.Property(p => p.TransactionDetails)
                      .HasColumnType("jsonb")
                      .HasDefaultValueSql("'{}'::jsonb") // ✅ default ที่ DB
                      .IsRequired();
            });

            // ✅ PromoCode entity DateTime configurations
            builder.Entity<PromoCode>(entity =>
            {
                entity.Property(e => e.ValidFrom)
                      .HasColumnType("timestamp with time zone");

                entity.Property(e => e.ValidTo)
                      .HasColumnType("timestamp with time zone");

                // CreatedAt และ ModifiedAt (ถ้ามี properties เหล่านี้)
                // entity.Property(e => e.CreatedAt)
                //       .HasColumnType("timestamp with time zone");
            });

            // ✅ Branch entity DateTime configurations (ถ้ามี properties เหล่านี้)
            // builder.Entity<Branch>(entity =>
            // {
            //     entity.Property(e => e.CreatedAt)
            //           .HasColumnType("timestamp with time zone");
            //
            //     entity.Property(e => e.ModifiedAt)
            //           .HasColumnType("timestamp with time zone");
            // });
        }

        private void ConfigureRelationships(ModelBuilder builder)
        {
            builder.Entity<Booking>()
                .HasOne(b => b.User)
                .WithMany(u => u.Bookings)
                .HasForeignKey(b => b.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Booking>()
                .HasOne(b => b.Table)
                .WithMany(t => t.Bookings)
                .HasForeignKey(b => b.TableId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Payment>()
                .HasOne(p => p.Booking)
                .WithOne(b => b.Payment)
                .HasForeignKey<Payment>(p => p.BookingId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Table>()
                .HasOne(t => t.Branch)
                .WithMany(b => b.Tables)
                .HasForeignKey(t => t.BranchId)
                .OnDelete(DeleteBehavior.Cascade);
        }

        private void ConfigureIndexes(ModelBuilder builder)
        {
            // Indexes for performance
            builder.Entity<Booking>()
                .HasIndex(b => new { b.BookingDate, b.Status })
                .HasDatabaseName("IX_Booking_Date_Status");

            builder.Entity<Booking>()
                .HasIndex(b => b.BookingCode)
                .IsUnique();

            // ✅ เพิ่ม index สำหรับการค้นหาตาม UserId
            builder.Entity<Booking>()
                .HasIndex(b => b.UserId)
                .HasDatabaseName("IX_Booking_UserId");

            // ✅ เพิ่ม index สำหรับการค้นหาตาม TableId และวันที่
            builder.Entity<Booking>()
                .HasIndex(b => new { b.TableId, b.BookingDate })
                .HasDatabaseName("IX_Booking_TableId_Date");

            builder.Entity<PromoCode>()
                .HasIndex(p => p.Code)
                .IsUnique();

            builder.Entity<ApplicationUser>()
                .HasIndex(u => u.Email)
                .IsUnique();

            // ✅ เพิ่ม index สำหรับ PromoCode validity
            builder.Entity<PromoCode>()
                .HasIndex(p => new { p.IsActive, p.ValidFrom, p.ValidTo })
                .HasDatabaseName("IX_PromoCode_Validity");
        }

        private void ConfigureDecimalPrecision(ModelBuilder builder)
        {
            // Configure decimal precision for PostgreSQL
            builder.Entity<Table>(entity =>
            {
                entity.Property(t => t.MinimumSpend)
                      .HasPrecision(10, 2);

                entity.Property(t => t.BasePrice)
                      .HasPrecision(10, 2);
            });

            builder.Entity<Booking>(entity =>
            {
                entity.Property(b => b.TotalAmount)
                      .HasPrecision(10, 2);

                entity.Property(b => b.DepositAmount)
                      .HasPrecision(10, 2);

                entity.Property(b => b.DiscountAmount)
                      .HasPrecision(10, 2);
            });

            builder.Entity<Payment>(entity =>
            {
                entity.Property(p => p.Amount)
                      .HasPrecision(10, 2);

                // RefundAmount (ถ้ามี property นี้)
                // entity.Property(p => p.RefundAmount)
                //       .HasPrecision(10, 2);
            });

            // ✅ กำหนดค่า decimal สำหรับ PromoCode (เฉพาะที่มีจริง)
            builder.Entity<PromoCode>(entity =>
            {
                // ตรวจสอบให้แน่ใจว่า properties เหล่านี้มีอยู่จริงใน PromoCode model
                entity.Property(p => p.DiscountAmount)
                      .HasPrecision(10, 2);

                entity.Property(p => p.DiscountPercent)
                      .HasPrecision(5, 2);

                entity.Property(p => p.MinimumSpend)
                      .HasPrecision(10, 2);
            });
        }
    }
}