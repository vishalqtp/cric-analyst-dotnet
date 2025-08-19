using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FileUploadApi.Migrations
{
    /// <inheritdoc />
    public partial class newfeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "City",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "Season",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "Team1",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "Team2",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "Venue",
                table: "Matches");

            migrationBuilder.RenameColumn(
                name: "Date",
                table: "Matches",
                newName: "Year");

            migrationBuilder.AddColumn<string>(
                name: "JsonData",
                table: "Matches",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "MatchId",
                table: "Matches",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TournamentName",
                table: "Matches",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "JsonData",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "MatchId",
                table: "Matches");

            migrationBuilder.DropColumn(
                name: "TournamentName",
                table: "Matches");

            migrationBuilder.RenameColumn(
                name: "Year",
                table: "Matches",
                newName: "Date");

            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "Matches",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Season",
                table: "Matches",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Team1",
                table: "Matches",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Team2",
                table: "Matches",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Venue",
                table: "Matches",
                type: "TEXT",
                nullable: true);
        }
    }
}
