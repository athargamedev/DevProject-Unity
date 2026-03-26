-- ─────────────────────────────────────────────────────────────────────────────
-- Schedule memory-worker Edge Function via pg_cron + pg_net
--
-- The memory-worker Edge Function summarises pending dialogue transcript jobs
-- and writes consolidated NpcDialogueMemory entries. Running it on a schedule
-- ensures NPC memories stay current even without a dedicated Unity game server.
--
-- Prerequisites:
--   • pg_cron extension must be enabled (Supabase cloud: enabled by default)
--   • pg_net extension must be enabled  (Supabase cloud: enabled by default)
--   • The memory-worker Edge Function must be deployed:
--       supabase functions deploy memory-worker
--
-- Usage:
--   Run this migration after deploying the memory-worker function.
--   Replace <YOUR_PROJECT_REF> with your Supabase project reference ID.
--   The service-role key is read from vault.secrets so it is never hardcoded here.
-- ─────────────────────────────────────────────────────────────────────────────

-- Enable required extensions if not already present.
CREATE EXTENSION IF NOT EXISTS pg_cron;
CREATE EXTENSION IF NOT EXISTS pg_net;

-- ─── Helper: invoke the memory-worker Edge Function ──────────────────────────
-- This function is called by the cron job. It reads the service-role key from
-- vault.secrets to avoid hardcoding credentials in migration SQL.
CREATE OR REPLACE FUNCTION internal.trigger_memory_worker()
RETURNS void
LANGUAGE plpgsql
SECURITY DEFINER
AS $$
DECLARE
  v_project_url   text;
  v_service_key   text;
  v_function_url  text;
BEGIN
  -- Read project URL and service role key from Supabase vault.
  -- Set these once with:
  --   SELECT vault.create_secret('supabase_project_url', 'https://<ref>.supabase.co');
  --   SELECT vault.create_secret('supabase_service_role_key', '<key>');
  SELECT decrypted_secret INTO v_project_url
    FROM vault.decrypted_secrets
    WHERE name = 'supabase_project_url'
    LIMIT 1;

  SELECT decrypted_secret INTO v_service_key
    FROM vault.decrypted_secrets
    WHERE name = 'supabase_service_role_key'
    LIMIT 1;

  IF v_project_url IS NULL OR v_service_key IS NULL THEN
    RAISE NOTICE 'trigger_memory_worker: vault secrets not configured — skipping.';
    RETURN;
  END IF;

  v_function_url := v_project_url || '/functions/v1/memory-worker';

  PERFORM net.http_post(
    url     := v_function_url,
    headers := jsonb_build_object(
      'Content-Type',  'application/json',
      'Authorization', 'Bearer ' || v_service_key
    ),
    body    := '{}'::jsonb
  );
END;
$$;

-- Grant execute only to postgres (cron runs as postgres).
REVOKE ALL ON FUNCTION internal.trigger_memory_worker() FROM PUBLIC;
GRANT  EXECUTE ON FUNCTION internal.trigger_memory_worker() TO postgres;

-- ─── Cron job: run memory-worker every 2 minutes ─────────────────────────────
-- Adjust the schedule as needed:
--   '* * * * *'   — every minute  (aggressive, good for active sessions)
--   '*/2 * * * *' — every 2 min   (default, balanced)
--   '*/5 * * * *' — every 5 min   (quiet servers / cost-sensitive)
SELECT cron.schedule(
  'dialogue-memory-worker',          -- job name (unique)
  '*/2 * * * *',                     -- every 2 minutes
  $$ SELECT internal.trigger_memory_worker(); $$
);

-- ─── Comment ─────────────────────────────────────────────────────────────────
COMMENT ON FUNCTION internal.trigger_memory_worker() IS
  'Invokes the memory-worker Supabase Edge Function to process pending '
  'dialogue memory consolidation jobs. Called by pg_cron every 2 minutes.';
