using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace tran_nguyen_vi_khang_api.Migrations
{
    /// <inheritdoc />
    public partial class AddModeRequestsAndLocationOptimization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "DisplacementMeters",
                table: "DeviceLocations",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EventType",
                table: "DeviceLocations",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Periodic");

            migrationBuilder.CreateTable(
                name: "DeviceModeRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DeviceId = table.Column<int>(type: "int", nullable: false),
                    ChildId = table.Column<int>(type: "int", nullable: false),
                    TargetMode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ApprovalToken = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewedByUserId = table.Column<int>(type: "int", nullable: true),
                    ExecutedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceModeRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeviceModeRequests_Children_ChildId",
                        column: x => x.ChildId,
                        principalTable: "Children",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DeviceModeRequests_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DeviceModeRequests_Users_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceModeRequests_ChildId_RequestedAt",
                table: "DeviceModeRequests",
                columns: new[] { "ChildId", "RequestedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceModeRequests_DeviceId_Status_ExpiresAt",
                table: "DeviceModeRequests",
                columns: new[] { "DeviceId", "Status", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceModeRequests_ReviewedByUserId",
                table: "DeviceModeRequests",
                column: "ReviewedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeviceModeRequests");

            migrationBuilder.DropColumn(
                name: "DisplacementMeters",
                table: "DeviceLocations");

            migrationBuilder.DropColumn(
                name: "EventType",
                table: "DeviceLocations");
        }
    }
}
