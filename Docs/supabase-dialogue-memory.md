# Supabase Dialogue Memory

## Why this stack

This project uses a local Supabase stack for development because it gives us:

- Postgres for persistent narrative and player state
- `pgvector` for semantic dialogue-memory retrieval
- reproducible Dockerized local services
- PostgREST RPC endpoints that fit the game's server-authoritative flow

The important rule is authority:

- Unity clients send gameplay and dialogue intent through NGO
- the authoritative game server decides what actually happened
- only the authoritative server path writes dialogue history, runtime state, and memories to Supabase

Supabase is persistence and retrieval here, not gameplay authority.

## Repo files

- `supabase/config.toml`
- `supabase/migrations/20260324191931_init_game_memory_schema.sql`
- `supabase/migrations/20260324194027_refine_dialogue_authority_contract.sql`
- `supabase/seed.sql`
- `.mcp.json`
- `.claude/mcp.json`
- `package.json`

Useful commands:

- `npm run supabase:start`
- `npm run supabase:status`
- `npm run supabase:status:env`
- `npm run supabase:stop`
- `npm run supabase:db:reset`
- `npm run supabase:db:push`
- `npm run supabase:db:lint`

## Data model

### `public.player_profiles`

Stable identity and narrative profile for a player.

- primary key: `player_key text`
- optional future direct-auth link: `auth_user_id uuid`
- narrative fields: `bio`, `long_term_summary`
- non-critical authored/player metadata: `customization_json`, `metadata`

`player_key` is the current source of truth for game integration because Unity currently identifies players through local identity and prompt-context sync, not Supabase Auth.

### `public.player_runtime_state`

Fast-changing gameplay state that should not be mixed into the long-lived player profile.

- `player_key`
- `scene_name`
- `current_health`
- `max_health`
- `position jsonb`
- `status_flags jsonb`
- `last_synced_at`

### `public.npc_profiles`

Stable NPC identity registry for memory targeting.

- primary key: `npc_key text`
- `display_name`
- `bio`
- `profile_asset_key`

`npc_key` should come from `NpcDialogueActor.ProfileId`, not volatile scene instance names.

### `public.dialogue_sessions`

One conversation window between one player and one NPC.

- `player_key`
- `npc_key`
- `conversation_key`
- `scene_name`
- `summary`
- `started_at`, `ended_at`, `last_message_at`

### `public.dialogue_messages`

Append-only transcript rows.

- `session_id`
- `player_key`
- `npc_key`
- `speaker_role`
- `speaker_key`
- `turn_index`
- `content`
- `structured_actions`
- `extracted_facts`

### `public.dialogue_memories`

Derived memory rows for retrieval, not raw truth.

- `player_key`
- `npc_key`
- optional `session_id`, `source_message_id`
- `memory_scope`
- `summary`
- `memory_text`
- `importance`
- `embedding vector(1536)`

### `public.memory_jobs`

Asynchronous summarization/embedding work queue.

- `player_key`
- `npc_key`
- `session_id`
- `job_type`
- `status`
- `payload`
- `error`

## RPC contract

These functions are the authoritative write/read surface used by the game server:

- `authoritative_upsert_player_profile`
- `authoritative_upsert_player_runtime_state`
- `authoritative_upsert_npc_profile`
- `authoritative_open_dialogue_session`
- `authoritative_append_dialogue_message`
- `authoritative_close_dialogue_session`
- `authoritative_enqueue_memory_job`
- `authoritative_get_recent_dialogue_context`
- `authoritative_claim_memory_job`
- `authoritative_update_memory_job_status`
- `authoritative_get_dialogue_session_transcript`
- `authoritative_upsert_dialogue_memory`
- `authoritative_match_dialogue_memories`
- `match_dialogue_memories`

The migration explicitly revokes execute from `anon` and `authenticated` for these RPCs and grants execute only to `service_role`. That keeps direct client calls from becoming a second authority path.

## Unity authority flow

Current intended flow:

