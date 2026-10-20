using SocialMediaWeb.Dtos;
using SocialMediaWeb.Models;

namespace SocialMediaWeb.Services.Interfaces
{
    public interface ICommentService
    {
        Task<Comment> CreateCommentAsync(CreateCommentDto commentCreateDto);
        Task<List<Comment>> GetCommentAsync(int postId);
        Task<List<Comment>> GetCommentsAsync();
        Task<List<CommentResponseDto>> GetPostCommentsAsync(int postId);
        Task<CommentResponseDto> AddCommentAsync(CreateCommentDto commentCreateDto);
    }
}
