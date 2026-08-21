-- Orbit Work Management - Large Data Seeding Script
-- Target Workspace: Orbit Workspace (01a01fa6-7b95-7b16-9d06-009664e29263)
-- Target Project: Orbit delivery (01a01fa6-d2e8-7381-bedf-067fbe75d991), Key: ORB
-- Owner/Admin Membership: 01a01fa6-7b95-7723-8c5c-71afe33c4936
-- LOCAL DEV ONLY: never run against a shared/staging/prod database.
--
-- Seeded dev/QA accounts share one Argon2id password hash so they can be used to log in
-- for local QA testing. Override it by invoking psql with:
--   psql -v seed_password_hash='<argon2id-hash>' -f scripts/seed_orbit_large.sql
-- Falls back to a well-known local-only dev hash when no override is given.
\if :{?seed_password_hash}
\else
\set seed_password_hash $argon2id$v=19$m=65536,t=3,p=2$BIZi2t1DrobCZ9tbOh01jA==$sp+YJn4lzGV/wJ2mNBI/ymH8RNieKmS7PdPIPi7wpLw=
\endif

SELECT set_config('orbit.seed_password_hash', :'seed_password_hash', false);

DO $$
DECLARE
    -- Constants
    c_tenant_id UUID := '01a01fa6-7b95-7b16-9d06-009664e29263';
    c_project_id UUID := '01a01fa6-d2e8-7381-bedf-067fbe75d991';
    c_project_key VARCHAR(10) := 'ORB';
    c_admin_membership_id UUID := '01a01fa6-7b95-7723-8c5c-71afe33c4936';
    c_shared_password_hash VARCHAR(255) := current_setting('orbit.seed_password_hash');

    -- Variables
    v_user_ids UUID[] := '{}';
    v_membership_ids UUID[] := '{}';
    v_team_alpha_id UUID;
    v_team_beta_id UUID;
    v_sprint1_id UUID;
    v_sprint2_id UUID;
    v_sprint3_id UUID;

    -- Work Item Arrays
    v_initiative_ids UUID[] := '{}';
    v_epic_ids UUID[] := '{}';
    v_epic_names VARCHAR[] := '{}';
    v_story_ids UUID[] := '{}';
    v_bug_ids UUID[] := '{}';

    -- Loop indices and temporary variables
    i INT;
    j INT;
    k INT;
    v_seq BIGINT := 1;
    v_summary VARCHAR(255);
    v_desc VARCHAR(2000);
    v_ac VARCHAR(5000);
    v_new_item_id UUID;
    v_parent_id UUID;
    v_story_pts NUMERIC(10,2);
    v_assignee_id UUID;
    v_developer_id UUID;
    v_status VARCHAR(32);
    v_sprint_name VARCHAR(255);
    v_sprint_id UUID;
    v_fact_time TIMESTAMP WITH TIME ZONE;
    v_user_idx INT;
    v_bug_id UUID;
    v_bug_target_id UUID;
    v_attachment_id UUID;
    v_attachment_key VARCHAR(255);
    v_admin_uid UUID;
    v_attachment_ids UUID[] := '{}';

    -- Roles & custom fields
    v_member_role_id UUID;
    v_cf_release_phase_id UUID;
    v_cf_customer_impact_id UUID;
    v_cf_regression_severity_id UUID;
    v_cf_security_review_id UUID;
    v_cf_target_release_id UUID;
    v_choice_alpha_id UUID;
    v_choice_beta_id UUID;
    v_choice_ga_id UUID;
    v_choice_low_id UUID;
    v_choice_medium_id UUID;
    v_choice_high_id UUID;
