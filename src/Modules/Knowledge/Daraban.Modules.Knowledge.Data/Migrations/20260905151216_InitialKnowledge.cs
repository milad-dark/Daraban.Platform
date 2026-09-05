using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;

#nullable disable

namespace Daraban.Modules.Knowledge.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialKnowledge : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "knowledge");

            migrationBuilder.CreateTable(
                name: "kb_categories",
                schema: "knowledge",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    slug = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_kb_categories", x => x.id);
                    table.ForeignKey(
                        name: "FK_kb_categories_kb_categories_parent_id",
                        column: x => x.parent_id,
                        principalSchema: "knowledge",
                        principalTable: "kb_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "kb_articles",
                schema: "knowledge",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    summary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    category_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    is_faq = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    author_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    published_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    view_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    helpful_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    not_helpful_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    tags = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    search_vector = table.Column<NpgsqlTsVector>(type: "tsvector", nullable: false)
                        .Annotation("Npgsql:TsVectorConfig", "english")
                        .Annotation("Npgsql:TsVectorProperties", new[] { "title", "content" }),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_kb_articles", x => x.id);
                    table.ForeignKey(
                        name: "FK_kb_articles_kb_categories_category_id",
                        column: x => x.category_id,
                        principalSchema: "knowledge",
                        principalTable: "kb_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "kb_article_targets",
                schema: "knowledge",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    article_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    target_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_recursive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_kb_article_targets", x => x.id);
                    table.ForeignKey(
                        name: "FK_kb_article_targets_kb_articles_article_id",
                        column: x => x.article_id,
                        principalSchema: "knowledge",
                        principalTable: "kb_articles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "kb_feedback",
                schema: "knowledge",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    article_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_helpful = table.Column<bool>(type: "boolean", nullable: false),
                    comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_kb_feedback", x => x.id);
                    table.ForeignKey(
                        name: "FK_kb_feedback_kb_articles_article_id",
                        column: x => x.article_id,
                        principalSchema: "knowledge",
                        principalTable: "kb_articles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "kb_ticket_links",
                schema: "knowledge",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    article_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ticket_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_solution = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    linked_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    linked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_kb_ticket_links", x => x.id);
                    table.ForeignKey(
                        name: "FK_kb_ticket_links_kb_articles_article_id",
                        column: x => x.article_id,
                        principalSchema: "knowledge",
                        principalTable: "kb_articles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_kb_article_targets_article_id",
                schema: "knowledge",
                table: "kb_article_targets",
                column: "article_id");

            migrationBuilder.CreateIndex(
                name: "ix_kb_article_targets_target",
                schema: "knowledge",
                table: "kb_article_targets",
                columns: new[] { "target_type", "target_id" });

            migrationBuilder.CreateIndex(
                name: "uq_kb_article_targets_article_target",
                schema: "knowledge",
                table: "kb_article_targets",
                columns: new[] { "article_id", "target_type", "target_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_kb_articles_author_user_id",
                schema: "knowledge",
                table: "kb_articles",
                column: "author_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_kb_articles_category_id",
                schema: "knowledge",
                table: "kb_articles",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_kb_articles_entity_id",
                schema: "knowledge",
                table: "kb_articles",
                column: "entity_id");

            migrationBuilder.CreateIndex(
                name: "ix_kb_articles_entity_status",
                schema: "knowledge",
                table: "kb_articles",
                columns: new[] { "entity_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_kb_articles_is_faq",
                schema: "knowledge",
                table: "kb_articles",
                column: "is_faq");

            migrationBuilder.CreateIndex(
                name: "ix_kb_articles_search_vector",
                schema: "knowledge",
                table: "kb_articles",
                column: "search_vector")
                .Annotation("Npgsql:IndexMethod", "GIN");

            migrationBuilder.CreateIndex(
                name: "ix_kb_categories_entity_id",
                schema: "knowledge",
                table: "kb_categories",
                column: "entity_id");

            migrationBuilder.CreateIndex(
                name: "ix_kb_categories_parent_id",
                schema: "knowledge",
                table: "kb_categories",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "uq_kb_categories_entity_slug",
                schema: "knowledge",
                table: "kb_categories",
                columns: new[] { "entity_id", "slug" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_kb_feedback_article_id",
                schema: "knowledge",
                table: "kb_feedback",
                column: "article_id");

            migrationBuilder.CreateIndex(
                name: "uq_kb_feedback_article_user",
                schema: "knowledge",
                table: "kb_feedback",
                columns: new[] { "article_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_kb_ticket_links_article_id",
                schema: "knowledge",
                table: "kb_ticket_links",
                column: "article_id");

            migrationBuilder.CreateIndex(
                name: "uq_kb_ticket_links_ticket_article",
                schema: "knowledge",
                table: "kb_ticket_links",
                columns: new[] { "ticket_id", "article_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_kb_ticket_links_ticket_solution",
                schema: "knowledge",
                table: "kb_ticket_links",
                column: "ticket_id",
                unique: true,
                filter: "is_solution = true");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "kb_article_targets",
                schema: "knowledge");

            migrationBuilder.DropTable(
                name: "kb_feedback",
                schema: "knowledge");

            migrationBuilder.DropTable(
                name: "kb_ticket_links",
                schema: "knowledge");

            migrationBuilder.DropTable(
                name: "kb_articles",
                schema: "knowledge");

            migrationBuilder.DropTable(
                name: "kb_categories",
                schema: "knowledge");
        }
    }
}
