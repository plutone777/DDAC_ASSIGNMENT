using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DDAC.Migrations
{
    /// <inheritdoc />
    public partial class LinkAdvisorAndEmployerProfilesToUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_CareerAdvisorProfiles",
                table: "CareerAdvisorProfiles");

            migrationBuilder.DropColumn(
                name: "AdvisorID",
                table: "CareerAdvisorProfiles");

            migrationBuilder.AddColumn<int>(
                name: "AdvisorID",
                table: "CareerAdvisorProfiles",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddPrimaryKey(
                name: "PK_CareerAdvisorProfiles",
                table: "CareerAdvisorProfiles",
                column: "AdvisorID");

            migrationBuilder.DropPrimaryKey(
                name: "PK_EmployerProfiles",
                table: "EmployerProfiles");

            migrationBuilder.DropColumn(
                name: "EmployerID",
                table: "EmployerProfiles");

            migrationBuilder.AddColumn<int>(
                name: "EmployerID",
                table: "EmployerProfiles",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddPrimaryKey(
                name: "PK_EmployerProfiles",
                table: "EmployerProfiles",
                column: "EmployerID");

            migrationBuilder.AddForeignKey(
                name: "FK_CareerAdvisorProfiles_Users_AdvisorID",
                table: "CareerAdvisorProfiles",
                column: "AdvisorID",
                principalTable: "Users",
                principalColumn: "UserID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_EmployerProfiles_Users_EmployerID",
                table: "EmployerProfiles",
                column: "EmployerID",
                principalTable: "Users",
                principalColumn: "UserID",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CareerAdvisorProfiles_Users_AdvisorID",
                table: "CareerAdvisorProfiles");

            migrationBuilder.DropForeignKey(
                name: "FK_EmployerProfiles_Users_EmployerID",
                table: "EmployerProfiles");

            migrationBuilder.AlterColumn<int>(
                name: "EmployerID",
                table: "EmployerProfiles",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .Annotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AlterColumn<int>(
                name: "AdvisorID",
                table: "CareerAdvisorProfiles",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .Annotation("SqlServer:Identity", "1, 1");
        }
    }
}
