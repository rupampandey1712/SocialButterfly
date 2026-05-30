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
        }

    }
}

