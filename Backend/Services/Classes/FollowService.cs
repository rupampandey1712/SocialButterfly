using Microsoft.EntityFrameworkCore;
using SocialMediaWeb.Dtos;
using SocialMediaWeb.Models;
using SocialMediaWeb.Security;
using SocialMediaWeb.Services.Interfaces;

namespace SocialMediaWeb.Services.Classes
{
    public class FollowService : IFollowService
    {
        private readonly ApplicationDbContext _context;
        private readonly IBlobStorageService? _blobStorage;
        private readonly INotificationPublisher? _notifications;

        public FollowService(
            ApplicationDbContext context,
            IBlobStorageService? blobStorage = null,
            INotificationPublisher? notifications = null)
        {
            _context = context;
            _blobStorage = blobStorage;
            _notifications = notifications;
        }

        public async Task<FollowResultDto> ToggleFollowAsync(int followerId, int targetId)
        {
            if (followerId == targetId)
            {
                throw new ArgumentException("You can't follow yourself.");
            }

            var activeUsers = await _context.Users.CountAsync(u => (u.Id == followerId || u.Id == targetId) && !u.IsDeleted);
            if (activeUsers != 2)
            {
                throw new KeyNotFoundException("User cannot be found");
            }

            var existing = await _context.Follows.FirstOrDefaultAsync(f => f.FollowerId == followerId && f.FollowingId == targetId);
            if (existing != null)
            {
                _context.Follows.Remove(existing);
            }
            else
            {
                _context.Follows.Add(new Follow { FollowerId = followerId, FollowingId = targetId });
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // A double-click raced us to the same unique (follower, following) row; the end state is the same.
                _context.ChangeTracker.Clear();
            }

            var isFollowing = existing == null;
            if (isFollowing && _notifications != null)
            {
                await _notifications.PublishAsync(NotificationTypes.UserFollowed, followerId, targetId);
            }

            return new FollowResultDto
            {
                IsFollowing = isFollowing,
                FollowersCount = await _context.Follows.CountAsync(f => f.FollowingId == targetId)
            };
        }

        public Task<List<UserListItemDto>> GetFollowersAsync(int userId, int? viewerId)
        {
            var users = _context.Follows
                .Where(f => f.FollowingId == userId)
                .OrderByDescending(f => f.CreatedAt)
                .Select(f => f.Follower!);
            return ToListItemsAsync(users, viewerId);
        }

        public Task<List<UserListItemDto>> GetFollowingAsync(int userId, int? viewerId)
        {
            var users = _context.Follows
                .Where(f => f.FollowerId == userId)
                .OrderByDescending(f => f.CreatedAt)
                .Select(f => f.Following!);
            return ToListItemsAsync(users, viewerId);
        }

        public Task<List<UserListItemDto>> GetPeopleAsync(int? viewerId, string? query, int take)
        {
            var users = _context.Users.Where(u => u.Role != "Admin");
            if (!string.IsNullOrWhiteSpace(query))
            {
                var pattern = SearchText.Contains(query.Trim().TrimStart('@'));
                users = users.Where(u =>
                    (u.Name != null && EF.Functions.Like(u.Name, pattern)) ||
                    (u.Username != null && EF.Functions.Like(u.Username, pattern)) ||
                    (u.Information != null && EF.Functions.Like(u.Information, pattern)));
            }

            // Most-followed first, then newest members.
            users = users
                .OrderByDescending(u => _context.Follows.Count(f => f.FollowingId == u.Id))
                .ThenByDescending(u => u.Id)
                .Take(Math.Clamp(take, 1, 100));
            return ToListItemsAsync(users, viewerId);
        }

        public async Task<List<UserListItemDto>> GetSuggestionsAsync(int viewerId, int take)
        {
            var users = _context.Users
                .Where(u => u.Id != viewerId && u.Role != "Admin" &&
                            !_context.Follows.Any(f => f.FollowerId == viewerId && f.FollowingId == u.Id))
                // People who follow you or are followed by people you follow come first.
                .OrderByDescending(u => _context.Follows.Count(f => f.FollowerId == u.Id && f.FollowingId == viewerId) * 3 +
                                        _context.Follows.Count(f => f.FollowingId == u.Id &&
                                            _context.Follows.Any(mine => mine.FollowerId == viewerId && mine.FollowingId == f.FollowerId)))
                .ThenByDescending(u => _context.Follows.Count(f => f.FollowingId == u.Id))
                .ThenByDescending(u => u.Id)
                .Take(Math.Clamp(take, 1, 30));
            return await ToListItemsAsync(users, viewerId);
        }

        private async Task<List<UserListItemDto>> ToListItemsAsync(IQueryable<User> users, int? viewerId)
        {
            var items = await users
                .Where(u => !u.IsDeleted)
                .Select(u => new UserListItemDto
                {
                    Id = u.Id,
                    Name = u.Name,
                    Username = u.Username,
                    ImagePath = u.ImagePath,
                    Information = u.Information,
                    IsSelf = viewerId == u.Id,
                    IsFollowing = viewerId != null && _context.Follows.Any(f => f.FollowerId == viewerId && f.FollowingId == u.Id),
                    FollowsYou = viewerId != null && _context.Follows.Any(f => f.FollowerId == u.Id && f.FollowingId == viewerId),
                    FollowersCount = _context.Follows.Count(f => f.FollowingId == u.Id)
                })
                .AsNoTracking()
                .ToListAsync();

            if (_blobStorage != null)
            {
                foreach (var item in items)
                {
                    item.ImagePath = _blobStorage.ToPublicUrl(item.ImagePath);
                }
            }

            return items;
        }
    }
}
