-- Supabase Seed Script: Platform Admin User
-- Execute this after running EF migrations

-- Insert admin tenant
INSERT INTO "Tenants" (
  "Id", 
  "Name", 
  "Plan", 
  "CreatedAt", 
  "UpdatedAt"
) VALUES (
  gen_random_uuid(),
  'Platform Admin',
  3, -- Enterprise plan
  NOW(),
  NOW()
) ON CONFLICT DO NOTHING;

-- Get tenant ID
WITH admin_tenant AS (
  SELECT "Id" FROM "Tenants" WHERE "Name" = 'Platform Admin' LIMIT 1
)
-- Insert admin user
INSERT INTO "Users" (
  "Id",
  "Email",
  "FullName",
  "PasswordHash",
  "IsVerified",
  "CreatedAt",
  "UpdatedAt"
) 
SELECT 
  gen_random_uuid(),
  'admin@platform.com',
  'Administrator',
  -- BCrypt hash of "Admin@123"
  '$2a$11$mHlhDI5AYoXGWF4fQgr.e.Lh5FZZuI3.7BBFyB3J9xp7Yrxl.x4mu',
  true,
  NOW(),
  NOW()
WHERE NOT EXISTS (
  SELECT 1 FROM "Users" WHERE "Email" = 'admin@platform.com'
) ON CONFLICT ("Email") DO NOTHING;

-- Insert membership
INSERT INTO "TenantMemberships" (
  "Id",
  "TenantId",
  "UserId",
  "Role",
  "CreatedAt",
  "UpdatedAt"
)
SELECT
  gen_random_uuid(),
  (SELECT "Id" FROM "Tenants" WHERE "Name" = 'Platform Admin' LIMIT 1),
  (SELECT "Id" FROM "Users" WHERE "Email" = 'admin@platform.com' LIMIT 1),
  0, -- PlatformAdmin role
  NOW(),
  NOW()
WHERE EXISTS (
  SELECT 1 FROM "Tenants" WHERE "Name" = 'Platform Admin'
)
AND EXISTS (
  SELECT 1 FROM "Users" WHERE "Email" = 'admin@platform.com'
)
ON CONFLICT DO NOTHING;
