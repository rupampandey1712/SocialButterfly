using BCrypt.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SocialMediaWeb.Dtos;
using SocialMediaWeb.Models;
using SocialMediaWeb.Security;
using SocialMediaWeb.Services.Interfaces;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace SocialMediaWeb.Services.Classes
{
    public class UserService : IUserService
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _config;
        private readonly IBlobStorageService _blobStorage;
        private readonly IHttpContextAccessor _httpContextAccessor;

        private const string ProfileImagesContainer = "profile-images";

        public UserService(
            ApplicationDbContext context,
            IConfiguration config,
            IBlobStorageService blobStorage,
            IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _config = config;
            _blobStorage = blobStorage;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<UserResponseDto> LoginAsync(UserLoginDto loginDto)
        {
            var identifier = string.IsNullOrWhiteSpace(loginDto.Identifier) ? loginDto.Email : loginDto.Identifier;
            if (string.IsNullOrWhiteSpace(identifier) || string.IsNullOrWhiteSpace(loginDto.Password))
            {
                throw new ArgumentException("Enter your phone number, username or email, and your password.");
            }

            var user = await FindByIdentifierAsync(identifier);
            if (user == null ||
                string.IsNullOrWhiteSpace(user.PasswordHash) ||
                !BCrypt.Net.BCrypt.Verify(loginDto.Password, user.PasswordHash))
            {
                throw new Exception("Sorry, your password was incorrect. Please double-check your password.");
            }

            return ToLoginResponse(user);
        }

        public UserResponseDto ToLoginResponse(User user)
        {
            return new UserResponseDto
            {
                Id = user.Id,
                Email = user.Email,
                Role = user.Role,
                Name = user.Name,
                Username = user.Username,
                Theme = user.Theme,
                ImagePath = _blobStorage.ToPublicUrl(user.ImagePath),
                Token = GenerateJwt(user)
            };
        }

        /// <summary>Resolves an email, E.164 phone number or username to an active user.</summary>
        public async Task<User?> FindByIdentifierAsync(string identifier)
        {
            var value = identifier.Trim();
            if (IdentityRules.LooksLikeEmail(value))
            {
                var email = value.ToLowerInvariant();
                return await _context.Users.FirstOrDefaultAsync(u => u.Email != null && u.Email.ToLower() == email && !u.IsDeleted);
            }

            if (IdentityRules.LooksLikePhone(value) && IdentityRules.NormalizePhone(value) is string phone)
            {
                return await _context.Users.FirstOrDefaultAsync(u => u.PhoneNumber == phone && !u.IsDeleted);
            }

            var username = value.TrimStart('@').ToLowerInvariant();
            return await _context.Users.FirstOrDefaultAsync(u => u.Username == username && !u.IsDeleted);
        }

        public async Task RegisterAsync(UserRegistrationDto model)
        {
            await RegisterWithResultAsync(model);
        }

        public async Task<RegisterResponseDto> RegisterWithResultAsync(UserRegistrationDto model)
        {
            var email = IdentityRules.NormalizeEmail(model.Email);
            var phone = IdentityRules.NormalizePhone(model.PhoneNumber);

            if (!string.IsNullOrWhiteSpace(model.Email) && email == null)
            {
                throw new ArgumentException("Enter a valid email address.");
            }

            if (!string.IsNullOrWhiteSpace(model.PhoneNumber) && phone == null)
            {
                throw new ArgumentException("Enter a valid mobile number with country code, e.g. +91 98765 43210.");
            }

            if (email == null && phone == null)
            {
                throw new ArgumentException("A mobile number or email address is required.");
            }

            if (string.IsNullOrWhiteSpace(model.Password) || model.Password.Length < IdentityRules.MinimumPasswordLength)
            {
                throw new ArgumentException($"Create a password at least {IdentityRules.MinimumPasswordLength} characters long.");
            }

            if (email != null && await _context.Users.AnyAsync(u => u.Email != null && u.Email.ToLower() == email))
            {
                throw new InvalidOperationException("Another account is using the same email.");
            }

            if (phone != null && await _context.Users.AnyAsync(u => u.PhoneNumber == phone))
            {
                throw new InvalidOperationException("Another account is using the same mobile number.");
            }

            string username;
            if (!string.IsNullOrWhiteSpace(model.Username))
            {
                username = IdentityRules.NormalizeUsername(model.Username)
                    ?? throw new ArgumentException("Usernames can only use letters, numbers, underscores and periods (3-30 characters).");
                if (await _context.Users.AnyAsync(u => u.Username == username))
                {
                    throw new InvalidOperationException("This username isn't available. Please try another.");
                }
            }
            else
            {
                username = await SuggestUsernameAsync(IdentityRules.UsernameSeed(model.Name, email));
            }

            if (model.ImageFile is { Length: > 0 } && !MediaRules.IsImage(model.ImageFile))
            {
                throw new ArgumentException("Profile photos must be images (JPG, PNG, GIF, WebP or AVIF).");
            }

            var imageUrl = model.ImageFile is { Length: > 0 }
                ? await _blobStorage.UploadAsync(model.ImageFile, ProfileImagesContainer)
                : null;

            var user = new User
            {
                Name = string.IsNullOrWhiteSpace(model.Name) ? username : model.Name.Trim(),
                Username = username,
                Dob = model.Dob,
                Email = email,
                PhoneNumber = phone,
                Information = model.Information,
                IsDeleted = model.IsDeleted,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password, BCrypt.Net.BCrypt.GenerateSalt()),
                ImagePath = imageUrl,
                // Self-registration can never grant a role; admins come from AdminSeeder.
                Role = "User"
            };

            try
            {
                _context.Users.Add(user);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException e)
            {
                throw new InvalidOperationException("Could not register user.", e);
            }

            return new RegisterResponseDto
            {
                Id = user.Id,
                Username = user.Username,
                VerificationTarget = email ?? phone
            };
        }

        public async Task<AvailabilityDto> CheckAvailabilityAsync(string? username, string? email, string? phone, string? name)
        {
            var result = new AvailabilityDto();

            if (username != null)
            {
                var normalized = IdentityRules.NormalizeUsername(username);
                result.UsernameAvailable = normalized != null && !await _context.Users.AnyAsync(u => u.Username == normalized);
                if (result.UsernameAvailable != true)
                {
                    result.SuggestedUsername = await SuggestUsernameAsync(IdentityRules.UsernameSeed(normalized ?? username, null));
                }
            }
            else if (name != null)
            {
                result.SuggestedUsername = await SuggestUsernameAsync(IdentityRules.UsernameSeed(name, email));
            }

            if (email != null)
            {
                var normalized = IdentityRules.NormalizeEmail(email);
                result.EmailAvailable = normalized != null && !await _context.Users.AnyAsync(u => u.Email != null && u.Email.ToLower() == normalized);
            }

            if (phone != null)
            {
                var normalized = IdentityRules.NormalizePhone(phone);
                result.PhoneAvailable = normalized != null && !await _context.Users.AnyAsync(u => u.PhoneNumber == normalized);
            }

            return result;
        }

        private async Task<string> SuggestUsernameAsync(string seed)
        {
            if (!await _context.Users.AnyAsync(u => u.Username == seed))
            {
                return seed;
            }

            for (var attempt = 0; attempt < 20; attempt++)
            {
                var candidate = $"{seed}{Random.Shared.Next(10, attempt < 10 ? 999 : 99999)}";
                if (!await _context.Users.AnyAsync(u => u.Username == candidate))
                {
                    return candidate;
                }
            }

            return $"{seed}{Guid.NewGuid().ToString("N")[..6]}";
        }

        public async Task<UserSettingsDto> GetSettingsAsync(int userId)
        {
            var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted)
                ?? throw new KeyNotFoundException("User cannot be found");
            return ToSettings(user);
        }

        public async Task<UserSettingsDto> UpdateSettingsAsync(int userId, UpdateSettingsDto dto)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted)
                ?? throw new KeyNotFoundException("User cannot be found");

            if (dto.Name != null)
            {
                user.Name = string.IsNullOrWhiteSpace(dto.Name) ? user.Name : dto.Name.Trim();
            }

            if (dto.Information != null)
            {
                user.Information = dto.Information.Length > 150 ? dto.Information[..150] : dto.Information;
            }

            if (dto.Username != null)
            {
                var username = IdentityRules.NormalizeUsername(dto.Username)
                    ?? throw new ArgumentException("Usernames can only use letters, numbers, underscores and periods (3-30 characters).");
                if (username != user.Username && await _context.Users.AnyAsync(u => u.Username == username))
                {
                    throw new InvalidOperationException("This username isn't available. Please try another.");
                }
                user.Username = username;
            }

            if (dto.Email != null)
            {
                var email = string.IsNullOrWhiteSpace(dto.Email) ? null : IdentityRules.NormalizeEmail(dto.Email)
                    ?? throw new ArgumentException("Enter a valid email address.");
                if (email != user.Email)
                {
                    if (email != null && await _context.Users.AnyAsync(u => u.Id != userId && u.Email != null && u.Email.ToLower() == email))
                    {
                        throw new InvalidOperationException("Another account is using the same email.");
                    }
                    user.Email = email;
                    user.EmailConfirmed = false;
                }
            }

            if (dto.PhoneNumber != null)
            {
                var phone = string.IsNullOrWhiteSpace(dto.PhoneNumber) ? null : IdentityRules.NormalizePhone(dto.PhoneNumber)
                    ?? throw new ArgumentException("Enter a valid mobile number with country code, e.g. +91 98765 43210.");
                if (phone != user.PhoneNumber)
                {
                    if (phone != null && await _context.Users.AnyAsync(u => u.Id != userId && u.PhoneNumber == phone))
                    {
                        throw new InvalidOperationException("Another account is using the same mobile number.");
                    }
                    user.PhoneNumber = phone;
                    user.PhoneConfirmed = false;
                }
            }

            if (user.Email == null && user.PhoneNumber == null)
            {
                throw new ArgumentException("Keep at least an email address or a mobile number on your account.");
            }

            if (dto.Theme != null)
            {
                user.Theme = dto.Theme is "light" or "dark" or "system"
                    ? dto.Theme
                    : throw new ArgumentException("Theme must be light, dark or system.");
            }

            if (dto.NotifyLikes is bool likes) user.MuteLikeNotifications = !likes;
            if (dto.NotifyComments is bool comments) user.MuteCommentNotifications = !comments;
            if (dto.NotifyFollows is bool follows) user.MuteFollowNotifications = !follows;
            if (dto.NotifyNewPosts is bool posts) user.MuteNewPostNotifications = !posts;

            if (dto.ImageFile is { Length: > 0 })
            {
                // Both the MIME type and the extension must be an allowed image type:
                // the stored content type comes from the extension (e.g. .html would be served as HTML).
                if (!MediaRules.IsImage(dto.ImageFile))
                {
                    throw new ArgumentException("Profile photos must be images (JPG, PNG, GIF, WebP or AVIF).");
                }

                var previous = user.ImagePath;
                user.ImagePath = await _blobStorage.UploadAsync(dto.ImageFile, ProfileImagesContainer);
                if (!string.IsNullOrEmpty(previous) && Uri.TryCreate(previous, UriKind.Absolute, out _))
                {
                    await _blobStorage.DeleteAsync(previous, ProfileImagesContainer);
                }
            }

            await _context.SaveChangesAsync();
            return ToSettings(user);
        }

        public async Task ChangePasswordAsync(int userId, ChangePasswordDto dto)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted)
                ?? throw new KeyNotFoundException("User cannot be found");

            if (string.IsNullOrWhiteSpace(dto.CurrentPassword) ||
                string.IsNullOrWhiteSpace(user.PasswordHash) ||
                !BCrypt.Net.BCrypt.Verify(dto.CurrentPassword, user.PasswordHash))
            {
                throw new UnauthorizedAccessException("Your current password is incorrect.");
            }

            SetPassword(user, dto.NewPassword);
            await _context.SaveChangesAsync();
        }

        public static void SetPassword(User user, string? newPassword)
        {
            if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < IdentityRules.MinimumPasswordLength)
            {
                throw new ArgumentException($"Create a password at least {IdentityRules.MinimumPasswordLength} characters long.");
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword, BCrypt.Net.BCrypt.GenerateSalt());
        }

        private UserSettingsDto ToSettings(User user) => new()
        {
            Id = user.Id,
            Name = user.Name,
            Username = user.Username,
            Email = user.Email,
            EmailConfirmed = user.EmailConfirmed,
            PhoneNumber = user.PhoneNumber,
            PhoneConfirmed = user.PhoneConfirmed,
            Information = user.Information,
            ImagePath = _blobStorage.ToPublicUrl(user.ImagePath),
            Dob = user.Dob,
            Theme = user.Theme,
            NotifyLikes = !user.MuteLikeNotifications,
            NotifyComments = !user.MuteCommentNotifications,
            NotifyFollows = !user.MuteFollowNotifications,
            NotifyNewPosts = !user.MuteNewPostNotifications
        };

        public async Task<User> UpdateUserAsync(UserUpdateDto userUpdateDto)
        {
            var user = await _context.Users.FirstOrDefaultAsync(e => e.Id == userUpdateDto.Id);
            if (user == null)
            {
                throw new Exception($"User with id {userUpdateDto.Id} not found");
            }

            user.Name = userUpdateDto.Name ?? user.Name;
            user.Dob = userUpdateDto.Dob == default ? user.Dob : userUpdateDto.Dob;
            if (!string.IsNullOrWhiteSpace(userUpdateDto.Email))
            {
                // Same rules as Settings: valid, unique, and re-verified when it changes.
                var email = IdentityRules.NormalizeEmail(userUpdateDto.Email)
                    ?? throw new ArgumentException("Enter a valid email address.");
                if (email != user.Email)
                {
                    if (await _context.Users.AnyAsync(u => u.Id != user.Id && u.Email != null && u.Email.ToLower() == email))
                    {
                        throw new InvalidOperationException("Another account is using the same email.");
                    }
                    user.Email = email;
                    user.EmailConfirmed = false;
                }
            }
            user.Information = userUpdateDto.Information ?? user.Information;

            if (userUpdateDto.ImageFile != null)
            {
                if (!MediaRules.IsImage(userUpdateDto.ImageFile))
                {
                    throw new ArgumentException("Profile photos must be images (JPG, PNG, GIF, WebP or AVIF).");
                }

                if (!string.IsNullOrEmpty(user.ImagePath))
                {
                    await _blobStorage.DeleteAsync(user.ImagePath, ProfileImagesContainer);
                }

                user.ImagePath = await _blobStorage.UploadAsync(userUpdateDto.ImageFile, ProfileImagesContainer);
            }

            await _context.SaveChangesAsync();
            return user;
        }

        public async Task<User> FindById(int id)
        {
            var user = await _context.Users.FindAsync(id);

            if (user == null || user.IsDeleted)
            {
                throw new NullReferenceException("User cannot be found");
            }

            user.ImagePath = _blobStorage.ToPublicUrl(user.ImagePath);
            return user;
        }

        public async Task<UserProfileDto> GetProfileAsync(int userId, int? viewerId)
        {
            var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted)
                ?? throw new KeyNotFoundException("User cannot be found");

            var now = DateTime.UtcNow;
            var profile = new UserProfileDto
            {
                Id = user.Id,
                Name = user.Name,
                Username = user.Username,
                FollowersCount = await _context.Follows.CountAsync(f => f.FollowingId == userId),
                FollowingCount = await _context.Follows.CountAsync(f => f.FollowerId == userId),
                HasActiveStory = await _context.Stories.AnyAsync(st => st.UserId == userId && st.ExpiresAt > now),
                ImagePath = _blobStorage.ToPublicUrl(user.ImagePath),
                Information = user.Information,
                PostsCount = await _context.Posts.CountAsync(p => p.UserId == userId),
                FriendsCount = await _context.Friends.CountAsync(f => f.IsAccepted && (f.SenderId == userId || f.ReceiverId == userId))
            };

            if (viewerId is int viewer && viewer != userId)
            {
                var friendship = await _context.Friends.AsNoTracking().FirstOrDefaultAsync(f =>
                    (f.SenderId == viewer && f.ReceiverId == userId) ||
                    (f.SenderId == userId && f.ReceiverId == viewer));

                profile.FriendshipStatus = friendship == null ? "None"
                    : friendship.IsAccepted ? "Friends"
                    : friendship.SenderId == viewer ? "RequestSent"
                    : "RequestReceived";
                profile.IsFollowing = await _context.Follows.AnyAsync(f => f.FollowerId == viewer && f.FollowingId == userId);
                profile.FollowsYou = await _context.Follows.AnyAsync(f => f.FollowerId == userId && f.FollowingId == viewer);
            }
            else if (viewerId == userId)
            {
                profile.FriendshipStatus = "Self";
            }

            return profile;
        }

        public async Task<List<ResponseDto>> GetUserAsync()
        {
            var users = await _context.Users
                .Select(u => new ResponseDto
                {
                    Id = u.Id,
                    Name = u.Name,
                    Dob = u.Dob,
                    Email = u.Email,
                    Role = u.Role,
                    ImagePath = u.ImagePath
                })
                .ToListAsync();

            foreach (var u in users)
            {
                u.ImagePath = _blobStorage.ToPublicUrl(u.ImagePath);
            }

            return users;
        }

        public IQueryable<UserDto> Search(UserDto dto)
        {
            var query = _context.Users
                .Where(u => !u.IsDeleted && u.Role != "Admin")
                .AsQueryable();

            // Detect if this is a general search where the same term was passed to multiple fields
            var distinctTerms = new[] { dto.Name, dto.Email, dto.Role, dto.Information }
                .Where(term => !string.IsNullOrWhiteSpace(term))
                .Select(term => term!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (distinctTerms.Count == 1)
            {
                var pattern = SearchText.Contains(distinctTerms[0]);
                var handlePattern = SearchText.Contains(distinctTerms[0].TrimStart('@'));
                query = query.Where(u =>
                    (u.Name != null && EF.Functions.Like(u.Name, pattern)) ||
                    (u.Username != null && EF.Functions.Like(u.Username, handlePattern)) ||
                    (u.Email != null && EF.Functions.Like(u.Email, pattern)) ||
                    (u.Information != null && EF.Functions.Like(u.Information, pattern)));
            }
            else
            {
                if (!string.IsNullOrEmpty(dto.Name))
                {
                    query = query.Where(u => u.Name != null && EF.Functions.Like(u.Name, SearchText.Contains(dto.Name)));
                }

                if (!string.IsNullOrEmpty(dto.Email))
                {
                    query = query.Where(u => u.Email != null && EF.Functions.Like(u.Email, SearchText.Contains(dto.Email)));
                }

                if (!string.IsNullOrEmpty(dto.Role))
                {
                    query = query.Where(u => u.Role != null && EF.Functions.Like(u.Role, SearchText.Contains(dto.Role)));
                }

                if (!string.IsNullOrEmpty(dto.Information))
                {
                    query = query.Where(u => u.Information != null && EF.Functions.Like(u.Information, SearchText.Contains(dto.Information)));
                }
            }

            return query.Select(u => new UserDto
            {
                Id = u.Id,
                Name = u.Name,
                Username = u.Username,
                Email = u.Email,
                Role = u.Role,
                Information = u.Information,
                ImagePath = u.ImagePath
            });
        }

        private string GenerateJwt(User user)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Name ?? user.Username ?? user.Email ?? user.PhoneNumber ?? string.Empty),
                new Claim(ClaimTypes.Role, user.Role ?? "User")
            };

            var secret = _config["JWT:Secret"]
                ?? throw new InvalidOperationException("JWT:Secret is missing.");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            _ = int.TryParse(_config["JWT:TokenValidityInMinutes"], out int tokenValidityInMinutes);
            tokenValidityInMinutes = tokenValidityInMinutes <= 0 ? 60 : tokenValidityInMinutes;

            var token = new JwtSecurityToken(
                issuer: _config["JWT:ValidIssuer"],
                audience: _config["JWT:ValidAudience"],
                expires: DateTime.UtcNow.AddMinutes(tokenValidityInMinutes),
                claims: claims,
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
