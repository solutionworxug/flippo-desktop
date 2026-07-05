using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Flippo.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserDictionaries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "user_dictionaries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    SourceLanguage = table.Column<string>(type: "TEXT", nullable: false),
                    TargetLanguage = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_dictionaries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "user_dictionary_entries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DictionaryId = table.Column<long>(type: "INTEGER", nullable: false),
                    SourceWord = table.Column<string>(type: "TEXT", nullable: false),
                    TargetWord = table.Column<string>(type: "TEXT", nullable: false),
                    PartOfSpeech = table.Column<string>(type: "TEXT", nullable: false),
                    Gender = table.Column<string>(type: "TEXT", nullable: false),
                    ExampleSentence = table.Column<string>(type: "TEXT", nullable: false),
                    ExampleTranslation = table.Column<string>(type: "TEXT", nullable: false),
                    Level = table.Column<string>(type: "TEXT", nullable: false),
                    AcceptedAnswers = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_dictionary_entries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_dictionary_entries_user_dictionaries_DictionaryId",
                        column: x => x.DictionaryId,
                        principalTable: "user_dictionaries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_user_dictionary_entries_DictionaryId",
                table: "user_dictionary_entries",
                column: "DictionaryId");

            migrationBuilder.CreateIndex(
                name: "IX_user_dictionary_entries_SourceWord",
                table: "user_dictionary_entries",
                column: "SourceWord");

            migrationBuilder.CreateIndex(
                name: "IX_user_dictionary_entries_TargetWord",
                table: "user_dictionary_entries",
                column: "TargetWord");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_dictionary_entries");

            migrationBuilder.DropTable(
                name: "user_dictionaries");
        }
    }
}
