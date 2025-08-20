using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ToDos.Data.Migrations
{
    /// <inheritdoc />
    public partial class DateTimeOffsetConvertersAgain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ToDos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    IsCompleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DueDate = table.Column<long>(type: "INTEGER", nullable: true),
                    CompletedDate = table.Column<long>(type: "INTEGER", nullable: true),
                    CreatedDate = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedDate = table.Column<long>(type: "INTEGER", nullable: false),
                    Priority = table.Column<byte>(type: "INTEGER", nullable: false, defaultValue: (byte)2)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ToDos", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ToDos_IsCompleted_DueDate",
                table: "ToDos",
                columns: new[] { "IsCompleted", "DueDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ToDos");
        }
    }
}
