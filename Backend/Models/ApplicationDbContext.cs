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
        }

    }
}

