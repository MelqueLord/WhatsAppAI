-- Run this script on Supabase to initialize the migrations history table
-- and mark all existing migrations as applied.
-- This should only be run ONCE on existing databases that were created with EnsureCreated.

-- Create the migrations history table if it doesn't exist
CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

-- Insert all existing migrations as applied
-- Replace '10.0.0' with your actual EF Core version if different
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES
('20260816180733_AddSubscriptionPlan', '10.0.0'),
('20260817104719_AddMustChangePassword', '10.0.0'),
('20260819113003_AddHandoffAndMediaMessages', '10.0.0'),
('20260819185249_AddTenantDueDate', '10.0.0'),
('20260819205641_AddTenantLineCounts', '10.0.0'),
('20260819211358_AddWhatsAppLineSlots', '10.0.0'),
('20260819212417_AddTenantOperatorLimit', '10.0.0'),
('20260821102557_AddServiceLinesAndAiQueueRouting', '10.0.0'),
('20260824163708_AddBroadcastTables', '10.0.0'),
('20260824225616_AddAssignedLinesJson', '10.0.0'),
('20260824230732_AddKeywordsToServiceLine', '10.0.0'),
('20260824231517_AddQueueTransferMessage', '10.0.0'),
('20260824232650_SyncPendingModelChanges', '10.0.0')
ON CONFLICT DO NOTHING;
