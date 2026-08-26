using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhatsAppAI.Infrastructure.Migrations;

public partial class AddBroadcastQueueId : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
                CREATE SCHEMA IF NOT EXISTS "whatsappai";

                CREATE TABLE IF NOT EXISTS "whatsappai"."broadcast_lists" (
                    "id" uuid NOT NULL,
                    "tenant_id" uuid NOT NULL,
                    "name" varchar(100) NOT NULL,
                    "message" varchar(4096) NOT NULL,
                    "status" varchar(20) NOT NULL,
                    "line_phone_number_id" varchar(100) NOT NULL,
                    "queue_id" uuid NULL,
                    "total_count" integer NOT NULL,
                    "sent_count" integer NOT NULL,
                    "failed_count" integer NOT NULL,
                    "created_by_user_id" uuid NOT NULL,
                    "created_at" timestamp with time zone NOT NULL,
                    "started_at" timestamp with time zone NULL,
                    "finished_at" timestamp with time zone NULL,
                    CONSTRAINT "PK_broadcast_lists" PRIMARY KEY ("id")
                );

                ALTER TABLE "whatsappai"."broadcast_lists"
                    ADD COLUMN IF NOT EXISTS "queue_id" uuid NULL;

                CREATE TABLE IF NOT EXISTS "whatsappai"."broadcast_recipients" (
                    "id" uuid NOT NULL,
                    "tenant_id" uuid NOT NULL,
                    "broadcast_list_id" uuid NOT NULL,
                    "contact_id" uuid NOT NULL,
                    "status" varchar(20) NOT NULL,
                    "error_message" varchar(500) NULL,
                    "created_at" timestamp with time zone NOT NULL,
                    "sent_at" timestamp with time zone NULL,
                    CONSTRAINT "PK_broadcast_recipients" PRIMARY KEY ("id"),
                    CONSTRAINT "FK_broadcast_recipients_broadcast_lists_broadcast_list_id"
                        FOREIGN KEY ("broadcast_list_id") REFERENCES "whatsappai"."broadcast_lists" ("id") ON DELETE CASCADE
                );

                CREATE INDEX IF NOT EXISTS "IX_broadcast_lists_tenant_id"
                    ON "whatsappai"."broadcast_lists" ("tenant_id");
                CREATE INDEX IF NOT EXISTS "IX_broadcast_lists_tenant_id_status"
                    ON "whatsappai"."broadcast_lists" ("tenant_id", "status");
                CREATE INDEX IF NOT EXISTS "IX_broadcast_recipients_broadcast_list_id"
                    ON "whatsappai"."broadcast_recipients" ("broadcast_list_id");
                CREATE INDEX IF NOT EXISTS "IX_broadcast_recipients_broadcast_list_id_status"
                    ON "whatsappai"."broadcast_recipients" ("broadcast_list_id", "status");
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_broadcast_recipients_tenant_id_broadcast_list_id_contact_id"
                    ON "whatsappai"."broadcast_recipients" ("tenant_id", "broadcast_list_id", "contact_id");
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE IF EXISTS "whatsappai"."broadcast_lists"
                DROP COLUMN IF EXISTS "queue_id";
            """);
    }
}
