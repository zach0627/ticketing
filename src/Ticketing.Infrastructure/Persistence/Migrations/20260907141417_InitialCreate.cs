using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ticketing.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdminAudits",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false),
                    DetailsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RequestHash = table.Column<byte[]>(type: "binary(32)", nullable: true),
                    ResultJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminAudits", x => x.Id);
                    table.CheckConstraint("CK_AdminAudits_Operation", "    ([OperationId] IS NULL     AND [RequestHash] IS NULL     AND [ResultJson] IS NULL)\n OR ([OperationId] IS NOT NULL AND [RequestHash] IS NOT NULL AND [ResultJson] IS NOT NULL)");
                });

            migrationBuilder.CreateTable(
                name: "AppUsers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    GoogleSubject = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: true),
                    Role = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppUsers", x => x.Id);
                    table.CheckConstraint("CK_AppUsers_Identity", "[PasswordHash] IS NOT NULL OR [GoogleSubject] IS NOT NULL");
                    table.CheckConstraint("CK_AppUsers_Role", "[Role] IN ('Customer','Admin')");
                });

            migrationBuilder.CreateTable(
                name: "Events",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "varchar(3)", unicode: false, maxLength: 3, nullable: false),
                    Category = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Performer = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Genre = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1200)", maxLength: 1200, nullable: false),
                    ImagePath = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Events", x => x.Id);
                    table.CheckConstraint("CK_Events_Category", "[Category] IN ('Concert','Sport')");
                });

            migrationBuilder.CreateTable(
                name: "IdempotencyRecords",
                columns: table => new
                {
                    BuyerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    RequestHash = table.Column<byte[]>(type: "binary(32)", nullable: false),
                    HttpStatus = table.Column<int>(type: "int", nullable: false),
                    ResponseJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdempotencyRecords", x => new { x.BuyerId, x.Key });
                    table.ForeignKey(
                        name: "FK_Idem_Buyer",
                        column: x => x.BuyerId,
                        principalTable: "AppUsers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Performances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    EventId = table.Column<int>(type: "int", nullable: false),
                    Venue = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    City = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    StartsAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                    SalesOpensAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                    SalesClosesAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                    DurationMinutes = table.Column<int>(type: "int", nullable: false),
                    IsSalesPaused = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Performances", x => x.Id)
                        .Annotation("SqlServer:Clustered", true);
                    table.CheckConstraint("CK_Performances_Duration", "[DurationMinutes] > 0");
                    table.CheckConstraint("CK_Performances_Times", "[SalesOpensAtUtc] < [SalesClosesAtUtc] AND [SalesClosesAtUtc] < [StartsAtUtc]");
                    table.ForeignKey(
                        name: "FK_Performances_Event",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "SeatHolds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BuyerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PerformanceId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "varchar(12)", unicode: false, maxLength: 12, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(10,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeatHolds", x => x.Id);
                    table.UniqueConstraint("UQ_SeatHolds_IdBuyerPerformance", x => new { x.Id, x.BuyerId, x.PerformanceId });
                    table.UniqueConstraint("UQ_SeatHolds_IdPerformance", x => new { x.Id, x.PerformanceId });
                    table.CheckConstraint("CK_SeatHolds_Amount", "[TotalAmount] > 0");
                    table.CheckConstraint("CK_SeatHolds_Expiry", "[ExpiresAtUtc] > [CreatedAtUtc]");
                    table.CheckConstraint("CK_SeatHolds_Status", "[Status] IN ('Active','Completed','Cancelled','Expired')");
                    table.ForeignKey(
                        name: "FK_SeatHolds_Buyer",
                        column: x => x.BuyerId,
                        principalTable: "AppUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SeatHolds_Performance",
                        column: x => x.PerformanceId,
                        principalTable: "Performances",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Sections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    PerformanceId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "varchar(1)", unicode: false, maxLength: 1, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Price = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    RowCount = table.Column<int>(type: "int", nullable: false),
                    SeatsPerRow = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sections", x => x.Id);
                    table.UniqueConstraint("UQ_Sections_IdPerformance", x => new { x.Id, x.PerformanceId });
                    table.CheckConstraint("CK_Sections_Code", "[Code] IN ('A','B','C')");
                    table.CheckConstraint("CK_Sections_Values", "[Price] > 0 AND [RowCount] > 0 AND [SeatsPerRow] > 0");
                    table.ForeignKey(
                        name: "FK_Sections_Performance",
                        column: x => x.PerformanceId,
                        principalTable: "Performances",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Orders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HoldId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BuyerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PerformanceId = table.Column<int>(type: "int", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    Currency = table.Column<string>(type: "char(3)", unicode: false, fixedLength: true, maxLength: 3, nullable: false, defaultValue: "TWD"),
                    EventTitleSnapshot = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    StartsAtSnapshot = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orders", x => x.Id);
                    table.CheckConstraint("CK_Orders_Amount", "[TotalAmount] > 0 AND [Currency] = 'TWD'");
                    table.ForeignKey(
                        name: "FK_Orders_Buyer",
                        column: x => x.BuyerId,
                        principalTable: "AppUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Orders_HoldBuyerPerformance",
                        columns: x => new { x.HoldId, x.BuyerId, x.PerformanceId },
                        principalTable: "SeatHolds",
                        principalColumns: new[] { "Id", "BuyerId", "PerformanceId" });
                    table.ForeignKey(
                        name: "FK_Orders_Performance",
                        column: x => x.PerformanceId,
                        principalTable: "Performances",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Seats",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    PerformanceId = table.Column<int>(type: "int", nullable: false),
                    SectionId = table.Column<int>(type: "int", nullable: false),
                    RowNumber = table.Column<int>(type: "int", nullable: false),
                    SeatNumber = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "varchar(12)", unicode: false, maxLength: 12, nullable: false),
                    HoldId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    HeldUntilUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Seats", x => x.Id);
                    table.CheckConstraint("CK_Seats_Number", "[RowNumber] > 0 AND [SeatNumber] > 0");
                    table.CheckConstraint("CK_Seats_State", "    ([Status] = 'Available' AND [HoldId] IS NULL     AND [HeldUntilUtc] IS NULL)\n OR ([Status] = 'Held'      AND [HoldId] IS NOT NULL AND [HeldUntilUtc] IS NOT NULL)\n OR ([Status] = 'Sold'      AND [HoldId] IS NOT NULL AND [HeldUntilUtc] IS NULL)");
                    table.ForeignKey(
                        name: "FK_Seats_HoldPerformance",
                        columns: x => new { x.HoldId, x.PerformanceId },
                        principalTable: "SeatHolds",
                        principalColumns: new[] { "Id", "PerformanceId" });
                    table.ForeignKey(
                        name: "FK_Seats_Performance",
                        column: x => x.PerformanceId,
                        principalTable: "Performances",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Seats_SectionPerformance",
                        columns: x => new { x.SectionId, x.PerformanceId },
                        principalTable: "Sections",
                        principalColumns: new[] { "Id", "PerformanceId" });
                });

            migrationBuilder.CreateTable(
                name: "OrderItems",
                columns: table => new
                {
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SeatId = table.Column<int>(type: "int", nullable: false),
                    SectionCode = table.Column<string>(type: "varchar(1)", unicode: false, maxLength: 1, nullable: false),
                    RowNumber = table.Column<int>(type: "int", nullable: false),
                    SeatNumber = table.Column<int>(type: "int", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    TicketCode = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderItems", x => new { x.OrderId, x.SeatId });
                    table.CheckConstraint("CK_OrderItems_Values", "[RowNumber] > 0 AND [SeatNumber] > 0 AND [UnitPrice] > 0");
                    table.ForeignKey(
                        name: "FK_OrderItems_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrderItems_Seat",
                        column: x => x.SeatId,
                        principalTable: "Seats",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "SeatHoldItems",
                columns: table => new
                {
                    SeatId = table.Column<int>(type: "int", nullable: false),
                    HoldId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SectionCode = table.Column<string>(type: "varchar(1)", unicode: false, maxLength: 1, nullable: false),
                    RowNumber = table.Column<int>(type: "int", nullable: false),
                    SeatNumber = table.Column<int>(type: "int", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(10,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeatHoldItems", x => new { x.HoldId, x.SeatId });
                    table.CheckConstraint("CK_SeatHoldItems_Values", "[RowNumber] > 0 AND [SeatNumber] > 0 AND [UnitPrice] > 0");
                    table.ForeignKey(
                        name: "FK_SeatHoldItems_Seat",
                        column: x => x.SeatId,
                        principalTable: "Seats",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SeatHoldItems_SeatHolds_HoldId",
                        column: x => x.HoldId,
                        principalTable: "SeatHolds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "UX_AdminAudits_OperationId",
                table: "AdminAudits",
                column: "OperationId",
                unique: true,
                filter: "[OperationId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UQ_AppUsers_Email",
                table: "AppUsers",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_AppUsers_GoogleSubject",
                table: "AppUsers",
                column: "GoogleSubject",
                unique: true,
                filter: "[GoogleSubject] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UQ_Events_Code",
                table: "Events",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_OrderItems_Seat",
                table: "OrderItems",
                column: "SeatId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_OrderItems_Ticket",
                table: "OrderItems",
                column: "TicketCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_Buyer",
                table: "Orders",
                columns: new[] { "BuyerId", "CreatedAtUtc", "Id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_BuyerPerformance",
                table: "Orders",
                columns: new[] { "BuyerId", "PerformanceId" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_HoldId_BuyerId_PerformanceId",
                table: "Orders",
                columns: new[] { "HoldId", "BuyerId", "PerformanceId" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_PerformanceId",
                table: "Orders",
                column: "PerformanceId");

            migrationBuilder.CreateIndex(
                name: "UQ_Orders_Hold",
                table: "Orders",
                column: "HoldId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Performances_EventId",
                table: "Performances",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_SeatHoldItems_SeatId",
                table: "SeatHoldItems",
                column: "SeatId");

            migrationBuilder.CreateIndex(
                name: "IX_SeatHolds_PerformanceId",
                table: "SeatHolds",
                column: "PerformanceId");

            migrationBuilder.CreateIndex(
                name: "UX_SeatHolds_ActiveBuyerPerformance",
                table: "SeatHolds",
                columns: new[] { "BuyerId", "PerformanceId" },
                unique: true,
                filter: "[Status] = 'Active'");

            migrationBuilder.CreateIndex(
                name: "IX_Seats_HoldId",
                table: "Seats",
                column: "HoldId");

            migrationBuilder.CreateIndex(
                name: "IX_Seats_HoldId_PerformanceId",
                table: "Seats",
                columns: new[] { "HoldId", "PerformanceId" });

            migrationBuilder.CreateIndex(
                name: "IX_Seats_SectionId_PerformanceId",
                table: "Seats",
                columns: new[] { "SectionId", "PerformanceId" });

            migrationBuilder.CreateIndex(
                name: "UQ_Seats_Address",
                table: "Seats",
                columns: new[] { "PerformanceId", "SectionId", "RowNumber", "SeatNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_Sections_PerformanceCode",
                table: "Sections",
                columns: new[] { "PerformanceId", "Code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdminAudits");

            migrationBuilder.DropTable(
                name: "IdempotencyRecords");

            migrationBuilder.DropTable(
                name: "OrderItems");

            migrationBuilder.DropTable(
                name: "SeatHoldItems");

            migrationBuilder.DropTable(
                name: "Orders");

            migrationBuilder.DropTable(
                name: "Seats");

            migrationBuilder.DropTable(
                name: "SeatHolds");

            migrationBuilder.DropTable(
                name: "Sections");

            migrationBuilder.DropTable(
                name: "AppUsers");

            migrationBuilder.DropTable(
                name: "Performances");

            migrationBuilder.DropTable(
                name: "Events");
        }
    }
}
