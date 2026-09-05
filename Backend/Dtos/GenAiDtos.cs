namespace SocialMediaWeb.Dtos
{
    public class GenAiGenerateTextRequestDto
    {
        public string Prompt { get; set; } = string.Empty;
    }

    public class GenAiGenerateTextResponseDto
    {
        public string Text { get; set; } = string.Empty;
    }
}
