using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Flippo.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "session_records",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SetId = table.Column<long>(type: "INTEGER", nullable: true),
                    SetName = table.Column<string>(type: "TEXT", nullable: false),
                    CorrectCount = table.Column<int>(type: "INTEGER", nullable: false),
                    WrongCount = table.Column<int>(type: "INTEGER", nullable: false),
                    StartedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    DurationMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    WrongEntryIds = table.Column<string>(type: "TEXT", nullable: false),
                    LearnMode = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_session_records", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "vocabulary_sets",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    SourceLanguage = table.Column<string>(type: "TEXT", nullable: false),
                    TargetLanguage = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vocabulary_sets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "vocabulary_entries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SetId = table.Column<long>(type: "INTEGER", nullable: false),
                    SourceText = table.Column<string>(type: "TEXT", nullable: false),
                    TargetText = table.Column<string>(type: "TEXT", nullable: false),
                    AcceptedAnswers = table.Column<string>(type: "TEXT", nullable: false),
                    ExampleSentence = table.Column<string>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false),
                    PartOfSpeech = table.Column<string>(type: "TEXT", nullable: false),
                    Gender = table.Column<string>(type: "TEXT", nullable: false),
                    PluralForm = table.Column<string>(type: "TEXT", nullable: false),
                    VerbForms = table.Column<string>(type: "TEXT", nullable: false),
                    Pronunciation = table.Column<string>(type: "TEXT", nullable: false),
                    Tags = table.Column<string>(type: "TEXT", nullable: false),
                    Mnemonic = table.Column<string>(type: "TEXT", nullable: false),
                    ImagePath = table.Column<string>(type: "TEXT", nullable: false),
                    AudioPath = table.Column<string>(type: "TEXT", nullable: false),
                    Difficulty = table.Column<int>(type: "INTEGER", nullable: false),
                    BoxLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    NextReviewAt = table.Column<long>(type: "INTEGER", nullable: false),
                    CorrectCount = table.Column<int>(type: "INTEGER", nullable: false),
                    WrongCount = table.Column<int>(type: "INTEGER", nullable: false),
                    LastReviewedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    IsArchived = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsLeech = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastIntervalDays = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vocabulary_entries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_vocabulary_entries_vocabulary_sets_SetId",
                        column: x => x.SetId,
                        principalTable: "vocabulary_sets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_vocabulary_entries_IsArchived",
                table: "vocabulary_entries",
                column: "IsArchived");

            migrationBuilder.CreateIndex(
                name: "IX_vocabulary_entries_IsLeech",
                table: "vocabulary_entries",
                column: "IsLeech");

            migrationBuilder.CreateIndex(
                name: "IX_vocabulary_entries_NextReviewAt",
                table: "vocabulary_entries",
                column: "NextReviewAt");

            migrationBuilder.CreateIndex(
                name: "IX_vocabulary_entries_SetId",
                table: "vocabulary_entries",
                column: "SetId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "session_records");

            migrationBuilder.DropTable(
                name: "vocabulary_entries");

            migrationBuilder.DropTable(
                name: "vocabulary_sets");
        }
    }
}
