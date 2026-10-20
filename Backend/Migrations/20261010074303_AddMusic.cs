using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SocialMediaWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddMusic : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MusicArtist",
                table: "Stories",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MusicPath",
                table: "Stories",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MusicTitle",
                table: "Stories",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MusicArtist",
                table: "Posts",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MusicPath",
                table: "Posts",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MusicTitle",
                table: "Posts",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MusicArtist",
                table: "Stories");

            migrationBuilder.DropColumn(
                name: "MusicPath",
                table: "Stories");

            migrationBuilder.DropColumn(
                name: "MusicTitle",
                table: "Stories");

            migrationBuilder.DropColumn(
                name: "MusicArtist",
                table: "Posts");

            migrationBuilder.DropColumn(
                name: "MusicPath",
                table: "Posts");

            migrationBuilder.DropColumn(
                name: "MusicTitle",
                table: "Posts");
        }
    }
}
