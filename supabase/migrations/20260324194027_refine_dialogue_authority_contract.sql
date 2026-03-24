drop trigger if exists on_auth_user_created on auth.users;
drop trigger if exists touch_dialogue_session_after_message on public.dialogue_messages;

drop function if exists public.handle_new_user() cascade;
drop function if exists public.touch_dialogue_session_from_message() cascade;
drop function if exists public.match_dialogue_memories(uuid, text, extensions.vector, integer, double precision) cascade;
drop function if exists public.match_dialogue_memories(text, text, extensions.vector, integer, double precision) cascade;

drop table if exists public.dialogue_memories cascade;
drop table if exists public.dialogue_messages cascade;
drop table if exists public.dialogue_sessions cascade;
drop table if exists public.memory_jobs cascade;
drop table if exists public.npc_profiles cascade;
drop table if exists public.player_profiles cascade;
drop table if exists public.player_runtime_state cascade;

create table public.player_profiles (
  player_key text primary key,
  auth_user_id uuid unique references auth.users (id) on delete set null,
  player_handle text,
  bio text,
  long_term_summary text,
  customization_json jsonb not null default '{}'::jsonb,
  metadata jsonb not null default '{}'::jsonb,
  created_at timestamptz not null default timezone('utc', now()),
  updated_at timestamptz not null default timezone('utc', now())
);

create table public.player_runtime_state (
  player_key text primary key references public.player_profiles (player_key) on delete cascade,
  scene_name text,
  current_health double precision not null default 100 check (current_health >= 0),
  max_health double precision not null default 100 check (max_health > 0 and current_health <= max_health),
  position jsonb not null default '{}'::jsonb,
  status_flags jsonb not null default '{}'::jsonb,
  last_synced_at timestamptz not null default timezone('utc', now()),
  metadata jsonb not null default '{}'::jsonb,
  created_at timestamptz not null default timezone('utc', now()),
  updated_at timestamptz not null default timezone('utc', now())
);

create table public.npc_profiles (
  npc_key text primary key,
  display_name text not null,
  bio text,
  profile_asset_key text,
  metadata jsonb not null default '{}'::jsonb,
  created_at timestamptz not null default timezone('utc', now()),
  updated_at timestamptz not null default timezone('utc', now())
);

create table public.dialogue_sessions (
  id uuid primary key default extensions.gen_random_uuid(),
  player_key text not null references public.player_profiles (player_key) on delete cascade,
  npc_key text not null references public.npc_profiles (npc_key) on delete restrict,
  conversation_key text,
  scene_name text,
  started_at timestamptz not null default timezone('utc', now()),
  ended_at timestamptz,
  last_message_at timestamptz,
  summary text,
  metadata jsonb not null default '{}'::jsonb,
  created_at timestamptz not null default timezone('utc', now()),
  updated_at timestamptz not null default timezone('utc', now())
);

create table public.dialogue_messages (
  id uuid primary key default extensions.gen_random_uuid(),
  session_id uuid not null references public.dialogue_sessions (id) on delete cascade,
  player_key text not null references public.player_profiles (player_key) on delete cascade,
  npc_key text not null references public.npc_profiles (npc_key) on delete restrict,
  speaker_role text not null check (speaker_role in ('player', 'npc', 'system')),
  speaker_key text,
  turn_index integer not null default 0,
  content text not null,
  emotion_hint text,
  structured_actions jsonb not null default '[]'::jsonb,
  extracted_facts jsonb not null default '[]'::jsonb,
  metadata jsonb not null default '{}'::jsonb,
  created_at timestamptz not null default timezone('utc', now())
);

create table public.dialogue_memories (
  id uuid primary key default extensions.gen_random_uuid(),
  player_key text not null references public.player_profiles (player_key) on delete cascade,
  npc_key text not null references public.npc_profiles (npc_key) on delete restrict,
  session_id uuid references public.dialogue_sessions (id) on delete set null,
  source_message_id uuid references public.dialogue_messages (id) on delete set null,
  memory_scope text not null default 'episodic' check (memory_scope in ('episodic', 'semantic', 'profile')),
  summary text not null,
  memory_text text not null,
  importance smallint not null default 5 check (importance between 1 and 10),
  recall_count integer not null default 0 check (recall_count >= 0),
  last_recalled_at timestamptz,
  embedding extensions.vector(1536),
  metadata jsonb not null default '{}'::jsonb,
  created_at timestamptz not null default timezone('utc', now()),
  updated_at timestamptz not null default timezone('utc', now())
);

