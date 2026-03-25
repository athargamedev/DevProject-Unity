create index if not exists memory_jobs_claim_idx
  on public.memory_jobs (status, job_type, requested_at asc, created_at asc)
  where status = 'queued';

create index if not exists memory_jobs_running_idx
  on public.memory_jobs (status, started_at asc)
  where status = 'running';

create or replace function public.authoritative_requeue_stale_memory_jobs(
  p_stale_after_seconds integer default 300,
  p_job_types text[] default array['summarize_session', 'summarize_turns', 'embed_memory']
)
returns jsonb
language plpgsql
set search_path = public, extensions
as $$
declare
  v_requeued_count integer := 0;
begin
  with requeued as (
    update public.memory_jobs mj
       set status = 'queued',
           started_at = null,
           completed_at = null,
           error = null,
           payload = (
             (coalesce(mj.payload, '{}'::jsonb) - 'worker_id')
             || jsonb_build_object(
               'stale_requeue', true,
               'requeued_at', timezone('utc', now())
             )
           ),
           updated_at = timezone('utc', now())
     where mj.status = 'running'
       and mj.started_at is not null
       and mj.started_at <= timezone('utc', now()) - make_interval(secs => greatest(p_stale_after_seconds, 5))
       and (p_job_types is null or mj.job_type = any(p_job_types))
    returning 1
  )
  select count(*)
    into v_requeued_count
    from requeued;

  return jsonb_build_object(
    'requeued_count', v_requeued_count,
    'stale_after_seconds', greatest(p_stale_after_seconds, 5)
  );
end;
$$;

revoke execute on function public.authoritative_requeue_stale_memory_jobs(integer, text[]) from public, anon, authenticated;
grant execute on function public.authoritative_requeue_stale_memory_jobs(integer, text[]) to service_role;

alter function public.set_updated_at() set search_path = public, extensions;
alter function public.touch_dialogue_session_from_message() set search_path = public, extensions;
alter function public.authoritative_upsert_player_profile(text, uuid, text, text, text, jsonb, jsonb) set search_path = public, extensions;
alter function public.authoritative_upsert_player_runtime_state(text, text, double precision, double precision, jsonb, jsonb, jsonb) set search_path = public, extensions;
alter function public.authoritative_upsert_npc_profile(text, text, text, text, jsonb) set search_path = public, extensions;
alter function public.authoritative_open_dialogue_session(text, text, text, text, jsonb) set search_path = public, extensions;
alter function public.authoritative_append_dialogue_message(uuid, text, text, text, text, integer, text, text, jsonb, jsonb, jsonb) set search_path = public, extensions;
alter function public.authoritative_close_dialogue_session(uuid, text, jsonb) set search_path = public, extensions;
alter function public.authoritative_enqueue_memory_job(text, text, uuid, text, jsonb) set search_path = public, extensions;
alter function public.authoritative_get_recent_dialogue_context(text, text, integer, integer) set search_path = public, extensions;
alter function public.match_dialogue_memories(text, text, extensions.vector, integer, double precision) set search_path = public, extensions;
alter function public.authoritative_claim_memory_job(text, text[]) set search_path = public, extensions;
alter function public.authoritative_update_memory_job_status(uuid, text, text, jsonb) set search_path = public, extensions;
alter function public.authoritative_get_dialogue_session_transcript(uuid, integer) set search_path = public, extensions;
alter function public.authoritative_upsert_dialogue_memory(text, text, uuid, uuid, text, text, text, smallint, jsonb, jsonb) set search_path = public, extensions;
alter function public.authoritative_match_dialogue_memories(text, text, jsonb, integer, double precision) set search_path = public, extensions;
