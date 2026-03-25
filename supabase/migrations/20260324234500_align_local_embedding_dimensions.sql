drop index if exists public.dialogue_memories_embedding_idx;

alter table public.dialogue_memories
  drop column if exists embedding;

alter table public.dialogue_memories
  add column embedding extensions.vector(768);

create index if not exists dialogue_memories_embedding_idx
  on public.dialogue_memories
  using hnsw (embedding extensions.vector_cosine_ops);

drop function if exists public.match_dialogue_memories(text, text, extensions.vector, integer, double precision);

create or replace function public.match_dialogue_memories(
  query_player_key text,
  query_npc_key text,
  query_embedding extensions.vector(768),
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
set search_path = public, extensions
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

revoke execute on function public.match_dialogue_memories(text, text, extensions.vector, integer, double precision) from public, anon, authenticated;
grant execute on function public.match_dialogue_memories(text, text, extensions.vector, integer, double precision) to service_role;