create table public.memory_jobs (
  id uuid primary key default extensions.gen_random_uuid(),
  player_key text not null references public.player_profiles (player_key) on delete cascade,
  npc_key text not null references public.npc_profiles (npc_key) on delete restrict,
  session_id uuid references public.dialogue_sessions (id) on delete cascade,
  job_type text not null default 'summarize_session' check (job_type in ('summarize_session', 'summarize_turns', 'embed_memory')),
  status text not null default 'queued' check (status in ('queued', 'running', 'completed', 'failed', 'cancelled')),
  requested_at timestamptz not null default timezone('utc', now()),
  started_at timestamptz,
  completed_at timestamptz,
  attempt_count integer not null default 0 check (attempt_count >= 0),
  payload jsonb not null default '{}'::jsonb,
  error text,
  created_at timestamptz not null default timezone('utc', now()),
  updated_at timestamptz not null default timezone('utc', now())
);

create index player_runtime_state_last_synced_idx
  on public.player_runtime_state (last_synced_at desc);

create index dialogue_sessions_player_npc_idx
  on public.dialogue_sessions (player_key, npc_key, last_message_at desc nulls last);

create unique index dialogue_sessions_active_conversation_idx
  on public.dialogue_sessions (player_key, npc_key, conversation_key)
  where ended_at is null and conversation_key is not null;

create index dialogue_messages_session_turn_idx
  on public.dialogue_messages (session_id, turn_index, created_at);

create index dialogue_messages_player_npc_idx
  on public.dialogue_messages (player_key, npc_key, created_at desc);

create index dialogue_memories_player_npc_idx
  on public.dialogue_memories (player_key, npc_key, importance desc, created_at desc);

create index dialogue_memories_embedding_idx
  on public.dialogue_memories
  using hnsw (embedding extensions.vector_cosine_ops);

create index memory_jobs_queue_idx
  on public.memory_jobs (status, requested_at asc);

create or replace function public.touch_dialogue_session_from_message()
returns trigger
language plpgsql
as $$
begin
  update public.dialogue_sessions
     set last_message_at = new.created_at,
         updated_at = timezone('utc', now())
   where id = new.session_id;

  return new;
end;
$$;

create or replace function public.authoritative_upsert_player_profile(
  p_player_key text,
  p_auth_user_id uuid default null,
  p_player_handle text default null,
  p_bio text default null,
  p_long_term_summary text default null,
  p_customization_json jsonb default null,
  p_metadata jsonb default null
)
returns jsonb
language plpgsql
as $$
declare
  v_profile public.player_profiles;
begin
  if p_player_key is null or btrim(p_player_key) = '' then
    raise exception 'player_key is required';
  end if;

  insert into public.player_profiles (
    player_key,
    auth_user_id,
    player_handle,
    bio,
    long_term_summary,
    customization_json,
    metadata
  )
  values (
    btrim(p_player_key),
    p_auth_user_id,
    nullif(btrim(p_player_handle), ''),
    p_bio,
    p_long_term_summary,
    coalesce(p_customization_json, '{}'::jsonb),
    coalesce(p_metadata, '{}'::jsonb)
  )
  on conflict (player_key) do update
     set auth_user_id = coalesce(excluded.auth_user_id, public.player_profiles.auth_user_id),
         player_handle = coalesce(excluded.player_handle, public.player_profiles.player_handle),
         bio = coalesce(excluded.bio, public.player_profiles.bio),
         long_term_summary = coalesce(excluded.long_term_summary, public.player_profiles.long_term_summary),
         customization_json = coalesce(excluded.customization_json, public.player_profiles.customization_json),
         metadata = coalesce(public.player_profiles.metadata, '{}'::jsonb) || coalesce(excluded.metadata, '{}'::jsonb),
         updated_at = timezone('utc', now())
  returning * into v_profile;

  insert into public.player_runtime_state (player_key)
  values (v_profile.player_key)
  on conflict (player_key) do nothing;

  return jsonb_build_object(
    'player_key', v_profile.player_key,
    'player_handle', v_profile.player_handle,
    'auth_user_id', v_profile.auth_user_id
  );
end;
$$;

create or replace function public.authoritative_upsert_player_runtime_state(
  p_player_key text,
  p_scene_name text default null,
  p_current_health double precision default null,
  p_max_health double precision default null,
  p_position jsonb default null,
  p_status_flags jsonb default null,
  p_metadata jsonb default null
)
returns jsonb
language plpgsql
as $$
declare
  v_state public.player_runtime_state;
