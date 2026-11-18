-- =============================================================================
-- The management "My company" page: the permission that edits a shop's settings,
-- and an untranslated holiday name.
-- =============================================================================

INSERT INTO permissions (id, code, name, description, category, is_tenant_grantable)
VALUES (md5('perm:company:update')::uuid, 'company:update', 'Edit company settings',
        'Edit opening hours, closures, booking rules, the logo and the photos', 'company', true);

-- super_admin, company_admin and staff_admin. Every shop shares these system roles.
INSERT INTO role_permissions (role_id, permission_id)
SELECT role.id, md5('perm:company:update')::uuid
FROM (VALUES
    ('10000000-0000-4000-8000-000000000001'::uuid),
    ('10000000-0000-4000-8000-000000000002'::uuid),
    ('10000000-0000-4000-8000-000000000006'::uuid)
) AS role(id);


-- Only staff see a holiday's name, so it is not translated.
ALTER TABLE company_holidays ADD COLUMN name varchar(200) NULL;

DROP TABLE company_holiday_translations;
