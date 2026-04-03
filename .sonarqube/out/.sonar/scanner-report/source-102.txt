/**
 * memory-worker — Supabase Edge Function
 *
 * Processes pending dialogue memory consolidation jobs from the
 * dialogue_memory_jobs queue. Can be triggered two ways:
 *
 *   1. Scheduled: via pg_cron every N minutes (recommended)
 *      Run `supabase functions deploy memory-worker` then add the cron:
 *        SELECT cron.schedule(
 *          'memory-worker',
 *          '* * * * *',   -- every minute
 *          $$SELECT net.http_post(
 *            url      := '<project-url>/functions/v1/memory-worker',
 *            headers  := '{"Authorization":"Bearer <service-role-key>"}'::jsonb,
 *            body     := '{}'::jsonb
 *          ) AS request_id$$
 *        );
 *
 *   2. On-demand: POST /functions/v1/memory-worker (service-role key required)
 *      Unity's DialogueMemoryWorker can call this when the local server is up.
 *
 * Required Supabase secrets:
 *   LLM_UPSTREAM_URL — Base URL of the OpenAI-compatible endpoint
 *   LLM_API_KEY      — Bearer token for the upstream LLM API
 *   SUPABASE_URL     — Automatically available in Edge Functions
 *   SUPABASE_SERVICE_ROLE_KEY — Automatically available
 *
 * Optional secrets:
 *   MEMORY_WORKER_MAX_JOBS     — Max jobs to process per invocation (default: 5)
 *   MEMORY_WORKER_MAX_TOKENS   — Max tokens for summarisation LLM call (default: 300)
 *   EMBEDDING_ENDPOINT_URL     — Embedding model endpoint (same as LLM_UPSTREAM_URL/v1 if omitted)
 *   EMBEDDING_MODEL            — Embedding model name (default: nomic-embed-text-v1.5)
 */

import { serve }        from "https://deno.land/std@0.224.0/http/server.ts";
import { createClient } from "https://esm.sh/@supabase/supabase-js@2";

// ── Configuration ─────────────────────────────────────────────────────────────

const SUPABASE_URL      = Deno.env.get("SUPABASE_URL")!;
const SERVICE_ROLE_KEY  = Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")!;
const LLM_UPSTREAM_URL  = Deno.env.get("LLM_UPSTREAM_URL") ?? "";
const LLM_API_KEY       = Deno.env.get("LLM_API_KEY") ?? "";
const MAX_JOBS          = Number.parseInt(Deno.env.get("MEMORY_WORKER_MAX_JOBS")  ?? "5",   10);
const MAX_TOKENS        = Number.parseInt(Deno.env.get("MEMORY_WORKER_MAX_TOKENS") ?? "300", 10);
const EMBEDDING_URL     = Deno.env.get("EMBEDDING_ENDPOINT_URL") ?? LLM_UPSTREAM_URL;
const EMBEDDING_MODEL   = Deno.env.get("EMBEDDING_MODEL") ?? "nomic-embed-text-v1.5";

const WORKER_ID = `edge-worker-${crypto.randomUUID().slice(0, 8)}`;

// ── Types ─────────────────────────────────────────────────────────────────────

interface MemoryJob {
  job_id:      string;
  player_key:  string;
  npc_key:     string;
  session_id:  string;
  job_type:    string;
  payload:     Record<string, unknown>;
}

interface TranscriptMessage {
  speaker_role: string;
  speaker_key:  string;
  content:      string;
  created_at:   string;
}

// ── Supabase client ───────────────────────────────────────────────────────────

function makeClient() {
  return createClient(SUPABASE_URL, SERVICE_ROLE_KEY, {
    auth: { persistSession: false },
  });
}

// ── LLM helpers ───────────────────────────────────────────────────────────────

async function summariseTranscript(
  transcript: TranscriptMessage[],
  playerKey: string,
  npcKey: string,
  maxChars = 2400
): Promise<string> {
  const lines = transcript.map(
    (m) => `${m.speaker_role === "player" ? playerKey : npcKey}: ${m.content}`
  );

  let joined = lines.join("\n");
  if (joined.length > maxChars) {
    joined = joined.slice(-maxChars); // keep most recent
  }

  const systemPrompt =
    `You are a memory consolidation assistant for an NPC named "${npcKey}". ` +
    `Summarise the key facts from this dialogue in 1-3 sentences, focusing on ` +
    `what ${npcKey} learned about ${playerKey} and any significant events. ` +
    `Be concise and factual. Do not use first-person.`;

  const response = await fetch(
    `${LLM_UPSTREAM_URL.replace(/\/$/, "")}/v1/chat/completions`,
    {
      method: "POST",
      headers: {
        "Content-Type":  "application/json",
        "Authorization": `Bearer ${LLM_API_KEY}`,
      },
      body: JSON.stringify({
        model: Deno.env.get("LLM_DEFAULT_MODEL") ?? undefined,
        messages: [
          { role: "system",  content: systemPrompt },
          { role: "user",    content: joined },
        ],
        max_tokens:   MAX_TOKENS,
        temperature:  0.2,
        stream:       false,
      }),
    }
  );

  if (!response.ok) {
    throw new Error(`LLM summarisation failed: ${response.status}`);
  }

  const data = await response.json();
  return data?.choices?.[0]?.message?.content?.trim() ?? "";
}