begin
  if p_player_key is null or btrim(p_player_key) = '' then
    raise exception 'player_key is required';
  end if;

  insert into public.player_profiles (player_key)
  values (btrim(p_player_key))
  on conflict (player_key) do nothing;

  insert into public.player_runtime_state (
    player_key,
    scene_name,
    current_health,
    max_health,
    position,
    status_flags,
    last_synced_at,
    metadata
  )
  values (
    btrim(p_player_key),
    nullif(btrim(p_scene_name), ''),
    coalesce(p_current_health, 100),
    coalesce(p_max_health, greatest(coalesce(p_current_health, 100), 1)),
    coalesce(p_position, '{}'::jsonb),
    coalesce(p_status_flags, '{}'::jsonb),
    timezone('utc', now()),
    coalesce(p_metadata, '{}'::jsonb)
  )
  on conflict (player_key) do update
     set scene_name = coalesce(excluded.scene_name, public.player_runtime_state.scene_name),
         current_health = coalesce(excluded.current_health, public.player_runtime_state.current_health),
         max_health = coalesce(excluded.max_health, public.player_runtime_state.max_health),
         position = coalesce(public.player_runtime_state.position, '{}'::jsonb) || coalesce(excluded.position, '{}'::jsonb),
         status_flags = coalesce(public.player_runtime_state.status_flags, '{}'::jsonb) || coalesce(excluded.status_flags, '{}'::jsonb),
         last_synced_at = timezone('utc', now()),
         metadata = coalesce(public.player_runtime_state.metadata, '{}'::jsonb) || coalesce(excluded.metadata, '{}'::jsonb),
         updated_at = timezone('utc', now())
  returning * into v_state;

  return jsonb_build_object(
    'player_key', v_state.player_key,
    'current_health', v_state.current_health,
    'max_health', v_state.max_health,
    'last_synced_at', v_state.last_synced_at
  );
end;
$$;

create or replace function public.authoritative_upsert_npc_profile(
  p_npc_key text,
  p_display_name text,
  p_bio text default null,
  p_profile_asset_key text default null,
  p_metadata jsonb default null
)
returns jsonb
language plpgsql
as $$
declare
  v_npc public.npc_profiles;
begin
  if p_npc_key is null or btrim(p_npc_key) = '' then
    raise exception 'npc_key is required';
  end if;

  if p_display_name is null or btrim(p_display_name) = '' then
    raise exception 'display_name is required';
  end if;

  insert into public.npc_profiles (
    npc_key,
    display_name,
    bio,
    profile_asset_key,
    metadata
  )
  values (
    btrim(p_npc_key),
    btrim(p_display_name),
    p_bio,
    nullif(btrim(p_profile_asset_key), ''),
    coalesce(p_metadata, '{}'::jsonb)
  )
  on conflict (npc_key) do update
     set display_name = excluded.display_name,
         bio = coalesce(excluded.bio, public.npc_profiles.bio),
         profile_asset_key = coalesce(excluded.profile_asset_key, public.npc_profiles.profile_asset_key),
         metadata = coalesce(public.npc_profiles.metadata, '{}'::jsonb) || coalesce(excluded.metadata, '{}'::jsonb),
         updated_at = timezone('utc', now())
  returning * into v_npc;

  return jsonb_build_object(
    'npc_key', v_npc.npc_key,
    'display_name', v_npc.display_name
  );
end;
$$;

create or replace function public.authoritative_open_dialogue_session(
  p_player_key text,
  p_npc_key text,
  p_conversation_key text default null,
  p_scene_name text default null,
  p_metadata jsonb default null
)
returns jsonb
language plpgsql
as $$
declare
  v_existing public.dialogue_sessions;
  v_session public.dialogue_sessions;
