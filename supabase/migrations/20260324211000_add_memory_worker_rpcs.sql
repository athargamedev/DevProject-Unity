create or replace function public.authoritative_claim_memory_job(
  p_worker_id text default null,
  p_job_types text[] default array['summarize_session', 'summarize_turns', 'embed_memory']
)
returns jsonb
language plpgsql
as $$
declare
  v_job public.memory_jobs;
  v_payload_patch jsonb;
begin
  if p_worker_id is null or btrim(p_worker_id) = '' then
    v_payload_patch := '{}'::jsonb;
  else
    v_payload_patch := jsonb_build_object('worker_id', btrim(p_worker_id));
  end if;

  with next_job as (
    select mj.id
    from public.memory_jobs mj
    where mj.status = 'queued'
      and (p_job_types is null or mj.job_type = any(p_job_types))
    order by mj.requested_at asc, mj.created_at asc
    for update skip locked
    limit 1
  )
  update public.memory_jobs mj
     set status = 'running',
         started_at = coalesce(mj.started_at, timezone('utc', now())),
         attempt_count = mj.attempt_count + 1,
         payload = coalesce(mj.payload, '{}'::jsonb) || v_payload_patch,
         updated_at = timezone('utc', now())
   where mj.id in (select id from next_job)
   returning * into v_job;

  if v_job.id is null then
    return null;
  end if;

  return jsonb_build_object(
    'job_id', v_job.id,
    'player_key', v_job.player_key,
    'npc_key', v_job.npc_key,
    'session_id', v_job.session_id,
    'job_type', v_job.job_type,
    'status', v_job.status,
    'attempt_count', v_job.attempt_count,
    'payload', v_job.payload,
    'requested_at', v_job.requested_at,
    'started_at', v_job.started_at
  );
end;
$$;

create or replace function public.authoritative_update_memory_job_status(
  p_job_id uuid,
  p_status text,
  p_error text default null,
  p_payload_patch jsonb default null
)
returns jsonb
language plpgsql
as $$
declare
  v_job public.memory_jobs;
begin
  if p_job_id is null then
    raise exception 'job_id is required';
  end if;

  if p_status is null or p_status not in ('queued', 'running', 'completed', 'failed', 'cancelled') then
    raise exception 'invalid memory job status';
  end if;

  update public.memory_jobs
     set status = p_status,
         started_at = case
           when p_status = 'running' then coalesce(started_at, timezone('utc', now()))
           else started_at
         end,
         completed_at = case
           when p_status in ('completed', 'failed', 'cancelled') then timezone('utc', now())
           else null
         end,
         error = case
           when p_status = 'completed' then null
           else nullif(p_error, '')
         end,
         payload = coalesce(payload, '{}'::jsonb) || coalesce(p_payload_patch, '{}'::jsonb),
         updated_at = timezone('utc', now())
   where id = p_job_id
   returning * into v_job;

  if v_job.id is null then
    raise exception 'memory job not found';
  end if;

  return jsonb_build_object(
    'job_id', v_job.id,
    'status', v_job.status,
    'error', v_job.error,
    'completed_at', v_job.completed_at
  );
end;
$$;

create or replace function public.authoritative_get_dialogue_session_transcript(
  p_session_id uuid,
  p_message_limit integer default 24
)
returns jsonb
language sql
stable
as $$
  with target_session as (
    select ds.*
    from public.dialogue_sessions ds
    where ds.id = p_session_id
  ),
  limited_messages as (
    select dm.*
    from public.dialogue_messages dm
    where dm.session_id = p_session_id
    order by dm.created_at desc
    limit greatest(p_message_limit, 1)
  )
  select jsonb_build_object(
    'session_id', p_session_id,
    'player_key', (select player_key from target_session),
    'npc_key', (select npc_key from target_session),
    'scene_name', (select scene_name from target_session),
    'messages', coalesce((
      select jsonb_agg(
        jsonb_build_object(
          'message_id', msg.id,
          'speaker_role', msg.speaker_role,
          'speaker_key', msg.speaker_key,
          'content', msg.content,
          'turn_index', msg.turn_index,
          'created_at', msg.created_at
        )
        order by msg.created_at asc
      )
      from limited_messages msg
    ), '[]'::jsonb)
  );
$$;

create or replace function public.authoritative_upsert_dialogue_memory(
  p_player_key text,
  p_npc_key text,
  p_session_id uuid default null,
  p_source_message_id uuid default null,
  p_memory_scope text default 'episodic',
  p_summary text default null,
  p_memory_text text default null,
  p_importance smallint default 5,
  p_embedding extensions.vector(1536) default null,
  p_metadata jsonb default null
)
returns jsonb
language plpgsql
as $$
declare
  v_memory public.dialogue_memories;
begin
  if p_player_key is null or btrim(p_player_key) = '' then
    raise exception 'player_key is required';
  end if;

  if p_npc_key is null or btrim(p_npc_key) = '' then
    raise exception 'npc_key is required';
  end if;

  if p_summary is null or btrim(p_summary) = '' then
    raise exception 'summary is required';
  end if;

  if p_memory_text is null or btrim(p_memory_text) = '' then
    raise exception 'memory_text is required';
  end if;

  insert into public.dialogue_memories (
    player_key,
    npc_key,
    session_id,
    source_message_id,
    memory_scope,
    summary,
    memory_text,
    importance,
    embedding,
    metadata
  )
  values (
    btrim(p_player_key),
    btrim(p_npc_key),
    p_session_id,
    p_source_message_id,
    case
      when p_memory_scope in ('episodic', 'semantic', 'profile') then p_memory_scope
      else 'episodic'
    end,
    btrim(p_summary),
    btrim(p_memory_text),
    greatest(1, least(coalesce(p_importance, 5), 10)),
    p_embedding,
    coalesce(p_metadata, '{}'::jsonb)
  )
  returning * into v_memory;

  return jsonb_build_object(
    'memory_id', v_memory.id,
    'player_key', v_memory.player_key,
    'npc_key', v_memory.npc_key,
    'memory_scope', v_memory.memory_scope,
    'importance', v_memory.importance
  );
end;
$$;

revoke execute on function public.authoritative_claim_memory_job(text, text[]) from public, anon, authenticated;
revoke execute on function public.authoritative_update_memory_job_status(uuid, text, text, jsonb) from public, anon, authenticated;
revoke execute on function public.authoritative_get_dialogue_session_transcript(uuid, integer) from public, anon, authenticated;
revoke execute on function public.authoritative_upsert_dialogue_memory(text, text, uuid, uuid, text, text, text, smallint, extensions.vector, jsonb) from public, anon, authenticated;

grant execute on function public.authoritative_claim_memory_job(text, text[]) to service_role;
grant execute on function public.authoritative_update_memory_job_status(uuid, text, text, jsonb) to service_role;
grant execute on function public.authoritative_get_dialogue_session_transcript(uuid, integer) to service_role;
grant execute on function public.authoritative_upsert_dialogue_memory(text, text, uuid, uuid, text, text, text, smallint, extensions.vector, jsonb) to service_role;
