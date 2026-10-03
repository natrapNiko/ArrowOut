using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArrowOut.Data.Migrations
{
    /// <inheritdoc />
    public partial class ChallengeGameMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Challenges_Kind",
                table: "Challenges");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Challenges_Kind",
                table: "Challenges",
                sql: "[Kind] BETWEEN 0 AND 3");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Challenges_Kind",
                table: "Challenges");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Challenges_Kind",
                table: "Challenges",
                sql: "[Kind] BETWEEN 0 AND 2");
        }
    }
}
