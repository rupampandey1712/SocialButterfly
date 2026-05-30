using BCrypt.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SocialMediaWeb.Dtos;
using SocialMediaWeb.Models;
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
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public UserService(
            ApplicationDbContext context,
            IConfiguration config,
            IWebHostEnvironment webHostEnvironment,
            IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _config = config;
            _webHostEnvironment = webHostEnvironment;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<UserResponseDto> LoginAsync(UserLoginDto loginDto)
        {
            if (string.IsNullOrWhiteSpace(loginDto.Email) || string.IsNullOrWhiteSpace(loginDto.Password))
            {
                throw new ArgumentException("Email and password are required.");
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == loginDto.Email && !u.IsDeleted);
            if (user == null ||
                string.IsNullOrWhiteSpace(user.PasswordHash) ||
                !BCrypt.Net.BCrypt.Verify(loginDto.Password, user.PasswordHash))
            {
                throw new Exception("Invalid email or password");
            }

            return new UserResponseDto
            {
                Id = user.Id,
                Email = user.Email,
                Role = user.Role,
                ImagePath = user.ImagePath,
                Token = GenerateJwt(user)
            };
        }

        public async Task RegisterAsync(UserRegistrationDto model)
        {
            if (string.IsNullOrWhiteSpace(model.Email) || string.IsNullOrWhiteSpace(model.Password))
            {
                throw new ArgumentException("Email and password are required.");
            }

            if (await _context.Users.AnyAsync(u => u.Email == model.Email))
            {
                throw new InvalidOperationException("A user with this email already exists.");
            }

            var imageUrl = model.ImageFile is { Length: > 0 }
                ? await SaveImageAsync(model.ImageFile, "Images")
                : null;

            var user = new User
            {
                Name = model.Name,
                Dob = model.Dob,
                Email = model.Email,
                Information = model.Information,
                IsDeleted = model.IsDeleted,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password, BCrypt.Net.BCrypt.GenerateSalt()),
                ImagePath = imageUrl,
                Role = string.IsNullOrWhiteSpace(model.Role) ? "User" : model.Role
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
        }

        public async Task<User> UpdateUserAsync(UserUpdateDto userUpdateDto)
        {
            var user = await _context.Users.FirstOrDefaultAsync(e => e.Id == userUpdateDto.Id);
            if (user == null)
            {
                throw new Exception($"User with id {userUpdateDto.Id} not found");
            }

            user.Name = userUpdateDto.Name ?? user.Name;
            user.Dob = userUpdateDto.Dob == default ? user.Dob : userUpdateDto.Dob;
            user.Email = userUpdateDto.Email ?? user.Email;
            user.Information = userUpdateDto.Information ?? user.Information;

            if (userUpdateDto.ImageFile != null)
            {
                if (!string.IsNullOrEmpty(user.ImagePath))
                {
                    DeleteImage(user.ImagePath);
                }

                user.ImagePath = await SaveImageAsync(userUpdateDto.ImageFile, "Images");
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

            return user;
        }

        public async Task<List<ResponseDto>> GetUserAsync()
        {
            return await _context.Users
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
        }

        public IQueryable<UserDto> Search(UserDto dto)
        {
            var query = _context.Users.Where(u => u.Role != "Admin").AsQueryable();

            if (!string.IsNullOrEmpty(dto.Name))
            {
                query = query.Where(u => EF.Functions.Like(u.Name!, $"%{dto.Name}%"));
            }

            if (!string.IsNullOrEmpty(dto.Email))
            {
                query = query.Where(u => EF.Functions.Like(u.Email!, $"%{dto.Email}%"));
            }

            if (!string.IsNullOrEmpty(dto.Role))
            {
                query = query.Where(u => EF.Functions.Like(u.Role!, $"%{dto.Role}%"));
            }

            if (!string.IsNullOrEmpty(dto.Information))
            {
                query = query.Where(u => EF.Functions.Like(u.Information!, $"%{dto.Information}%"));
            }

            return query.Select(u => new UserDto
            {
                Name = u.Name,
                Email = u.Email,
                Role = u.Role,
                Information = u.Information,
                ImagePath = u.ImagePath
            });
        }

        private async Task<string> SaveImageAsync(IFormFile imageFile, string folderName)
        {
            var webRootPath = _webHostEnvironment.WebRootPath
                ?? Path.Combine(_webHostEnvironment.ContentRootPath, "wwwroot");
            var uploadPath = Path.Combine(webRootPath, folderName);
            Directory.CreateDirectory(uploadPath);

            var fileName = Path.GetFileNameWithoutExtension(imageFile.FileName);
            var fileExtension = Path.GetExtension(imageFile.FileName);
            var uniqueFileName = $"{fileName}_{DateTime.UtcNow.Ticks}{fileExtension}";
            var filePath = Path.Combine(uploadPath, uniqueFileName);

            await using var fileStream = new FileStream(filePath, FileMode.Create);
            await imageFile.CopyToAsync(fileStream);

            return $"{GetBaseUrl()}/{folderName}/{uniqueFileName}";
        }

        private void DeleteImage(string imagePath)
        {
            var fileName = Uri.TryCreate(imagePath, UriKind.Absolute, out var uri)
                ? Path.GetFileName(uri.LocalPath)
                : Path.GetFileName(imagePath);
            var webRootPath = _webHostEnvironment.WebRootPath
                ?? Path.Combine(_webHostEnvironment.ContentRootPath, "wwwroot");
            var filePath = Path.Combine(webRootPath, "Images", fileName);

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }

        private string GenerateJwt(User user)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Email ?? string.Empty),
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

        private string GetBaseUrl()
        {
            var request = _httpContextAccessor.HttpContext?.Request
                ?? throw new InvalidOperationException("The current HTTP request is not available.");

            return $"{request.Scheme}://{request.Host}";
        }
    }
}