async function generateEmbedding(text: string): Promise<number[] | null> {
  if (!EMBEDDING_URL || !text) return null;

  try {
    const response = await fetch(
      `${EMBEDDING_URL.replace(/\/$/, "")}/v1/embeddings`,
      {
        method:  "POST",
        headers: {
          "Content-Type":  "application/json",
          "Authorization": `Bearer ${LLM_API_KEY}`,
        },
        body: JSON.stringify({ model: EMBEDDING_MODEL, input: text }),
      }
    );

    if (!response.ok) return null;

    const data = await response.json();
    const embedding = data?.data?.[0]?.embedding;
    return Array.isArray(embedding) ? embedding : null;
  } catch {
    return null;
  }
}

// ── Job processing ────────────────────────────────────────────────────────────

async function processJob(client: ReturnType<typeof makeClient>, job: MemoryJob): Promise<void> {
  const sessionId = job.session_id;
  const { data: transcriptData, error: transcriptError } = await client.rpc(
    "authoritative_get_dialogue_session_transcript",
    { p_session_id: sessionId, p_message_limit: 12 }
  );

  if (transcriptError) {
    throw new Error(`Failed to fetch transcript: ${transcriptError.message}`);
  }

  const messages: TranscriptMessage[] = Array.isArray(transcriptData) ? transcriptData : [];
  if (messages.length === 0) {
    console.log(`[memory-worker] No messages for session ${sessionId}, skipping.`);
    return;
  }

  // Summarise via LLM
  const summary   = await summariseTranscript(messages, job.player_key, job.npc_key);
  const embedding = await generateEmbedding(summary);

  if (!summary) {
    throw new Error("LLM returned an empty summary.");
  }

  // Persist the memory
  const { error: upsertError } = await client.rpc(
    "authoritative_upsert_dialogue_memory",
    {
      p_player_key:   job.player_key,
      p_npc_key:      job.npc_key,
      p_session_id:   sessionId,
      p_memory_scope: "session",
      p_summary:      summary.slice(0, 280),
      p_memory_text:  summary,
      p_importance:   3,
      p_embedding:    embedding,
      p_metadata:     { source: "edge-memory-worker", job_id: job.job_id },
    }
  );

  if (upsertError) {
    throw new Error(`Failed to upsert memory: ${upsertError.message}`);
  }

  console.log(
    `[memory-worker] Processed job ${job.job_id} for ${job.player_key}/${job.npc_key}: ` +
    `"${summary.slice(0, 80)}..."`
  );
}

// ── Main handler ──────────────────────────────────────────────────────────────

serve(async () => {
  if (!LLM_UPSTREAM_URL || !LLM_API_KEY) {
    console.error("[memory-worker] LLM_UPSTREAM_URL or LLM_API_KEY secret is not set.");
    return new Response(
      JSON.stringify({ error: "Worker is not configured." }),
      { status: 503, headers: { "Content-Type": "application/json" } }
    );
  }

  const client = makeClient();

  // Claim up to MAX_JOBS jobs
  const results: Array<{ job_id: string; status: string; error?: string }> = [];

  for (let i = 0; i < MAX_JOBS; i++) {
    // Claim one job
    const { data: jobData, error: claimError } = await client.rpc(
      "authoritative_claim_memory_job",
      { p_worker_id: WORKER_ID }
    );

    if (claimError) {
      console.error(`[memory-worker] Claim failed: ${claimError.message}`);
      break;
    }

    // No more pending jobs
    if (!jobData || (Array.isArray(jobData) && jobData.length === 0)) {
      break;
    }

    const row = Array.isArray(jobData) ? jobData[0] : jobData;
    const job: MemoryJob = {
      job_id:     row.job_id ?? row.p_job_id,
      player_key: row.player_key ?? row.p_player_key,
      npc_key:    row.npc_key    ?? row.p_npc_key,
      session_id: row.session_id ?? row.p_session_id,
      job_type:   row.job_type   ?? "summarize_turns",
      payload:    row.payload    ?? {},
    };

    try {
      await processJob(client, job);

      await client.rpc("authoritative_update_memory_job_status", {
        p_job_id: job.job_id,
        p_status: "completed",
        p_error:  null,
      });

      results.push({ job_id: job.job_id, status: "completed" });
    } catch (err) {
      const message = err instanceof Error ? err.message : String(err);
      console.error(`[memory-worker] Job ${job.job_id} failed: ${message}`);

      await client.rpc("authoritative_update_memory_job_status", {
        p_job_id:       job.job_id,
        p_status:       "failed",
        p_error:        message,
        p_payload_patch: null,
      });

      results.push({ job_id: job.job_id, status: "failed", error: message });
    }
  }

  return new Response(
    JSON.stringify({ worker_id: WORKER_ID, processed: results.length, results }),
    { status: 200, headers: { "Content-Type": "application/json" } }
  );
});