begin
  if p_player_key is null or btrim(p_player_key) = '' then
    raise exception 'player_key is required';
  end if;

  if p_npc_key is null or btrim(p_npc_key) = '' then
    raise exception 'npc_key is required';
  end if;

  if p_conversation_key is not null and btrim(p_conversation_key) <> '' then
    select *
      into v_existing
      from public.dialogue_sessions
     where player_key = btrim(p_player_key)
       and npc_key = btrim(p_npc_key)
       and conversation_key = btrim(p_conversation_key)
       and ended_at is null
     order by created_at desc
     limit 1;
  end if;

  if v_existing.id is not null then
    update public.dialogue_sessions
       set scene_name = coalesce(nullif(btrim(p_scene_name), ''), scene_name),
           metadata = coalesce(metadata, '{}'::jsonb) || coalesce(p_metadata, '{}'::jsonb),
           updated_at = timezone('utc', now())
     where id = v_existing.id
     returning * into v_session;
  else
    insert into public.dialogue_sessions (
      player_key,
      npc_key,
      conversation_key,
      scene_name,
      metadata
    )
    values (
      btrim(p_player_key),
      btrim(p_npc_key),
      nullif(btrim(p_conversation_key), ''),
      nullif(btrim(p_scene_name), ''),
      coalesce(p_metadata, '{}'::jsonb)
    )
    returning * into v_session;
  end if;

  return jsonb_build_object(
    'session_id', v_session.id,
    'conversation_key', v_session.conversation_key,
    'created_at', v_session.created_at,
    'updated_at', v_session.updated_at
  );
end;
$$;

create or replace function public.authoritative_append_dialogue_message(
  p_session_id uuid,
  p_player_key text,
  p_npc_key text,
  p_speaker_role text,
  p_speaker_key text default null,
  p_turn_index integer default null,
  p_content text default null,
  p_emotion_hint text default null,
  p_structured_actions jsonb default null,
  p_extracted_facts jsonb default null,
  p_metadata jsonb default null
)
returns jsonb
language plpgsql
as $$
declare
  v_turn_index integer;
  v_message public.dialogue_messages;
begin
  if p_session_id is null then
    raise exception 'session_id is required';
  end if;

  if p_player_key is null or btrim(p_player_key) = '' then
    raise exception 'player_key is required';
  end if;

  if p_npc_key is null or btrim(p_npc_key) = '' then
    raise exception 'npc_key is required';
  end if;

  if p_speaker_role is null or p_speaker_role not in ('player', 'npc', 'system') then
    raise exception 'speaker_role must be player, npc, or system';
  end if;

  if p_content is null or btrim(p_content) = '' then
    raise exception 'content is required';
  end if;

  if p_turn_index is null then
    select coalesce(max(turn_index), -1) + 1
      into v_turn_index
      from public.dialogue_messages
     where session_id = p_session_id;
  else
    v_turn_index = greatest(p_turn_index, 0);
  end if;

  insert into public.dialogue_messages (
    session_id,
    player_key,
    npc_key,
    speaker_role,
    speaker_key,
    turn_index,
    content,
    emotion_hint,
    structured_actions,
    extracted_facts,
    metadata
  )
  values (
    p_session_id,
    btrim(p_player_key),
    btrim(p_npc_key),
    p_speaker_role,
    nullif(btrim(p_speaker_key), ''),
    v_turn_index,
    btrim(p_content),
    p_emotion_hint,
    coalesce(p_structured_actions, '[]'::jsonb),
    coalesce(p_extracted_facts, '[]'::jsonb),
    coalesce(p_metadata, '{}'::jsonb)
  )
  returning * into v_message;

  return jsonb_build_object(
    'message_id', v_message.id,
    'session_id', v_message.session_id,
    'turn_index', v_message.turn_index,
    'created_at', v_message.created_at
  );
end;
$$;

create or replace function public.authoritative_close_dialogue_session(
  p_session_id uuid,
  p_summary text default null,
  p_metadata jsonb default null
)
returns jsonb
language plpgsql
as $$
declare
  v_session public.dialogue_sessions;
begin
  if p_session_id is null then
    raise exception 'session_id is required';
  end if;

  update public.dialogue_sessions
     set ended_at = coalesce(ended_at, timezone('utc', now())),
         summary = coalesce(p_summary, summary),
         metadata = coalesce(metadata, '{}'::jsonb) || coalesce(p_metadata, '{}'::jsonb),
         updated_at = timezone('utc', now())
   where id = p_session_id
   returning * into v_session;

  return jsonb_build_object(
    'session_id', v_session.id,
    'ended_at', v_session.ended_at,
    'summary', v_session.summary
  );
end;
$$;

create or replace function public.authoritative_enqueue_memory_job(
  p_player_key text,
  p_npc_key text,
  p_session_id uuid default null,
  p_job_type text default 'summarize_session',
  p_payload jsonb default null
)
returns jsonb
language plpgsql
as $$
declare
  v_job public.memory_jobs;
