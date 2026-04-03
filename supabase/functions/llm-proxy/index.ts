/**
 * llm-proxy — Supabase Edge Function
 *
 * Proxies OpenAI-compatible chat completion requests from WebGL clients
 * to a configured LLM backend (LM Studio, OpenRouter, OpenAI, etc.).
 * The upstream API key is stored as a Supabase secret and never sent to
 * the browser.
 *
 * Required Supabase secrets (set with `supabase secrets set`):
 *   LLM_UPSTREAM_URL   — Base URL of the OpenAI-compatible endpoint
 *                        e.g. https://api.openai.com
 *                        or   https://openrouter.ai/api
 *   LLM_API_KEY        — Bearer token for the upstream LLM API
 *
 * Optional secrets:
 *   LLM_DEFAULT_MODEL  — Model name to use when the request omits one
 *                        e.g. gpt-4o-mini, qwen3-8b
 *   LLM_ALLOWED_ORIGINS — Comma-separated CORS origins (default: *)
 *
 * The client (Unity WebGL build) authenticates with the project's
 * anon/public key via the Authorization header — this is safe to ship
 * in the game bundle since the anon key only grants Edge Function access
 * for this specific function.
 */

import { serve } from "https://deno.land/std@0.224.0/http/server.ts";

// ── Constants ────────────────────────────────────────────────────────────────

const UPSTREAM_URL   = Deno.env.get("LLM_UPSTREAM_URL") ?? "";
const API_KEY        = Deno.env.get("LLM_API_KEY") ?? "";
const DEFAULT_MODEL  = Deno.env.get("LLM_DEFAULT_MODEL") ?? "";
const ALLOWED_ORIGINS = (Deno.env.get("LLM_ALLOWED_ORIGINS") ?? "*")
  .split(",")
  .map((s) => s.trim())
  .filter(Boolean);

const MAX_REQUEST_BODY_BYTES = 128_000; // 128 KB guard

// ── CORS helpers ──────────────────────────────────────────────────────────────

function corsHeaders(origin: string | null): Record<string, string> {
  const allowed =
    ALLOWED_ORIGINS.includes("*") ||
    (origin != null && ALLOWED_ORIGINS.includes(origin));

  return {
    "Access-Control-Allow-Origin": allowed ? (origin ?? "*") : ALLOWED_ORIGINS[0] ?? "*",
    "Access-Control-Allow-Headers": "Authorization, Content-Type",
    "Access-Control-Allow-Methods": "POST, OPTIONS",
  };
}

// ── Request validation ────────────────────────────────────────────────────────

interface ChatRequest {
  model?: string;
  messages: Array<{ role: string; content: string }>;
  temperature?: number;
  max_tokens?: number;
  top_p?: number;
  frequency_penalty?: number;
  presence_penalty?: number;
  stop?: string | string[];
  seed?: number;
  top_k?: number;
  repeat_penalty?: number;
  min_p?: number;
  typical_p?: number;
  repeat_last_n?: number;
  mirostat?: number;
  mirostat_tau?: number;
  mirostat_eta?: number;
  n_probs?: number;
  ignore_eos?: boolean;
  cache_prompt?: boolean;
  grammar?: string;
  thinking?: { type: string; budget_tokens: number };
  stream?: boolean;
}

function validateRequest(body: unknown): { valid: true; req: ChatRequest } | { valid: false; error: string } {
  if (typeof body !== "object" || body === null) {
    return { valid: false, error: "Request body must be a JSON object." };
  }

  const req = body as Record<string, unknown>;

  if (!Array.isArray(req.messages) || req.messages.length === 0) {
    return { valid: false, error: "messages must be a non-empty array." };
  }

  for (const msg of req.messages as unknown[]) {
    if (typeof msg !== "object" || msg === null) {
      return { valid: false, error: "Each message must be an object." };
    }
    const m = msg as Record<string, unknown>;
    if (typeof m.role !== "string" || !m.role) {
      return { valid: false, error: "Each message must have a non-empty role." };
    }
    if (typeof m.content !== "string") {
      return { valid: false, error: "Each message must have a string content." };
    }
  }

  // Streaming is not supported — Unity uses a simple request/response model.
  if (req.stream === true) {
    return { valid: false, error: "Streaming is not supported by this proxy." };
  }

  return { valid: true, req: body as ChatRequest };
}

// ── Build upstream body ───────────────────────────────────────────────────────

