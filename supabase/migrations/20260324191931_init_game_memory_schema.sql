create extension if not exists pgcrypto with schema extensions;
create extension if not exists vector with schema extensions;

create or replace function public.set_updated_at()
returns trigger
language plpgsql
as $$
begin
  new.updated_at = timezone('utc', now());
  return new;
end;
$$;

create table public.player_profiles (
  id uuid primary key references auth.users (id) on delete cascade,
  player_handle text,
  current_health integer not null default 100 check (current_health >= 0),
  max_health integer not null default 100 check (max_health > 0 and current_health <= max_health),
  bio text,
  long_term_summary text,
  last_health_sync_at timestamptz,
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
  player_id uuid not null references public.player_profiles (id) on delete cascade,
  npc_key text not null references public.npc_profiles (npc_key) on delete restrict,
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
  player_id uuid not null references public.player_profiles (id) on delete cascade,
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
  player_id uuid not null references public.player_profiles (id) on delete cascade,
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

create index dialogue_sessions_player_npc_idx
  on public.dialogue_sessions (player_id, npc_key, last_message_at desc nulls last);

create index dialogue_messages_session_turn_idx
  on public.dialogue_messages (session_id, turn_index, created_at);

create index dialogue_messages_player_npc_idx
  on public.dialogue_messages (player_id, npc_key, created_at desc);

create index dialogue_memories_player_npc_idx
  on public.dialogue_memories (player_id, npc_key, importance desc, created_at desc);

create index dialogue_memories_embedding_idx
  on public.dialogue_memories
  using hnsw (embedding extensions.vector_cosine_ops);

create or replace function public.handle_new_user()
returns trigger
language plpgsql
security definer
set search_path = public
as $$
begin
  insert into public.player_profiles (
    id,
    player_handle,
    metadata
  )
  values (
    new.id,
    coalesce(new.raw_user_meta_data ->> 'player_handle', new.raw_user_meta_data ->> 'display_name'),
    coalesce(new.raw_user_meta_data, '{}'::jsonb)
  )
  on conflict (id) do nothing;

  return new;
end;
$$;

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

create or replace function public.match_dialogue_memories(
  query_player_id uuid,
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
  where dm.player_id = query_player_id
    and (query_npc_key is null or dm.npc_key = query_npc_key)
    and dm.embedding is not null
    and 1 - (dm.embedding <=> query_embedding) >= match_threshold
  order by dm.embedding <=> query_embedding, dm.importance desc, dm.created_at desc
  limit greatest(match_count, 1);
$$;

create trigger set_player_profiles_updated_at
before update on public.player_profiles
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

create trigger on_auth_user_created
after insert on auth.users
for each row execute procedure public.handle_new_user();

create trigger touch_dialogue_session_after_message
after insert on public.dialogue_messages
for each row execute function public.touch_dialogue_session_from_message();

alter table public.player_profiles enable row level security;
alter table public.npc_profiles enable row level security;
alter table public.dialogue_sessions enable row level security;
alter table public.dialogue_messages enable row level security;
alter table public.dialogue_memories enable row level security;

create policy "players_read_own_profile"
  on public.player_profiles
  for select
  to authenticated
  using ((select auth.uid()) = id);

create policy "players_insert_own_profile"
  on public.player_profiles
  for insert
  to authenticated
  with check ((select auth.uid()) = id);

create policy "players_update_own_profile"
  on public.player_profiles
  for update
  to authenticated
  using ((select auth.uid()) = id)
  with check ((select auth.uid()) = id);

create policy "authenticated_read_npc_profiles"
  on public.npc_profiles
  for select
  to authenticated
  using (true);

create policy "players_manage_own_dialogue_sessions"
  on public.dialogue_sessions
  for all
  to authenticated
  using ((select auth.uid()) = player_id)
  with check ((select auth.uid()) = player_id);

create policy "players_manage_own_dialogue_messages"
  on public.dialogue_messages
  for all
  to authenticated
  using ((select auth.uid()) = player_id)
  with check ((select auth.uid()) = player_id);

create policy "players_manage_own_dialogue_memories"
  on public.dialogue_memories
  for all
  to authenticated
  using ((select auth.uid()) = player_id)
  with check ((select auth.uid()) = player_id);