begin
  if p_player_key is null or btrim(p_player_key) = '' then
    raise exception 'player_key is required';
  end if;

  if p_npc_key is null or btrim(p_npc_key) = '' then
    raise exception 'npc_key is required';
  end if;

  insert into public.memory_jobs (
    player_key,
    npc_key,
    session_id,
    job_type,
    payload
  )
  values (
    btrim(p_player_key),
    btrim(p_npc_key),
    p_session_id,
    coalesce(p_job_type, 'summarize_session'),
    coalesce(p_payload, '{}'::jsonb)
  )
  returning * into v_job;

  return jsonb_build_object(
    'job_id', v_job.id,
    'status', v_job.status,
    'job_type', v_job.job_type
  );
end;
$$;

create or replace function public.authoritative_get_recent_dialogue_context(
  p_player_key text,
  p_npc_key text,
  p_message_limit integer default 6,
  p_memory_limit integer default 6
)
returns jsonb
language sql
stable
as $$
  with recent_messages as (
    select jsonb_agg(
      jsonb_build_object(
        'message_id', dm.id,
        'speaker_role', dm.speaker_role,
        'speaker_key', dm.speaker_key,
        'content', dm.content,
        'turn_index', dm.turn_index,
        'created_at', dm.created_at
      )
      order by dm.created_at asc
    ) as payload
    from (
      select dm.*
      from public.dialogue_messages dm
      where dm.player_key = p_player_key
        and dm.npc_key = p_npc_key
        and greatest(p_message_limit, 0) > 0
      order by dm.created_at desc
      limit greatest(p_message_limit, 0)
    ) dm
  ),
  recent_memories as (
    select jsonb_agg(
      jsonb_build_object(
        'memory_id', mem.id,
        'memory_scope', mem.memory_scope,
        'summary', mem.summary,
        'memory_text', mem.memory_text,
        'importance', mem.importance,
        'last_recalled_at', mem.last_recalled_at,
        'created_at', mem.created_at
      )
      order by mem.importance desc, mem.created_at desc
    ) as payload
    from (
      select mem.*
      from public.dialogue_memories mem
      where mem.player_key = p_player_key
        and mem.npc_key = p_npc_key
        and greatest(p_memory_limit, 0) > 0
      order by mem.importance desc, mem.created_at desc
      limit greatest(p_memory_limit, 0)
    ) mem
  )
  select jsonb_build_object(
    'player_key', p_player_key,
    'npc_key', p_npc_key,
    'recent_messages', coalesce((select payload from recent_messages), '[]'::jsonb),
    'recent_memories', coalesce((select payload from recent_memories), '[]'::jsonb)
  );
$$;

create or replace function public.match_dialogue_memories(
  query_player_key text,
  query_npc_key text,
  query_embedding extensions.vector(1536),
  match_count integer default 8,
  match_threshold double precision default 0.7
)
returns table (
  memory_id uuid,
  npc_key text,
  memory_scope text,
  summary text,
  memory_text text,
  importance smallint,
  similarity double precision,
  metadata jsonb
)
language sql
stable
as $$
  select
    dm.id as memory_id,
    dm.npc_key,
    dm.memory_scope,
    dm.summary,
    dm.memory_text,
    dm.importance,
    1 - (dm.embedding <=> query_embedding) as similarity,
    dm.metadata
  from public.dialogue_memories dm
  where dm.player_key = query_player_key
    and (query_npc_key is null or dm.npc_key = query_npc_key)
    and dm.embedding is not null
    and 1 - (dm.embedding <=> query_embedding) >= match_threshold
  order by dm.embedding <=> query_embedding, dm.importance desc, dm.created_at desc
  limit greatest(match_count, 1);
$$;

create trigger set_player_profiles_updated_at
before update on public.player_profiles
for each row execute function public.set_updated_at();

create trigger set_player_runtime_state_updated_at
before update on public.player_runtime_state
for each row execute function public.set_updated_at();

create trigger set_npc_profiles_updated_at
before update on public.npc_profiles
for each row execute function public.set_updated_at();

create trigger set_dialogue_sessions_updated_at
before update on public.dialogue_sessions
for each row execute function public.set_updated_at();

create trigger set_dialogue_memories_updated_at
before update on public.dialogue_memories
for each row execute function public.set_updated_at();

create trigger set_memory_jobs_updated_at
before update on public.memory_jobs
for each row execute function public.set_updated_at();

create trigger touch_dialogue_session_after_message
after insert on public.dialogue_messages
for each row execute function public.touch_dialogue_session_from_message();