function buildUpstreamBody(req: ChatRequest): Record<string, unknown> {
  const body: Record<string, unknown> = {
    model: req.model || DEFAULT_MODEL || undefined,
    messages: req.messages,
    stream: false,
  };

  // Define parameter mappings to reduce conditional complexity
  const parameterMappings = [
    // Standard OpenAI parameters
    { key: 'temperature', value: req.temperature },
    { key: 'max_tokens', value: req.max_tokens },
    { key: 'top_p', value: req.top_p },
    { key: 'frequency_penalty', value: req.frequency_penalty },
    { key: 'presence_penalty', value: req.presence_penalty },
    { key: 'stop', value: req.stop },
    { key: 'seed', value: req.seed },

    // LM Studio / llama.cpp extra sampling fields
    { key: 'top_k', value: req.top_k },
    { key: 'repeat_penalty', value: req.repeat_penalty },
    { key: 'min_p', value: req.min_p },
    { key: 'typical_p', value: req.typical_p },
    { key: 'repeat_last_n', value: req.repeat_last_n },
    { key: 'mirostat', value: req.mirostat },
    { key: 'mirostat_tau', value: req.mirostat_tau },
    { key: 'mirostat_eta', value: req.mirostat_eta },
    { key: 'n_probs', value: req.n_probs },
    { key: 'ignore_eos', value: req.ignore_eos },
    { key: 'cache_prompt', value: req.cache_prompt },
    { key: 'grammar', value: req.grammar },

    // Qwen3 extended thinking
    { key: 'thinking', value: req.thinking },
  ];

  // Add parameters that are explicitly provided (avoid sending null/undefined)
  for (const mapping of parameterMappings) {
    if (mapping.value !== undefined) {
      body[mapping.key] = mapping.value;
    }
  }

  return body;
}

// ── Main handler ──────────────────────────────────────────────────────────────

serve(async (request: Request) => {
  const origin = request.headers.get("Origin");
  const cors   = corsHeaders(origin);

  // Preflight
  if (request.method === "OPTIONS") {
    return new Response(null, { status: 204, headers: cors });
  }

  if (request.method !== "POST") {
    return new Response(
      JSON.stringify({ error: "Method not allowed." }),
      { status: 405, headers: { ...cors, "Content-Type": "application/json" } }
    );
  }

  // Guard: upstream must be configured
  if (!UPSTREAM_URL || !API_KEY) {
    console.error("llm-proxy: LLM_UPSTREAM_URL or LLM_API_KEY secret is not set.");
    return new Response(
      JSON.stringify({ error: "LLM backend is not configured on the server." }),
      { status: 503, headers: { ...cors, "Content-Type": "application/json" } }
    );
  }

  // Read and size-limit the body
  const contentLength = Number.parseInt(request.headers.get("Content-Length") ?? "0", 10);
  if (contentLength > MAX_REQUEST_BODY_BYTES) {
    return new Response(
      JSON.stringify({ error: "Request body too large." }),
      { status: 413, headers: { ...cors, "Content-Type": "application/json" } }
    );
  }

  let rawBody: string;
  try {
    rawBody = await request.text();
  } catch {
    return new Response(
      JSON.stringify({ error: "Failed to read request body." }),
      { status: 400, headers: { ...cors, "Content-Type": "application/json" } }
    );
  }

  if (rawBody.length > MAX_REQUEST_BODY_BYTES) {
    return new Response(
      JSON.stringify({ error: "Request body too large." }),
      { status: 413, headers: { ...cors, "Content-Type": "application/json" } }
    );
  }

  let parsedBody: unknown;
  try {
    parsedBody = JSON.parse(rawBody);
  } catch {
    return new Response(
      JSON.stringify({ error: "Invalid JSON in request body." }),
      { status: 400, headers: { ...cors, "Content-Type": "application/json" } }
    );
  }

  const validation = validateRequest(parsedBody);
  if (!validation.valid) {
    return new Response(
      JSON.stringify({ error: validation.error }),
      { status: 400, headers: { ...cors, "Content-Type": "application/json" } }
    );
  }

  // Forward to upstream
  const upstreamBody   = buildUpstreamBody(validation.req);
  const upstreamUrl    = `${UPSTREAM_URL.replace(/\/$/, "")}/v1/chat/completions`;

  let upstreamResponse: Response;
  try {
    upstreamResponse = await fetch(upstreamUrl, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        "Authorization": `Bearer ${API_KEY}`,
      },
      body: JSON.stringify(upstreamBody),
    });
  } catch (err) {
    console.error("llm-proxy: upstream fetch failed:", err);
    return new Response(
      JSON.stringify({ error: "Failed to reach the LLM backend." }),
      { status: 502, headers: { ...cors, "Content-Type": "application/json" } }
    );
  }

  const upstreamText = await upstreamResponse.text();

  if (!upstreamResponse.ok) {
    console.error(
      `llm-proxy: upstream returned ${upstreamResponse.status}: ${upstreamText.slice(0, 256)}`
    );
    return new Response(
      JSON.stringify({ error: `LLM backend error (${upstreamResponse.status}).` }),
      {
        status: upstreamResponse.status >= 500 ? 502 : upstreamResponse.status,
        headers: { ...cors, "Content-Type": "application/json" },
      }
    );
  }

  return new Response(upstreamText, {
    status: 200,
    headers: { ...cors, "Content-Type": "application/json" },
  });
});
