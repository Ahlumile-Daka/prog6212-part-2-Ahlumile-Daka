using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Models;

namespace RaceDay.Api.Data
{
    public class RaceDayDbContext : DbContext
    {
        public RaceDayDbContext(DbContextOptions<RaceDayDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<EventType> EventTypes { get; set; }
        public DbSet<Event> Events { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Enrolment> Enrolments { get; set; }
        public DbSet<Result> Results { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>(entity =>
            {
                entity.HasIndex(e => e.Email).IsUnique();
                entity.Property(e => e.Role).HasMaxLength(20);
                entity.ToTable(t => t.HasCheckConstraint("CK_Users_Role", "[Role] IN ('Organiser', 'Participant')"));
            });

            modelBuilder.Entity<EventType>(entity =>
            {
                entity.HasIndex(e => e.Name).IsUnique();
            });

            modelBuilder.Entity<Event>(entity =>
            {
                entity.HasOne(e => e.EventType)
                    .WithMany(et => et.Events)
                    .HasForeignKey(e => e.EventTypeID)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Organiser)
                    .WithMany(u => u.OrganisedEvents)
                    .HasForeignKey(e => e.OrganiserID)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Category>(entity =>
            {
                entity.HasOne(c => c.Event)
                    .WithMany(e => e.Categories)
                    .HasForeignKey(c => c.EventID)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Enrolment>(entity =>
            {
                entity.HasOne(en => en.Participant)
                    .WithMany(u => u.Enrolments)
                    .HasForeignKey(en => en.ParticipantID)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(en => en.Event)
                    .WithMany(e => e.Enrolments)
                    .HasForeignKey(en => en.EventID)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(en => en.Category)
                    .WithMany(c => c.Enrolments)
                    .HasForeignKey(en => en.CategoryID)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(en => new { en.ParticipantID, en.EventID }).IsUnique();
            });

            modelBuilder.Entity<Result>(entity =>
            {
                entity.HasOne(r => r.Enrolment)
                    .WithOne(en => en.Result)
                    .HasForeignKey<Result>(r => r.EnrolmentID)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(r => r.CapturedByUser)
                    .WithMany(u => u.CapturedResults)
                    .HasForeignKey(r => r.CapturedBy)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(r => r.EnrolmentID).IsUnique();
            });

            // Seed EventTypes
            modelBuilder.Entity<EventType>().HasData(
                new EventType { EventTypeID = 1, Name = "Running", Description = "Road running events" },
                new EventType { EventTypeID = 2, Name = "Walking", Description = "Race walking / fun walks" },
                new EventType { EventTypeID = 3, Name = "Cycling", Description = "Road cycling events" }
            );
        }
    }
}