BEGIN
    RAISE NOTICE 'Starting seed script execution...';

    ----------------------------------------------------------------------------
    -- 0. Resolve Target Workspace, Project, and Admin User (admin@orbit.com)
    ----------------------------------------------------------------------------
    IF NOT EXISTS (SELECT 1 FROM workspaces WHERE id = c_tenant_id) THEN
        SELECT id INTO c_tenant_id FROM workspaces ORDER BY created_at ASC LIMIT 1;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM projects WHERE tenant_id = c_tenant_id AND id = c_project_id) THEN
        SELECT id, key INTO c_project_id, c_project_key FROM projects WHERE tenant_id = c_tenant_id ORDER BY created_at ASC LIMIT 1;
    END IF;

    -- Ensure admin@orbit.com account exists with matching Argon2id hash
    SELECT id INTO v_admin_uid FROM user_accounts WHERE normalized_email = 'ADMIN@ORBIT.COM';
    IF v_admin_uid IS NULL THEN
        v_admin_uid := gen_random_uuid();
        INSERT INTO user_accounts(id, normalized_email, display_name, status, created_at, updated_at, version)
        VALUES (v_admin_uid, 'ADMIN@ORBIT.COM', 'Admin', 'Active', NOW(), NOW(), 1);

        INSERT INTO local_credentials(user_id, password_hash, hash_algorithm, hash_parameters_version, changed_at)
        VALUES (v_admin_uid, c_shared_password_hash, 'Argon2id', 1, NOW());
    ELSE
        UPDATE local_credentials SET password_hash = c_shared_password_hash WHERE user_id = v_admin_uid;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM site_role_assignments WHERE user_id = v_admin_uid AND site_role = 'SuperAdministrator') THEN
        INSERT INTO site_role_assignments(user_id, site_role, granted_at)
        VALUES (v_admin_uid, 'SuperAdministrator', NOW());
    END IF;

    SELECT id INTO c_admin_membership_id FROM tenant_memberships WHERE tenant_id = c_tenant_id AND user_id = v_admin_uid;
    IF c_admin_membership_id IS NULL THEN
        c_admin_membership_id := gen_random_uuid();
        INSERT INTO tenant_memberships(id, tenant_id, principal_type, tenant_role, is_active, created_at, user_id, tier)
        VALUES (c_admin_membership_id, c_tenant_id, 'User', 'Owner', true, NOW(), v_admin_uid, 'Standard');
    END IF;

    ----------------------------------------------------------------------------
    -- 1. Create Dev & QA Users
    ----------------------------------------------------------------------------
    -- 5 Devs: dev1..dev5
    -- 2 QAs: qa1..qa2
    FOR i IN 1..7 LOOP
        DECLARE
            v_email VARCHAR(320);
            v_name VARCHAR(120);
            v_uid UUID := gen_random_uuid();
            v_mid UUID := gen_random_uuid();
        BEGIN
            IF i <= 5 THEN
                v_email := 'dev' || i || '@orbit.com';
                v_name := 'Developer ' || i;
            ELSE
                v_email := 'qa' || (i - 5) || '@orbit.com';
                v_name := 'QA Specialist ' || (i - 5);
            END IF;

            -- Check if user exists
            SELECT id INTO v_uid FROM user_accounts WHERE normalized_email = UPPER(v_email);
            IF v_uid IS NULL THEN
                v_uid := gen_random_uuid();
                INSERT INTO user_accounts(id, normalized_email, display_name, status, created_at, updated_at, version)
                VALUES (v_uid, UPPER(v_email), v_name, 'Active', NOW(), NOW(), 1);

                INSERT INTO local_credentials(user_id, password_hash, hash_algorithm, hash_parameters_version, changed_at)
                VALUES (v_uid, c_shared_password_hash, 'Argon2id', 1, NOW());
            END IF;

            -- Check if tenant membership exists
            SELECT id INTO v_mid FROM tenant_memberships WHERE tenant_id = c_tenant_id AND user_id = v_uid;
            IF v_mid IS NULL THEN
                v_mid := gen_random_uuid();
                INSERT INTO tenant_memberships(id, tenant_id, principal_type, tenant_role, is_active, created_at, user_id, tier)
                VALUES (v_mid, c_tenant_id, 'User', 'Member', true, NOW(), v_uid, 'Standard');
            END IF;

            v_user_ids := array_append(v_user_ids, v_uid);
            v_membership_ids := array_append(v_membership_ids, v_mid);
        END;
    END LOOP;

    ----------------------------------------------------------------------------
    -- 2. Create Teams & Memberships
    ----------------------------------------------------------------------------
    -- Team Alpha
    SELECT id INTO v_team_alpha_id FROM teams WHERE tenant_id = c_tenant_id AND name = 'Alpha Team';
    IF v_team_alpha_id IS NULL THEN
        v_team_alpha_id := gen_random_uuid();
        INSERT INTO teams(id, tenant_id, name, created_by_membership_id, created_at, updated_at)
        VALUES (v_team_alpha_id, c_tenant_id, 'Alpha Team', c_admin_membership_id, NOW(), NOW());
    END IF;

    -- Team Beta
    SELECT id INTO v_team_beta_id FROM teams WHERE tenant_id = c_tenant_id AND name = 'Beta Team';
    IF v_team_beta_id IS NULL THEN
        v_team_beta_id := gen_random_uuid();
        INSERT INTO teams(id, tenant_id, name, created_by_membership_id, created_at, updated_at)
        VALUES (v_team_beta_id, c_tenant_id, 'Beta Team', c_admin_membership_id, NOW(), NOW());
    END IF;

    -- Add members to Team Alpha (Dev 1, 2, 3 and QA 1)
    DELETE FROM team_memberships WHERE tenant_id = c_tenant_id AND team_id = v_team_alpha_id;
    INSERT INTO team_memberships(id, tenant_id, team_id, membership_id, created_at) VALUES
    (gen_random_uuid(), c_tenant_id, v_team_alpha_id, v_membership_ids[1], NOW()),
    (gen_random_uuid(), c_tenant_id, v_team_alpha_id, v_membership_ids[2], NOW()),
    (gen_random_uuid(), c_tenant_id, v_team_alpha_id, v_membership_ids[3], NOW()),
    (gen_random_uuid(), c_tenant_id, v_team_alpha_id, v_membership_ids[6], NOW());

    -- Add members to Team Beta (Dev 4, 5 and QA 2)
    DELETE FROM team_memberships WHERE tenant_id = c_tenant_id AND team_id = v_team_beta_id;
    INSERT INTO team_memberships(id, tenant_id, team_id, membership_id, created_at) VALUES
    (gen_random_uuid(), c_tenant_id, v_team_beta_id, v_membership_ids[4], NOW()),
    (gen_random_uuid(), c_tenant_id, v_team_beta_id, v_membership_ids[5], NOW()),
    (gen_random_uuid(), c_tenant_id, v_team_beta_id, v_membership_ids[7], NOW());

    ----------------------------------------------------------------------------
    -- 3. Create Sprints
    ----------------------------------------------------------------------------
    -- Sprint 1 (Past, Closed)
    SELECT id INTO v_sprint1_id FROM sprints WHERE tenant_id = c_tenant_id AND project_id = c_project_id AND name = 'Sprint 1';
    IF v_sprint1_id IS NULL THEN
        v_sprint1_id := gen_random_uuid();
        INSERT INTO sprints(id, tenant_id, project_id, name, goal, state, start_date, end_date, version, created_at, updated_at)
        VALUES (v_sprint1_id, c_tenant_id, c_project_id, 'Sprint 1', 'MFA and Security Baseline', 'Closed', '2026-08-01', '2026-08-14', 2, NOW(), NOW());
    ELSE
        UPDATE sprints SET state = 'Closed', start_date = '2026-08-01', end_date = '2026-08-14' WHERE id = v_sprint1_id;
    END IF;

    -- Sprint 2 (Active)
    SELECT id INTO v_sprint2_id FROM sprints WHERE tenant_id = c_tenant_id AND project_id = c_project_id AND name = 'Sprint 2';
    IF v_sprint2_id IS NULL THEN
        v_sprint2_id := gen_random_uuid();
        INSERT INTO sprints(id, tenant_id, project_id, name, goal, state, start_date, end_date, version, created_at, updated_at)
        VALUES (v_sprint2_id, c_tenant_id, c_project_id, 'Sprint 2', 'Dashboard and Collaboration Features', 'Active', '2026-08-15', '2026-08-28', 1, NOW(), NOW());
    ELSE
        UPDATE sprints SET state = 'Active', start_date = '2026-08-15', end_date = '2026-08-28' WHERE id = v_sprint2_id;
    END IF;

    -- Sprint 3 (Future)
    SELECT id INTO v_sprint3_id FROM sprints WHERE tenant_id = c_tenant_id AND project_id = c_project_id AND name = 'Sprint 3';
    IF v_sprint3_id IS NULL THEN
        v_sprint3_id := gen_random_uuid();
        INSERT INTO sprints(id, tenant_id, project_id, name, goal, state, start_date, end_date, version, created_at, updated_at)
        VALUES (v_sprint3_id, c_tenant_id, c_project_id, 'Sprint 3', 'Performance Tuning & Advanced Reporting', 'Future', '2026-08-29', '2026-09-11', 1, NOW(), NOW());
    ELSE
        UPDATE sprints SET state = 'Future', start_date = '2026-08-29', end_date = '2026-09-11' WHERE id = v_sprint3_id;
    END IF;

    ----------------------------------------------------------------------------
    -- Clean existing Work Items and Agile facts in this project for clean start
    ----------------------------------------------------------------------------
    DELETE FROM sprint_scope_facts WHERE tenant_id = c_tenant_id AND sprint_id IN (v_sprint1_id, v_sprint2_id, v_sprint3_id);
    DELETE FROM sprint_memberships WHERE tenant_id = c_tenant_id AND sprint_id IN (v_sprint1_id, v_sprint2_id, v_sprint3_id);
    DELETE FROM work_item_comments WHERE tenant_id = c_tenant_id AND work_item_id IN (SELECT id FROM work_items WHERE tenant_id = c_tenant_id AND project_id = c_project_id);
    DELETE FROM work_item_links WHERE tenant_id = c_tenant_id AND (source_work_item_id IN (SELECT id FROM work_items WHERE tenant_id = c_tenant_id AND project_id = c_project_id) OR target_work_item_id IN (SELECT id FROM work_items WHERE tenant_id = c_tenant_id AND project_id = c_project_id));
    DELETE FROM attachments WHERE tenant_id = c_tenant_id AND work_item_id IN (SELECT id FROM work_items WHERE tenant_id = c_tenant_id AND project_id = c_project_id);
    DELETE FROM work_items WHERE tenant_id = c_tenant_id AND project_id = c_project_id;

    ----------------------------------------------------------------------------
    -- 4. Seed 5 Initiatives
    ----------------------------------------------------------------------------
    RAISE NOTICE 'Seeding 5 Initiatives...';
    FOR i IN 1..5 LOOP
        v_new_item_id := gen_random_uuid();
        v_summary := 'Initiative ' || i || ': ';
        IF i = 1 THEN v_summary := v_summary || 'Core Platform Architecture Redesign';
        ELSIF i = 2 THEN v_summary := v_summary || 'Data Ingestion & Integration Hub';
        ELSIF i = 3 THEN v_summary := v_summary || 'Advanced Business Analytics Suite';
        ELSIF i = 4 THEN v_summary := v_summary || 'Next-Gen Security & Compliance System';
        ELSIF i = 5 THEN v_summary := v_summary || 'Global User Experience Revamp';
        END IF;

        v_desc := '<p>High-level strategic initiative focused on ' || v_summary || '. Drives key organizational outcomes.</p>';

        INSERT INTO work_items(id, tenant_id, project_id, sequence_number, key, summary, description, type, status, priority, rank, version, created_at, updated_at)
        VALUES (v_new_item_id, c_tenant_id, c_project_id, v_seq, c_project_key || '-' || v_seq, v_summary, v_desc, 'Initiative', 'InProgress', 'High', v_seq * 1024.0, 1, NOW() - INTERVAL '30 days', NOW());

        v_initiative_ids := array_append(v_initiative_ids, v_new_item_id);
        v_seq := v_seq + 1;
    END LOOP;

    ----------------------------------------------------------------------------
    -- 5. Seed 10 Epics (2 per Initiative)
    ----------------------------------------------------------------------------
    RAISE NOTICE 'Seeding 10 Epics...';
    v_epic_names := ARRAY[
        'Authentication Flow', 'Audit Logging UI',
        'Direct Sync Adapter', 'Public API Gateways',
        'Interactive Dashboards', 'Report Generators',
        'GDPR Compliance Data', 'At-Rest Encryption',
        'Collaborative Boards', 'Adaptive Mobile App'
    ];

    FOR i IN 1..10 LOOP
        v_new_item_id := gen_random_uuid();
        v_summary := 'Epic ' || i || ': ' || v_epic_names[i];
        v_desc := '<p>Feature Epic to support ' || v_summary || '. Enables core functional enhancements.</p>';
        v_parent_id := v_initiative_ids[((i - 1) / 2) + 1];

        INSERT INTO work_items(id, tenant_id, project_id, sequence_number, key, summary, description, type, status, priority, rank, version, created_at, updated_at, parent_id, epic_name)
        VALUES (v_new_item_id, c_tenant_id, c_project_id, v_seq, c_project_key || '-' || v_seq, v_summary, v_desc, 'Epic', 'InProgress', 'High', v_seq * 1024.0, 1, NOW() - INTERVAL '25 days', NOW(), v_parent_id, v_epic_names[i]);

        v_epic_ids := array_append(v_epic_ids, v_new_item_id);
        v_seq := v_seq + 1;
    END LOOP;

    ----------------------------------------------------------------------------
    -- 6. Seed 100 Stories (10 per Epic)
    ----------------------------------------------------------------------------
    RAISE NOTICE 'Seeding 100 Stories...';
    FOR i IN 1..100 LOOP
        v_new_item_id := gen_random_uuid();
        -- Associate with Epic (1..10)
        v_parent_id := v_epic_ids[((i - 1) / 10) + 1];
        v_sprint_name := NULL;
        v_sprint_id := NULL;

        -- HTML AC Table (TipTap rich text format)
        v_ac := '<table><thead><tr><th>As a</th><th>When</th><th>Then</th><th>Dev</th><th>UAT</th><th>Production</th><th>Comments</th></tr></thead><tbody>' ||
                '<tr><td>User</td><td>Inputting valid credentials</td><td>Authenticated session is established</td><td>Verified</td><td>Verified</td><td>Pending</td><td>Initial check passed</td></tr>' ||
                '<tr><td>Guest</td><td>Trying to access dashboard</td><td>Denied with clear access message</td><td>Verified</td><td>Verified</td><td>Pending</td><td>-</td></tr>' ||
                '<tr><td>Developer</td><td>Checking logs</td><td>Security event is printed with metadata</td><td>Verified</td><td>Verified</td><td>Pending</td><td>Audit trail entry verified</td></tr>' ||
                '<tr><td>Admin</td><td>Viewing reports</td><td>Standard metrics are loaded within 1s</td><td>Verified</td><td>Verified</td><td>Pending</td><td>Performance NFR verified</td></tr>' ||
                '<tr><td>System</td><td>Database connection fails</td><td>Retries thrice and alerts admin</td><td>Verified</td><td>Verified</td><td>Pending</td><td>Failover tested</td></tr>' ||
                '</tbody></table>';

        -- Determine Sprints & Statuses
        IF i <= 40 THEN
            -- Sprint 1 (Completed / Done)
            v_sprint_name := 'Sprint 1';
            v_sprint_id := v_sprint1_id;
            v_status := 'Done';
        ELSIF i <= 70 THEN
            -- Sprint 2 (Active Sprint)
            v_sprint_name := 'Sprint 2';
            v_sprint_id := v_sprint2_id;
            -- 5 Completed (Done) stories to simulate burndown progress
            -- 15 InProgress, 10 Selected
            IF i <= 45 THEN
                v_status := 'Done';
            ELSIF i <= 60 THEN
                v_status := 'InProgress';
            ELSE
                v_status := 'Selected';
            END IF;
        ELSIF i <= 90 THEN
            -- Sprint 3 (Future Sprint)
            v_sprint_name := 'Sprint 3';
            v_sprint_id := v_sprint3_id;
            v_status := 'Backlog';
        ELSE
            -- Backlog, no Sprint
            v_status := 'Backlog';
        END IF;

        -- Story points: 1, 2, 3, 5, 8
        IF (i % 5) = 0 THEN v_story_pts := 1.0;
        ELSIF (i % 5) = 1 THEN v_story_pts := 2.0;
        ELSIF (i % 5) = 2 THEN v_story_pts := 3.0;
        ELSIF (i % 5) = 3 THEN v_story_pts := 5.0;
        ELSE v_story_pts := 8.0;
        END IF;

        -- Distribute assignees (1..7)
        v_user_idx := (i % 7) + 1;
        v_assignee_id := v_user_ids[v_user_idx];
        v_developer_id := v_user_ids[v_user_idx];

        v_summary := 'Story ' || i || ': Technical implementation of ' || v_epic_names[((i - 1) / 10) + 1] || ' module component part ' || ((i - 1) % 10 + 1);
        v_desc := '<p>Complete user story for component detail verification. Follow criteria and testing table below.</p>';

        INSERT INTO work_items(id, tenant_id, project_id, sequence_number, key, summary, description, type, status, priority, rank, version, created_at, updated_at, parent_id, epic_name, acceptance_criteria, assignee_user_id, developer_user_id, product_owner_user_id, sprint_name, story_points, identified_on, start_date)
        VALUES (v_new_item_id, c_tenant_id, c_project_id, v_seq, c_project_key || '-' || v_seq, v_summary, v_desc, 'Story', v_status, 'Medium', v_seq * 1024.0, 1, NOW() - INTERVAL '20 days', NOW(), v_parent_id, v_epic_names[((i - 1) / 10) + 1], v_ac, v_assignee_id, v_developer_id, c_admin_membership_id, v_sprint_name, v_story_pts, 'QA Iteration', CURRENT_DATE);

        v_story_ids := array_append(v_story_ids, v_new_item_id);

        -- Add Sprint Memberships and Facts
        IF v_sprint_id IS NOT NULL THEN
            -- Add membership
            INSERT INTO sprint_memberships(id, tenant_id, sprint_id, work_item_id, added_at)
            VALUES (gen_random_uuid(), c_tenant_id, v_sprint_id, v_new_item_id, NOW() - INTERVAL '15 days');

            -- Added fact (happens at start of sprint)
            v_fact_time := CASE 
                WHEN v_sprint_name = 'Sprint 1' THEN TIMESTAMP WITH TIME ZONE '2026-08-01 09:00:00+00'
                WHEN v_sprint_name = 'Sprint 2' THEN TIMESTAMP WITH TIME ZONE '2026-08-15 09:00:00+00'
                ELSE NOW()
            END;

            INSERT INTO sprint_scope_facts(id, tenant_id, sprint_id, work_item_id, fact_type, estimate_delta, occurred_at, recorded_at)
            VALUES (gen_random_uuid(), c_tenant_id, v_sprint_id, v_new_item_id, 'SprintAdded', v_story_pts, v_fact_time, NOW());

            -- If Sprint 1 (Completed), complete it gradually
            IF v_sprint_name = 'Sprint 1' THEN
                -- Stagger completion day between day 1 and 13
                v_fact_time := TIMESTAMP WITH TIME ZONE '2026-08-01 17:00:00+00' + ((i % 13) || ' days')::interval;
                INSERT INTO sprint_scope_facts(id, tenant_id, sprint_id, work_item_id, fact_type, estimate_delta, occurred_at, recorded_at)
                VALUES (gen_random_uuid(), c_tenant_id, v_sprint_id, v_new_item_id, 'StatusChanged', -v_story_pts, v_fact_time, NOW());
            END IF;

            -- If Sprint 2 (Active), complete stories 41-45
            IF v_sprint_name = 'Sprint 2' AND v_status = 'Done' THEN
                v_fact_time := TIMESTAMP WITH TIME ZONE '2026-08-15 17:00:00+00' + ((i % 5) || ' days')::interval;
                INSERT INTO sprint_scope_facts(id, tenant_id, sprint_id, work_item_id, fact_type, estimate_delta, occurred_at, recorded_at)
                VALUES (gen_random_uuid(), c_tenant_id, v_sprint_id, v_new_item_id, 'StatusChanged', -v_story_pts, v_fact_time, NOW());
            END IF;
        END IF;

        v_seq := v_seq + 1;
    END LOOP;

    -- Add SprintCompleted fact for Sprint 1
    INSERT INTO sprint_scope_facts(id, tenant_id, sprint_id, work_item_id, fact_type, estimate_delta, occurred_at, recorded_at)
    VALUES (gen_random_uuid(), c_tenant_id, v_sprint1_id, NULL, 'SprintCompleted', NULL, TIMESTAMP WITH TIME ZONE '2026-08-14 18:00:00+00', NOW());

    ----------------------------------------------------------------------------
    -- 7. Seed 5 Subtasks for EACH Story (500 Subtasks total)
    ----------------------------------------------------------------------------
    RAISE NOTICE 'Seeding 500 Subtasks...';
    FOR i IN 1..100 LOOP
        v_parent_id := v_story_ids[i];
        -- Parent story parameters
        SELECT status, epic_name, assignee_user_id, developer_user_id, sprint_name INTO v_status, v_sprint_name, v_assignee_id, v_developer_id FROM work_items WHERE id = v_parent_id;

        FOR k IN 1..5 LOOP
            v_new_item_id := gen_random_uuid();
            v_summary := 'Sub-task ' || k || ' for Story ORB-' || (i + 15) || ': Verify detail implementation ' || k;
            v_desc := '<p>Verification subtask ' || k || ' detailing unit and integration tests setup for parent story ORB-' || (i + 15) || '.</p>';

            INSERT INTO work_items(id, tenant_id, project_id, sequence_number, key, summary, description, type, status, priority, rank, version, created_at, updated_at, parent_id, epic_name, assignee_user_id, developer_user_id, product_owner_user_id, sprint_name)
            VALUES (v_new_item_id, c_tenant_id, c_project_id, v_seq, c_project_key || '-' || v_seq, v_summary, v_desc, 'Subtask', v_status, 'Low', v_seq * 1024.0, 1, NOW() - INTERVAL '18 days', NOW(), v_parent_id, v_sprint_name, v_assignee_id, v_developer_id, c_admin_membership_id, v_sprint_name);

            v_seq := v_seq + 1;
        END LOOP;
    END LOOP;

    ----------------------------------------------------------------------------
    -- 8. Seed 10 Bugs (assigned to team members, related to stories)
    ----------------------------------------------------------------------------
    RAISE NOTICE 'Seeding 10 Bugs...';
    FOR i IN 1..10 LOOP
        v_new_item_id := gen_random_uuid();
        -- Associate with developer (1..5)
        v_user_idx := ((i - 1) % 5) + 1;
        v_developer_id := v_user_ids[v_user_idx];
        v_assignee_id := v_developer_id;

        v_summary := 'Bug ' || i || ': Defect in component execution of ' || v_epic_names[i] || ' causing layout overflow';
        v_desc := '<p><strong>Steps to conduct:</strong><br/>1. Log in as standard user.<br/>2. Open board view.<br/>3. Verify layout behavior.<br/></p>
