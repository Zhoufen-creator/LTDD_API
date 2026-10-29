using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace tran_nguyen_vi_khang_api.Migrations
{
    /// <inheritdoc />
    public partial class AddLocationRefreshAndBattery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BatteryPercentage",
                table: "Devices",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "BatteryUpdatedAt",
                table: "Devices",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsCharging",
                table: "Devices",
                type: "bit",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LocationRefreshRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ChildId = table.Column<int>(type: "int", nullable: false),
                    DeviceId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocationRefreshRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LocationRefreshRequests_Children_ChildId",
                        column: x => x.ChildId,
                        principalTable: "Children",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LocationRefreshRequests_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LocationRefreshRequests_ChildId_RequestedAt",
                table: "LocationRefreshRequests",
                columns: new[] { "ChildId", "RequestedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_LocationRefreshRequests_DeviceId_Status_ExpiresAt",
                table: "LocationRefreshRequests",
                columns: new[] { "DeviceId", "Status", "ExpiresAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LocationRefreshRequests");

            migrationBuilder.DropColumn(
                name: "BatteryPercentage",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "BatteryUpdatedAt",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "IsCharging",
                table: "Devices");
        }
    }
}