alter table public.player_profiles enable row level security;
alter table public.player_runtime_state enable row level security;
alter table public.npc_profiles enable row level security;
alter table public.dialogue_sessions enable row level security;
alter table public.dialogue_messages enable row level security;
alter table public.dialogue_memories enable row level security;
alter table public.memory_jobs enable row level security;

create policy "players_read_own_profile"
  on public.player_profiles
  for select
  to authenticated
  using (auth_user_id = auth.uid());

create policy "players_insert_own_profile"
  on public.player_profiles
  for insert
  to authenticated
  with check (auth_user_id = auth.uid());

create policy "players_update_own_profile"
  on public.player_profiles
  for update
  to authenticated
  using (auth_user_id = auth.uid())
  with check (auth_user_id = auth.uid());

create policy "players_read_own_runtime_state"
  on public.player_runtime_state
  for select
  to authenticated
  using (
    exists (
      select 1
      from public.player_profiles p
      where p.player_key = public.player_runtime_state.player_key
        and p.auth_user_id = auth.uid()
    )
  );

create policy "authenticated_read_npc_profiles"
  on public.npc_profiles
  for select
  to authenticated
  using (true);

create policy "players_read_own_dialogue_sessions"
  on public.dialogue_sessions
  for select
  to authenticated
  using (
    exists (
      select 1
      from public.player_profiles p
      where p.player_key = public.dialogue_sessions.player_key
        and p.auth_user_id = auth.uid()
    )
  );

create policy "players_read_own_dialogue_messages"
  on public.dialogue_messages
  for select
  to authenticated
  using (
    exists (
      select 1
      from public.player_profiles p
      where p.player_key = public.dialogue_messages.player_key
        and p.auth_user_id = auth.uid()
    )
  );

create policy "players_read_own_dialogue_memories"
  on public.dialogue_memories
  for select
  to authenticated
  using (
    exists (
      select 1
      from public.player_profiles p
      where p.player_key = public.dialogue_memories.player_key
        and p.auth_user_id = auth.uid()
    )
  );

create policy "players_read_own_memory_jobs"
  on public.memory_jobs
  for select
  to authenticated
  using (
    exists (
      select 1
      from public.player_profiles p
      where p.player_key = public.memory_jobs.player_key
        and p.auth_user_id = auth.uid()
    )
  );

revoke execute on function public.authoritative_upsert_player_profile(text, uuid, text, text, text, jsonb, jsonb) from public, anon, authenticated;
revoke execute on function public.authoritative_upsert_player_runtime_state(text, text, double precision, double precision, jsonb, jsonb, jsonb) from public, anon, authenticated;
revoke execute on function public.authoritative_upsert_npc_profile(text, text, text, text, jsonb) from public, anon, authenticated;
revoke execute on function public.authoritative_open_dialogue_session(text, text, text, text, jsonb) from public, anon, authenticated;
revoke execute on function public.authoritative_append_dialogue_message(uuid, text, text, text, text, integer, text, text, jsonb, jsonb, jsonb) from public, anon, authenticated;
revoke execute on function public.authoritative_close_dialogue_session(uuid, text, jsonb) from public, anon, authenticated;
revoke execute on function public.authoritative_enqueue_memory_job(text, text, uuid, text, jsonb) from public, anon, authenticated;
revoke execute on function public.authoritative_get_recent_dialogue_context(text, text, integer, integer) from public, anon, authenticated;
revoke execute on function public.match_dialogue_memories(text, text, extensions.vector, integer, double precision) from public, anon, authenticated;

grant execute on function public.authoritative_upsert_player_profile(text, uuid, text, text, text, jsonb, jsonb) to service_role;
grant execute on function public.authoritative_upsert_player_runtime_state(text, text, double precision, double precision, jsonb, jsonb, jsonb) to service_role;
grant execute on function public.authoritative_upsert_npc_profile(text, text, text, text, jsonb) to service_role;
grant execute on function public.authoritative_open_dialogue_session(text, text, text, text, jsonb) to service_role;
grant execute on function public.authoritative_append_dialogue_message(uuid, text, text, text, text, integer, text, text, jsonb, jsonb, jsonb) to service_role;
grant execute on function public.authoritative_close_dialogue_session(uuid, text, jsonb) to service_role;
grant execute on function public.authoritative_enqueue_memory_job(text, text, uuid, text, jsonb) to service_role;
grant execute on function public.authoritative_get_recent_dialogue_context(text, text, integer, integer) to service_role;
grant execute on function public.match_dialogue_memories(text, text, extensions.vector, integer, double precision) to service_role;
