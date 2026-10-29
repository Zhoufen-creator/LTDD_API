using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace tran_nguyen_vi_khang_api.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceActivityLogAndRuleEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastModifiedAt",
                table: "MonitoringRules",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "RuleConfig",
                table: "MonitoringRules",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SyncStatus",
                table: "MonitoringRules",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "ServerSaved");

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "MonitoringRules",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "DeviceActivityLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClientEventId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    DeviceId = table.Column<int>(type: "int", nullable: false),
                    ChildId = table.Column<int>(type: "int", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PackageName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    AppName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    StartTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DurationSeconds = table.Column<int>(type: "int", nullable: true),
                    RuleId = table.Column<int>(type: "int", nullable: true),
                    ViolationAction = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ViolationDetails = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RecordedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceActivityLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeviceActivityLogs_Children_ChildId",
                        column: x => x.ChildId,
                        principalTable: "Children",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DeviceActivityLogs_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DeviceActivityLogs_MonitoringRules_RuleId",
                        column: x => x.RuleId,
                        principalTable: "MonitoringRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceActivityLogs_ChildId_EventType_RecordedAt",
                table: "DeviceActivityLogs",
                columns: new[] { "ChildId", "EventType", "RecordedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceActivityLogs_ChildId_RecordedAt",
                table: "DeviceActivityLogs",
                columns: new[] { "ChildId", "RecordedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceActivityLogs_ClientEventId",
                table: "DeviceActivityLogs",
                column: "ClientEventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeviceActivityLogs_DeviceId_RecordedAt",
                table: "DeviceActivityLogs",
                columns: new[] { "DeviceId", "RecordedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceActivityLogs_RuleId",
                table: "DeviceActivityLogs",
                column: "RuleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeviceActivityLogs");

            migrationBuilder.DropColumn(
                name: "LastModifiedAt",
                table: "MonitoringRules");

            migrationBuilder.DropColumn(
                name: "RuleConfig",
                table: "MonitoringRules");

            migrationBuilder.DropColumn(
                name: "SyncStatus",
                table: "MonitoringRules");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "MonitoringRules");
        }
    }
}
