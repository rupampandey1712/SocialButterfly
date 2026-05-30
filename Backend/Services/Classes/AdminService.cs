using Microsoft.EntityFrameworkCore;
using SocialMediaWeb.Dtos;
using SocialMediaWeb.Models;
using SocialMediaWeb.Services.Interfaces;

namespace SocialMediaWeb.Services.Classes
{
    public class AdminService : IAdminService
    {
        private readonly ApplicationDbContext _context;


        public AdminService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<PostScheduleDto>> GetPostByDate()
        {
            var posts = await _context.Posts
       .Where(p => p.CreatedAt.HasValue)
       .Select(p => new PostScheduleDto
       {
           CreatedAt = p.CreatedAt!.Value.Date.ToString("yyyy-MM-dd"),
           Name = (p.User != null && p.User.Name != null) ? p.User.Name.ToUpper() : string.Empty,
           Image = p.User != null ? p.User.ImagePath : null
       })
       .ToListAsync();

            return posts;
        }

        public async Task<IEnumerable<UserScheduleDto>> GetUserDetail()
        {
            var users = await _context.Users.Where(u=>u.Role!="Admin").ToListAsync();

            return users.Select(u => new UserScheduleDto
            {
                Id = u.Id,
                Name = u.Name,
                Email = u.Email,
                IsDeleted = u.IsDeleted,
                Imagepath = u.ImagePath,
                Role = u.Role

            });
        }

        public async Task UpdateIsUserDelete(int id, UpdateUserFlag flag)
        {
            var user = await _context.Users.FindAsync(id);

            if(user == null) {

                throw new NullReferenceException();

            }

            user.IsDeleted = flag.IsDeleted;

            await _context.SaveChangesAsync();

        }
    }
}
