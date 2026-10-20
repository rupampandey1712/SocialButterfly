using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SocialMediaWeb.Dtos;
using SocialMediaWeb.Models;
using SocialMediaWeb.Services.Interfaces;

namespace SocialMediaWeb.Services.Classes
{
    public class FriendService:IFriendService
    {
        private readonly ApplicationDbContext _context;
        private readonly IBlobStorageService? _blobStorage;
        private readonly INotificationPublisher? _notifications;

        public FriendService(
            ApplicationDbContext context,
            IBlobStorageService? blobStorage = null,
            INotificationPublisher? notifications = null)
        {
            _context = context;
            _blobStorage = blobStorage;
            _notifications = notifications;
        }

        public async Task<ActionResult<FriendDto>> SendFriendRequest(FriendDto friendDto)
        {
            if (friendDto.SenderId == friendDto.ReceiverId)
            {
                throw new ArgumentException("Sender and receiver must be different users.");
            }

            var usersExist = await _context.Users
                .CountAsync(u => (u.Id == friendDto.SenderId || u.Id == friendDto.ReceiverId) && !u.IsDeleted);
            if (usersExist != 2)
            {
                throw new ArgumentException("Both users must exist and be active.");
            }

            var requestExists = await _context.Friends.AnyAsync(f =>
                (f.SenderId == friendDto.SenderId && f.ReceiverId == friendDto.ReceiverId) ||
                (f.SenderId == friendDto.ReceiverId && f.ReceiverId == friendDto.SenderId));
            if (requestExists)
            {
                throw new InvalidOperationException("A friend relationship or request already exists between these users.");
            }

            var friend = new Friend
            {
                SenderId = friendDto.SenderId,
                ReceiverId = friendDto.ReceiverId,
                IsAccepted = false,
                AcceptedAt = DateTime.UtcNow,
            };

            _context.Friends.Add(friend);
            await _context.SaveChangesAsync();

            if (_notifications != null)
            {
                await _notifications.PublishAsync(NotificationTypes.FriendRequestSent, friendDto.SenderId, friendDto.ReceiverId);
            }

            var friendDtoResponse = new FriendDto
            {
                Id = friend.Id,
                SenderId = (int)friend.SenderId,
                ReceiverId = (int)friend.ReceiverId,
                IsAccepted = friend.IsAccepted,
                AcceptedAt = friend.IsAccepted ? friend.AcceptedAt : DateTime.MinValue
            };

            return friendDtoResponse;
        }

        public async Task<ActionResult<FriendDto>> AcceptFriendRequest(int senderId, int? receiverId = null)
        {
            var query = _context.Friends.Where(u => u.SenderId == senderId && !u.IsAccepted);
            if (receiverId.HasValue && receiverId.Value > 0)
            {
                query = query.Where(u => u.ReceiverId == receiverId.Value);
            }

            var friend = await query.FirstOrDefaultAsync();

            if (friend == null)
            {
                throw new Exception("Friend request not found.");
            }

            friend.IsAccepted = true;
            friend.AcceptedAt = DateTime.UtcNow;

            _context.Entry(friend).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            if (_notifications != null)
            {
                await _notifications.PublishAsync(NotificationTypes.FriendRequestAccepted, friend.ReceiverId!.Value, friend.SenderId);
            }

            var response = new FriendDto
            {
                Id = friend.Id,
                SenderId = (int)friend.SenderId!,
                ReceiverId = (int)friend.ReceiverId!,
                IsAccepted = friend.IsAccepted,
                AcceptedAt = friend.AcceptedAt
            };

            return response;
        }

