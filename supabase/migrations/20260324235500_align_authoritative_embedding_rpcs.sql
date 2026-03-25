create or replace function public.authoritative_upsert_dialogue_memory(
  p_player_key text,
  p_npc_key text,
  p_session_id uuid default null,
  p_source_message_id uuid default null,
  p_memory_scope text default 'episodic',
  p_summary text default null,
  p_memory_text text default null,
  p_importance smallint default 5,
  p_embedding jsonb default null,
  p_metadata jsonb default null
)
returns jsonb
language plpgsql
set search_path = public, extensions
as $$
declare
  v_memory public.dialogue_memories;
  v_embedding extensions.vector(768);
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

  if p_embedding is not null then
    v_embedding := p_embedding::text::extensions.vector(768);
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
    v_embedding,
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

create or replace function public.authoritative_match_dialogue_memories(
  p_player_key text,
  p_npc_key text default null,
  p_query_embedding jsonb default null,
  p_match_count integer default 4,
  p_match_threshold double precision default 0.72
)
returns jsonb
language plpgsql
set search_path = public, extensions
as $$
declare
  v_embedding extensions.vector(768);
  v_match_ids uuid[];
  v_result jsonb;
begin
  if p_player_key is null or btrim(p_player_key) = '' then
    raise exception 'player_key is required';
  end if;

  if p_query_embedding is null then
    return jsonb_build_object('matches', '[]'::jsonb);
  end if;

  v_embedding := p_query_embedding::text::extensions.vector(768);

  with matches as (
    select
      dm.id,
      dm.npc_key,
      dm.memory_scope,
      dm.summary,
      dm.memory_text,
      dm.importance,
      dm.metadata,
      1 - (dm.embedding <=> v_embedding) as similarity
    from public.dialogue_memories dm
    where dm.player_key = btrim(p_player_key)
      and (p_npc_key is null or btrim(p_npc_key) = '' or dm.npc_key = btrim(p_npc_key))
      and dm.embedding is not null
      and 1 - (dm.embedding <=> v_embedding) >= greatest(0, least(p_match_threshold, 1))
    order by dm.embedding <=> v_embedding, dm.importance desc, dm.created_at desc
    limit greatest(p_match_count, 1)
  )
  select
    array_agg(matches.id),
    jsonb_build_object(
      'matches',
      coalesce(
        jsonb_agg(
          jsonb_build_object(
            'memory_id', matches.id,
            'npc_key', matches.npc_key,
            'memory_scope', matches.memory_scope,
            'summary', matches.summary,
            'memory_text', matches.memory_text,
            'importance', matches.importance,
            'similarity', matches.similarity,
            'metadata', matches.metadata
          )
          order by matches.similarity desc, matches.importance desc
        ),
        '[]'::jsonb
      )
    )
  into v_match_ids, v_result
  from matches;

  if v_match_ids is not null and array_length(v_match_ids, 1) > 0 then
    update public.dialogue_memories mem
       set recall_count = mem.recall_count + 1,
           last_recalled_at = timezone('utc', now()),
           updated_at = timezone('utc', now())
     where mem.id = any(v_match_ids);
  end if;

  return coalesce(v_result, jsonb_build_object('matches', '[]'::jsonb));
end;
$$;

revoke execute on function public.authoritative_upsert_dialogue_memory(text, text, uuid, uuid, text, text, text, smallint, jsonb, jsonb) from public, anon, authenticated;
revoke execute on function public.authoritative_match_dialogue_memories(text, text, jsonb, integer, double precision) from public, anon, authenticated;

grant execute on function public.authoritative_upsert_dialogue_memory(text, text, uuid, uuid, text, text, text, smallint, jsonb, jsonb) to service_role;
grant execute on function public.authoritative_match_dialogue_memories(text, text, jsonb, integer, double precision) to service_role;