<p><strong>Actual result:</strong> UI breaks due to missing check constraint error.<br/><strong>Expected result:</strong> Smooth page resizing.</p>';

        INSERT INTO work_items(id, tenant_id, project_id, sequence_number, key, summary, description, type, status, priority, rank, version, created_at, updated_at, assignee_user_id, developer_user_id, product_owner_user_id, story_points, identified_on, steps_to_conduct)
        VALUES (v_new_item_id, c_tenant_id, c_project_id, v_seq, c_project_key || '-' || v_seq, v_summary, v_desc, 'Bug', 'InProgress', 'High', v_seq * 1024.0, 1, NOW(), NOW(), v_assignee_id, v_developer_id, c_admin_membership_id, 2.0, 'Chrome/macOS', 'Open browser and check board size');

        -- Link bug to a corresponding story (e.g. Story 1 to 10)
        v_bug_target_id := v_story_ids[i];
        INSERT INTO work_item_links(id, tenant_id, source_work_item_id, target_work_item_id, kind, created_at)
        VALUES (gen_random_uuid(), c_tenant_id, v_new_item_id, v_bug_target_id, 'RelatesTo', NOW());

        -- Add Dev & QA watchers to the Bug (for Mailpit email tests)
        INSERT INTO work_item_watchers(id, tenant_id, work_item_id, user_id, created_at) VALUES
        (gen_random_uuid(), c_tenant_id, v_new_item_id, v_user_ids[6], NOW()), -- QA 1
        (gen_random_uuid(), c_tenant_id, v_new_item_id, v_user_ids[1], NOW()); -- Dev 1

        v_bug_ids := array_append(v_bug_ids, v_new_item_id);
        v_seq := v_seq + 1;
    END LOOP;

    ----------------------------------------------------------------------------
    -- 9. Seed Dependency Links (Blocks dependencies)
    ----------------------------------------------------------------------------
    RAISE NOTICE 'Adding Dependency Links...';
    -- Establish blockages
    -- Story 11 blocks Story 12
    -- Story 13 blocks Story 14
    -- Story 15 blocks Story 16
    -- Story 17 blocks Story 18
    -- Story 19 blocks Story 20
    FOR i IN 1..5 LOOP
        INSERT INTO work_item_links(id, tenant_id, source_work_item_id, target_work_item_id, kind, created_at)
        VALUES (gen_random_uuid(), c_tenant_id, v_story_ids[i * 2 + 9], v_story_ids[i * 2 + 10], 'Blocks', NOW());
    END LOOP;

    ----------------------------------------------------------------------------
    -- 10. Add comments
    ----------------------------------------------------------------------------
    RAISE NOTICE 'Adding QA Comments...';
    FOR i IN 1..10 LOOP
        -- QA comment on story
        INSERT INTO work_item_comments(id, tenant_id, work_item_id, body, created_at, updated_at, author_membership_id, version, mentioned_user_ids)
        VALUES (gen_random_uuid(), c_tenant_id, v_story_ids[i], '<strong>QA specialist review:</strong> Acceptance criteria completely verified in UAT environment. Moving to Done.', NOW() - INTERVAL '1 day', NOW() - INTERVAL '1 day', v_membership_ids[6], 1, '{}'::uuid[]);

        -- Developer check-in comment on bug
        v_bug_id := v_bug_ids[i];
        INSERT INTO work_item_comments(id, tenant_id, work_item_id, body, created_at, updated_at, author_membership_id, version, mentioned_user_ids)
        VALUES (gen_random_uuid(), c_tenant_id, v_bug_id, '<p>Developer investigating the stack trace. This seems related to database index locking. Applying hotfix.</p>', NOW(), NOW(), v_membership_ids[((i - 1) % 5) + 1], 1, '{}'::uuid[]);
    END LOOP;

    ----------------------------------------------------------------------------
    -- 11. Add mock attachments (images)
    ----------------------------------------------------------------------------
    RAISE NOTICE 'Adding attachments...';
    FOR i IN 1..5 LOOP
        v_attachment_id := gen_random_uuid();
        v_attachment_key := 'attachments/' || v_attachment_id || '.png';
        INSERT INTO attachments(id, tenant_id, work_item_id, file_name, content_type, size_bytes, object_key, uploaded_by_membership_id, uploaded_at, scan_status, scanned_at)
        VALUES (v_attachment_id, c_tenant_id, v_story_ids[i], 'layout_screenshot_' || i || '.png', 'image/png', 10240 * i, v_attachment_key, v_membership_ids[6], NOW(), 'Clean', NOW());

        -- Update work_items table arrays to list attachments
        UPDATE work_items SET attachment_names = array_append(attachment_names, 'layout_screenshot_' || i || '.png') WHERE id = v_story_ids[i];

        v_attachment_ids := array_append(v_attachment_ids, v_attachment_id);
    END LOOP;

    ----------------------------------------------------------------------------
    -- 12. Grant every seeded dev/QA member the "Member" project role
    ----------------------------------------------------------------------------
    -- Without this, dev1-5/qa1-2 (TenantRole 'Member', no tenant-wide access shortcut)
    -- would have zero visibility into the seeded project under the query-level permission
    -- model (Domain.Access.ProjectAccessQuery) - only c_admin_membership_id (Owner) could
    -- see it. Every tenant is seeded with three system roles (Administrator/Member/Viewer,
    -- see Role.SeedSystemRoles) at provisioning time, so "Member" always exists here.
    RAISE NOTICE 'Assigning project roles to seeded members...';
    SELECT id INTO v_member_role_id FROM roles WHERE tenant_id = c_tenant_id AND name = 'Member';
    IF v_member_role_id IS NOT NULL THEN
        FOR i IN 1..array_length(v_membership_ids, 1) LOOP
            INSERT INTO project_role_assignments(id, tenant_id, project_id, membership_id, role_id, created_at)
            VALUES (gen_random_uuid(), c_tenant_id, c_project_id, v_membership_ids[i], v_member_role_id, NOW())
            ON CONFLICT (tenant_id, project_id, membership_id) DO UPDATE SET role_id = EXCLUDED.role_id;
        END LOOP;
    END IF;

    ----------------------------------------------------------------------------
    -- 13. Seed project-scoped custom fields and their values
    ----------------------------------------------------------------------------
    RAISE NOTICE 'Seeding custom field definitions...';

    SELECT id INTO v_cf_release_phase_id FROM custom_field_definitions WHERE tenant_id = c_tenant_id AND project_id = c_project_id AND key = 'release_phase';
    IF v_cf_release_phase_id IS NULL THEN
        v_cf_release_phase_id := gen_random_uuid();
        INSERT INTO custom_field_definitions(tenant_id, id, project_id, key, label, field_type, required, "order", enabled, applicable_types, version, created_at, updated_at)
        VALUES (c_tenant_id, v_cf_release_phase_id, c_project_id, 'release_phase', 'Release Phase', 'SingleChoice', false, 0, true, '{}', 1, NOW(), NOW());

        v_choice_alpha_id := gen_random_uuid();
        v_choice_beta_id := gen_random_uuid();
        v_choice_ga_id := gen_random_uuid();
        INSERT INTO custom_field_choice_options(tenant_id, custom_field_definition_id, id, label, "order") VALUES
        (c_tenant_id, v_cf_release_phase_id, v_choice_alpha_id, 'Alpha', 0),
        (c_tenant_id, v_cf_release_phase_id, v_choice_beta_id, 'Beta', 1),
        (c_tenant_id, v_cf_release_phase_id, v_choice_ga_id, 'GA', 2);
    ELSE
        SELECT id INTO v_choice_alpha_id FROM custom_field_choice_options WHERE tenant_id = c_tenant_id AND custom_field_definition_id = v_cf_release_phase_id AND label = 'Alpha';
        SELECT id INTO v_choice_beta_id FROM custom_field_choice_options WHERE tenant_id = c_tenant_id AND custom_field_definition_id = v_cf_release_phase_id AND label = 'Beta';
        SELECT id INTO v_choice_ga_id FROM custom_field_choice_options WHERE tenant_id = c_tenant_id AND custom_field_definition_id = v_cf_release_phase_id AND label = 'GA';
    END IF;

    SELECT id INTO v_cf_customer_impact_id FROM custom_field_definitions WHERE tenant_id = c_tenant_id AND project_id = c_project_id AND key = 'customer_impact';
    IF v_cf_customer_impact_id IS NULL THEN
        v_cf_customer_impact_id := gen_random_uuid();
        INSERT INTO custom_field_definitions(tenant_id, id, project_id, key, label, field_type, required, "order", enabled, applicable_types, version, created_at, updated_at)
        VALUES (c_tenant_id, v_cf_customer_impact_id, c_project_id, 'customer_impact', 'Customer Impact', 'Text', false, 1, true, '{}', 1, NOW(), NOW());
    END IF;

    SELECT id INTO v_cf_regression_severity_id FROM custom_field_definitions WHERE tenant_id = c_tenant_id AND project_id = c_project_id AND key = 'regression_severity';
    IF v_cf_regression_severity_id IS NULL THEN
        v_cf_regression_severity_id := gen_random_uuid();
        INSERT INTO custom_field_definitions(tenant_id, id, project_id, key, label, field_type, required, "order", enabled, applicable_types, version, created_at, updated_at)
        VALUES (c_tenant_id, v_cf_regression_severity_id, c_project_id, 'regression_severity', 'Regression Severity', 'SingleChoice', false, 2, true, ARRAY['Bug'], 1, NOW(), NOW());

        v_choice_low_id := gen_random_uuid();
        v_choice_medium_id := gen_random_uuid();
        v_choice_high_id := gen_random_uuid();
        INSERT INTO custom_field_choice_options(tenant_id, custom_field_definition_id, id, label, "order") VALUES
        (c_tenant_id, v_cf_regression_severity_id, v_choice_low_id, 'Low', 0),
        (c_tenant_id, v_cf_regression_severity_id, v_choice_medium_id, 'Medium', 1),
        (c_tenant_id, v_cf_regression_severity_id, v_choice_high_id, 'High', 2);
    ELSE
        SELECT id INTO v_choice_low_id FROM custom_field_choice_options WHERE tenant_id = c_tenant_id AND custom_field_definition_id = v_cf_regression_severity_id AND label = 'Low';
        SELECT id INTO v_choice_medium_id FROM custom_field_choice_options WHERE tenant_id = c_tenant_id AND custom_field_definition_id = v_cf_regression_severity_id AND label = 'Medium';
        SELECT id INTO v_choice_high_id FROM custom_field_choice_options WHERE tenant_id = c_tenant_id AND custom_field_definition_id = v_cf_regression_severity_id AND label = 'High';
    END IF;

    SELECT id INTO v_cf_security_review_id FROM custom_field_definitions WHERE tenant_id = c_tenant_id AND project_id = c_project_id AND key = 'needs_security_review';
    IF v_cf_security_review_id IS NULL THEN
        v_cf_security_review_id := gen_random_uuid();
        INSERT INTO custom_field_definitions(tenant_id, id, project_id, key, label, field_type, required, "order", enabled, applicable_types, version, created_at, updated_at)
        VALUES (c_tenant_id, v_cf_security_review_id, c_project_id, 'needs_security_review', 'Needs Security Review', 'Checkbox', false, 3, true, '{}', 1, NOW(), NOW());
    END IF;

    SELECT id INTO v_cf_target_release_id FROM custom_field_definitions WHERE tenant_id = c_tenant_id AND project_id = c_project_id AND key = 'target_release';
    IF v_cf_target_release_id IS NULL THEN
        v_cf_target_release_id := gen_random_uuid();
        INSERT INTO custom_field_definitions(tenant_id, id, project_id, key, label, field_type, required, "order", enabled, applicable_types, version, created_at, updated_at)
        VALUES (c_tenant_id, v_cf_target_release_id, c_project_id, 'target_release', 'Target Release', 'Date', false, 4, true, '{}', 1, NOW(), NOW());
    END IF;

    RAISE NOTICE 'Seeding custom field values for stories and bugs...';
    FOR i IN 1..10 LOOP
        INSERT INTO work_item_custom_field_values(id, tenant_id, work_item_id, custom_field_definition_id, "values", created_at, updated_at) VALUES
        (gen_random_uuid(), c_tenant_id, v_story_ids[i], v_cf_release_phase_id, ARRAY[(CASE WHEN i <= 4 THEN v_choice_ga_id WHEN i <= 7 THEN v_choice_beta_id ELSE v_choice_alpha_id END)::text], NOW(), NOW()),
        (gen_random_uuid(), c_tenant_id, v_story_ids[i], v_cf_customer_impact_id, ARRAY['Affects ' || (i * 120) || ' active accounts across the EU region.'], NOW(), NOW()),
        (gen_random_uuid(), c_tenant_id, v_story_ids[i], v_cf_target_release_id, ARRAY[(CURRENT_DATE + (i || ' days')::interval)::date::text], NOW(), NOW())
        ON CONFLICT DO NOTHING;
    END LOOP;

    FOR i IN 1..10 LOOP
        INSERT INTO work_item_custom_field_values(id, tenant_id, work_item_id, custom_field_definition_id, "values", created_at, updated_at) VALUES
        (gen_random_uuid(), c_tenant_id, v_bug_ids[i], v_cf_regression_severity_id, ARRAY[(CASE WHEN i <= 3 THEN v_choice_high_id WHEN i <= 7 THEN v_choice_medium_id ELSE v_choice_low_id END)::text], NOW(), NOW()),
        (gen_random_uuid(), c_tenant_id, v_bug_ids[i], v_cf_security_review_id, ARRAY[(CASE WHEN i <= 2 THEN 'true' ELSE 'false' END)], NOW(), NOW())
        ON CONFLICT DO NOTHING;
    END LOOP;

    ----------------------------------------------------------------------------
    -- 14. Populate modern work_items columns: due_date, team_id, flag, cover, archive
    ----------------------------------------------------------------------------
    RAISE NOTICE 'Populating due dates, team assignment, flags, cover, archive...';
    FOR i IN 1..100 LOOP
        UPDATE work_items
        SET due_date = CURRENT_DATE + ((i % 30) || ' days')::interval,
            team_id = CASE WHEN i % 2 = 0 THEN v_team_alpha_id ELSE v_team_beta_id END
        WHERE id = v_story_ids[i];
    END LOOP;

    -- Flag the two highest-priority initiatives for follow-up
    UPDATE work_items SET "IsFlagged" = true WHERE id IN (v_initiative_ids[1], v_initiative_ids[2]);

    -- Give the first story a cover image (from the attachments seeded in step 11)
    IF array_length(v_attachment_ids, 1) > 0 THEN
        UPDATE work_items SET "CoverAttachmentId" = v_attachment_ids[1] WHERE id = v_story_ids[1];
    END IF;

    -- Archive the last bug as a resolved/closed-out example
    UPDATE work_items SET "IsArchived" = true, "ArchivedAt" = NOW() WHERE id = v_bug_ids[10];

    ----------------------------------------------------------------------------
    -- 15. Seed worklogs (developer time spent)
    ----------------------------------------------------------------------------
    RAISE NOTICE 'Seeding work item worklogs...';
    FOR i IN 1..20 LOOP
        v_user_idx := ((i - 1) % 5) + 1;
        INSERT INTO work_item_worklogs(id, tenant_id, work_item_id, author_membership_id, minutes_spent, work_date, description, created_at)
        VALUES (gen_random_uuid(), c_tenant_id, v_story_ids[i], v_membership_ids[v_user_idx], 30 * ((i % 8) + 1), CURRENT_DATE - (i || ' days')::interval, 'Implementation and code review time for ' || (SELECT summary FROM work_items WHERE id = v_story_ids[i]), NOW());
    END LOOP;

    ----------------------------------------------------------------------------
    -- 16. Seed work item history entries (audit trail)
    ----------------------------------------------------------------------------
    RAISE NOTICE 'Seeding work item history entries...';
    FOR i IN 1..15 LOOP
        INSERT INTO work_item_history_entries(id, tenant_id, work_item_id, changed_by_membership_id, field_name, old_value, new_value, changed_at)
        VALUES (gen_random_uuid(), c_tenant_id, v_story_ids[i], c_admin_membership_id, 'Status', 'Selected', 'InProgress', NOW() - (i || ' hours')::interval);
    END LOOP;
    FOR i IN 1..10 LOOP
        INSERT INTO work_item_history_entries(id, tenant_id, work_item_id, changed_by_membership_id, field_name, old_value, new_value, changed_at)
        VALUES (gen_random_uuid(), c_tenant_id, v_bug_ids[i], c_admin_membership_id, 'Priority', 'Medium', 'High', NOW() - (i || ' hours')::interval);
    END LOOP;

    ----------------------------------------------------------------------------
    -- 17. Seed votes on key epics and bugs
    ----------------------------------------------------------------------------
    RAISE NOTICE 'Seeding work item votes...';
    FOR i IN 1..5 LOOP
        FOR j IN 1..3 LOOP
            INSERT INTO work_item_votes(id, tenant_id, work_item_id, user_id, created_at)
            VALUES (gen_random_uuid(), c_tenant_id, v_epic_ids[i], v_user_ids[j], NOW())
            ON CONFLICT DO NOTHING;
        END LOOP;
    END LOOP;
    FOR i IN 1..5 LOOP
        INSERT INTO work_item_votes(id, tenant_id, work_item_id, user_id, created_at)
        VALUES (gen_random_uuid(), c_tenant_id, v_bug_ids[i], v_user_ids[6], NOW())
        ON CONFLICT DO NOTHING;
    END LOOP;

    ----------------------------------------------------------------------------
    -- 18. Seed workspace typography settings and project settings
    ----------------------------------------------------------------------------
    RAISE NOTICE 'Seeding workspace typography and project settings...';
    INSERT INTO workspace_typography_settings(tenant_id, left_font_family, left_font_color, left_font_size_px, middle_font_family, middle_font_color, middle_font_size_px, right_font_family, right_font_color, right_font_size_px, control_height_px, control_font_size_px, version, updated_at)
    VALUES (c_tenant_id, 'Inter', '#172B4D', 13, 'Inter', '#172B4D', 13, 'Inter', '#6B778C', 12, 32, 13, 1, NOW())
    ON CONFLICT (tenant_id) DO NOTHING;

    INSERT INTO project_settings(tenant_id, project_id, default_work_item_type, default_priority, enable_releases, enable_time_tracking, repository_url, version, updated_at)
    VALUES (c_tenant_id, c_project_id, 'Story', 'Medium', true, true, 'https://github.com/orbit-work/orbit', 1, NOW())
    ON CONFLICT (tenant_id, project_id) DO NOTHING;

    RAISE NOTICE 'Database seeding completed successfully!';
END $$;