1. Client sends dialogue intent to `NetworkDialogueService`.
2. Server validates participants and player identity.
3. Server resolves NPC profile and conversation key.
4. Server runs inference and applies gameplay/effect consequences.
5. `DialoguePersistenceGateway` persists the authoritative player turn and NPC turn.
6. `CombatHealth` events persist authoritative health snapshots.
7. Every few NPC replies, the gateway queues a memory job.
8. `NetworkDialogueService` can query `authoritative_get_recent_dialogue_context` before inference.
9. Future semantic retrieval can extend this with `match_dialogue_memories`.

## Unity integration

Current server-side gateway:

- `Assets/Network_Game/Dialogue/Persistence/DialoguePersistenceGateway.cs`
- `Assets/Network_Game/Dialogue/Persistence/DialogueMemoryWorker.cs`

Responsibilities:

- server-only writes
- player profile upsert
- runtime health sync
- NPC profile sync from live scene actors
- session open and transcript append on `NetworkDialogueService.OnRawDialogueResponse`
- periodic memory-job enqueue
- job claim, transcript fetch, memory insert, and job completion RPC access

Current memory worker flow:

1. `DialoguePersistenceGateway` enqueues `summarize_turns` jobs after every few NPC replies.
2. `DialogueMemoryWorker` claims queued jobs through `authoritative_claim_memory_job`.
3. The worker fetches the session transcript with `authoritative_get_dialogue_session_transcript`.
4. The worker reuses the configured OpenAI-compatible dialogue backend to compress the transcript into one durable memory.
5. The worker optionally generates a `text-embedding-3-small` embedding for the memory text.
6. The worker inserts that memory with `authoritative_upsert_dialogue_memory`.
7. The worker marks the job completed or failed with `authoritative_update_memory_job_status`.

Current semantic recall flow:

1. `NetworkDialogueService` still fetches recent transcript and recent summaries through `authoritative_get_recent_dialogue_context`.
2. When semantic recall is enabled, it resolves the local `DialogueMemoryWorker` embedding configuration and embeds the current player prompt.
3. It queries Supabase through `authoritative_match_dialogue_memories`.
4. Supabase uses the HNSW `pgvector` index on `dialogue_memories.embedding` to return only the top relevant long-term memories.
5. The RPC updates `recall_count` and `last_recalled_at` for returned rows.

This gives the game both:

- cheap recent-context recall from relational transcript tables
- semantic long-term memory recall from vector search

## Performance notes

The current implementation deliberately uses the Supabase features that matter most for dialogue runtime performance without overcomplicating the authority model:

- `pgvector` + HNSW index for fast semantic lookup over long-term memory
- append-only transcript rows for cheap writes during gameplay
- explicit RPC wrappers so Unity does not build SQL dynamically
- asynchronous memory jobs so inference and persistence stay off the critical response path

The Supabase docs also recommend `pgmq`, `pg_net`, `pg_cron`, and Edge Functions for fully automated embedding pipelines. Those are strong options for a later phase, but I kept the first runtime implementation inside the authoritative Unity server so gameplay authority stays in one place and the host does not depend on an additional function runner during local development.

Current prompt integration:

- `NetworkDialogueService` now queries persisted recent context through the gateway before inference
- persisted transcript lines are injected only when live in-memory history is empty
- persistent memory summaries are appended as a compact system-prompt section
- lookup failures are non-fatal and fall back to the normal prompt path

Credential contract:

- base URL default: `http://127.0.0.1:54321`
- service key env var default: `SUPABASE_SERVICE_ROLE_KEY`
- local CLI fallback env var: `SERVICE_ROLE_KEY`
- loopback-only endpoint requirement stays enabled by default

## Local URLs

After `npm run supabase:start`:

- Studio: `http://127.0.0.1:54323`
- API: `http://127.0.0.1:54321`
- Postgres: `postgresql://postgres:postgres@127.0.0.1:54322/postgres`
- MCP: `http://127.0.0.1:54321/mcp`

## MCP management

The local Supabase stack already exposes an MCP endpoint when it is running, so there is no separate MCP server process to install for local development.

Configured clients:

- `.mcp.json` -> `supabaseLocal`
- `.claude/mcp.json` -> `supabaseLocal`
