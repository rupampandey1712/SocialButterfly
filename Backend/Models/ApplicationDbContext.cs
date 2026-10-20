using Microsoft.EntityFrameworkCore;

namespace SocialMediaWeb.Models
{
    public class ApplicationDbContext:DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users => Set<User>();
        public DbSet<SocialMediaPost> Posts => Set<SocialMediaPost>();
        public DbSet<Comment> Comments => Set<Comment>();
        public DbSet<Like> Likes => Set<Like>();

        public DbSet<Friend> Friends => Set<Friend>();
        public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
        public DbSet<Notification> Notifications => Set<Notification>();
        public DbSet<Follow> Follows => Set<Follow>();
        public DbSet<Story> Stories => Set<Story>();
        public DbSet<StoryView> StoryViews => Set<StoryView>();
        public DbSet<VerificationCode> VerificationCodes => Set<VerificationCode>();
        public DbSet<GeneratedImage> GeneratedImages => Set<GeneratedImage>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SocialMediaPost>()
                .HasMany(p => p.Comments)
                .WithOne(c => c.SocialMediaPost)
                .HasForeignKey(c => c.PostId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<SocialMediaPost>()
                .HasMany(p => p.Likes)
                .WithOne(l => l.Post)
                .HasForeignKey(l => l.PostId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Comment>()
                .HasOne(c => c.User)
                .WithMany()
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Like>()
                .HasOne(l => l.User)
                .WithMany()
                .HasForeignKey(l => l.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Friend>()
                     .HasOne(m=>m.Sender)
                     .WithMany(t => t.Senders)
                     .HasForeignKey(m => m.SenderId)
                     .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Friend>()
                         .HasOne(m => m.Receiver)
                     .WithMany(t => t.Receivers)
                     .HasForeignKey(m => m.ReceiverId)
                     .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<ChatMessage>(entity =>
            {
                entity.Property(message => message.Content)
                    .HasMaxLength(2000)
                    .IsRequired();

                entity.HasIndex(message => message.MessageId)
                    .IsUnique();

                entity.HasIndex(message => new { message.SenderId, message.ReceiverId, message.SentAt });

                entity.HasOne(message => message.Sender)
                    .WithMany()
                    .HasForeignKey(message => message.SenderId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(message => message.Receiver)
                    .WithMany()
                    .HasForeignKey(message => message.ReceiverId)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            modelBuilder.Entity<Notification>(entity =>
            {
                entity.HasIndex(n => new { n.EventId, n.RecipientId })
                    .IsUnique();

                entity.HasIndex(n => new { n.RecipientId, n.IsRead, n.CreatedAt });

                entity.HasOne(n => n.Recipient)
                    .WithMany()
                    .HasForeignKey(n => n.RecipientId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(n => n.Actor)
                    .WithMany()
                    .HasForeignKey(n => n.ActorId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(n => n.Post)
                    .WithMany()
                    .HasForeignKey(n => n.PostId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<User>(entity =>
            {
                entity.HasIndex(u => u.Username)
                    .IsUnique()
                    .HasFilter("[Username] IS NOT NULL");

                entity.HasIndex(u => u.PhoneNumber)
                    .IsUnique()
                    .HasFilter("[PhoneNumber] IS NOT NULL");

                entity.Property(u => u.Theme)
                    .HasDefaultValue("system");
            });

            modelBuilder.Entity<Follow>(entity =>
            {
                entity.HasIndex(f => new { f.FollowerId, f.FollowingId })
                    .IsUnique();

                entity.HasIndex(f => f.FollowingId);

                entity.HasOne(f => f.Follower)
                    .WithMany()
                    .HasForeignKey(f => f.FollowerId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(f => f.Following)
                    .WithMany()
                    .HasForeignKey(f => f.FollowingId)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            modelBuilder.Entity<Story>(entity =>
            {
                entity.HasIndex(s => new { s.UserId, s.ExpiresAt });

                entity.HasOne(s => s.User)
                    .WithMany()
                    .HasForeignKey(s => s.UserId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasMany(s => s.Views)
                    .WithOne(v => v.Story)
                    .HasForeignKey(v => v.StoryId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<StoryView>(entity =>
            {
                entity.HasIndex(v => new { v.StoryId, v.ViewerId })
                    .IsUnique();

                entity.HasOne(v => v.Viewer)
                    .WithMany()
                    .HasForeignKey(v => v.ViewerId)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            modelBuilder.Entity<VerificationCode>(entity =>
            {
                entity.HasIndex(c => new { c.Target, c.Purpose, c.CreatedAt });

                entity.HasOne(c => c.User)
                    .WithMany()
                    .HasForeignKey(c => c.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }

    }
}