        public async Task<bool> DeleteOrDeclineFriendRequest(int senderId, int receiverId)
        {
            var friend = await _context.Friends.FirstOrDefaultAsync(f =>
                (f.SenderId == senderId && f.ReceiverId == receiverId) ||
                (f.SenderId == receiverId && f.ReceiverId == senderId));

            if (friend == null)
            {
                return false;
            }

            _context.Friends.Remove(friend);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<string> GetFriendshipStatus(int currentUserId, int otherUserId)
        {
            if (currentUserId <= 0 || otherUserId <= 0 || currentUserId == otherUserId)
            {
                return "None";
            }

            var friendship = await _context.Friends
                .AsNoTracking()
                .FirstOrDefaultAsync(f =>
                    (f.SenderId == currentUserId && f.ReceiverId == otherUserId) ||
                    (f.SenderId == otherUserId && f.ReceiverId == currentUserId));

            if (friendship == null)
            {
                return "None";
            }

            if (friendship.IsAccepted)
            {
                return "Friends";
            }

            if (friendship.SenderId == currentUserId)
            {
                return "RequestSent";
            }

            return "RequestReceived";
        }

        public async Task<ActionResult<IEnumerable<User>>> GetFriends(int userId)
        {
            var user = await _context.Users
                .Include(u => u.Senders).ThenInclude(f => f.Receiver)
                .Include(u => u.Receivers).ThenInclude(f => f.Sender)
                .FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted);

            if (user == null)
            {
                throw new Exception("User not found");
            }

            var friends = user.Senders
                .Where(f => f.IsAccepted && f.Receiver != null && !f.Receiver.IsDeleted)
                .Select(f => f.Receiver!)
                .Union(user.Receivers
                    .Where(f => f.IsAccepted && f.Sender != null && !f.Sender.IsDeleted)
                    .Select(f => f.Sender!))
                .Distinct()
                .ToList();

            return friends;
        }

        public async Task<ActionResult<int>> GetRequest(int receiverId)
        {
            var count = await _context.Friends
                .CountAsync(f => f.ReceiverId == receiverId && !f.IsAccepted);

            return count;
        }

        public async Task<ActionResult<int>> FriendsCount(int userId)
        {
            var count = await _context.Friends
                .CountAsync(f => f.IsAccepted && (f.ReceiverId == userId || f.SenderId == userId));

            return count;
        }

        public async Task<ActionResult<List<RequestDto>>> GetUserRequest(int receiverId)
        {
            var requests = await (from u in _context.Users
                                 join f in _context.Friends on u.Id equals f.SenderId
                                 where f.ReceiverId == receiverId && f.IsAccepted == false && !u.IsDeleted
                                 select new RequestDto
                                 {
                                     ImagePath = u.ImagePath,
                                     Name = u.Name,
                                     Dob = u.Dob,
                                     Id = u.Id
                                 }).ToListAsync();

            if (_blobStorage != null)
            {
                foreach (var req in requests)
                {
                    req.ImagePath = _blobStorage.ToPublicUrl(req.ImagePath);
                }
            }

            return requests;
        }

        public async Task<ActionResult<List<RequestDto>>> GetMyFriends(int userId)
        {
            var sentFriends = await (from f in _context.Friends
                                     join u in _context.Users on f.ReceiverId equals u.Id
                                     where f.SenderId == userId && f.IsAccepted && !u.IsDeleted
                                     select new RequestDto
                                     {
                                         ImagePath = u.ImagePath,
                                         Name = u.Name,
                                         Dob = u.Dob,
                                         Id = u.Id
                                     }).ToListAsync();

            var receivedFriends = await (from f in _context.Friends
                                         join u in _context.Users on f.SenderId equals u.Id
                                         where f.ReceiverId == userId && f.IsAccepted && !u.IsDeleted
                                         select new RequestDto
                                         {
                                             ImagePath = u.ImagePath,
                                             Name = u.Name,
                                             Dob = u.Dob,
                                             Id = u.Id
                                         }).ToListAsync();

            var allFriends = sentFriends.Concat(receivedFriends)
                .GroupBy(f => f.Id)
                .Select(g => g.First())
                .ToList();

            if (_blobStorage != null)
            {
                foreach (var friend in allFriends)
                {
                    friend.ImagePath = _blobStorage.ToPublicUrl(friend.ImagePath);
                }
            }

            return allFriends;
        }
    }
}
