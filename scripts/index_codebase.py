#!/usr/bin/env python3
"""
index_codebase.py
-----------------
Index the DevProject Unity codebase into Supabase npc_knowledge_entries so
the NPC can answer quiz questions about the project architecture.

Reads every .cs file under Assets/Network_Game/, chunks by class/struct/
interface, generates a 768-dim embedding via nomic-embed-text-v1.5 (LM Studio),
and upserts each chunk into Supabase via the service-role RPC.

Usage:
    python scripts/index_codebase.py
    python scripts/index_codebase.py --npc-key NPC_Andre --dry-run
    python scripts/index_codebase.py --file Assets/Network_Game/Dialogue/NetworkDialogueService.cs
"""

import os
import re
import sys
import json
import time
import argparse
import requests
from pathlib import Path

# ── Config ────────────────────────────────────────────────────────────────────

PROJECT_ROOT   = Path(__file__).parent.parent
ASSETS_ROOT    = PROJECT_ROOT / "Assets" / "Network_Game"

SUPABASE_URL   = "http://127.0.0.1:54321"
SUPABASE_KEY   = (
    os.environ.get("SUPABASE_SERVICE_ROLE_KEY")
    or "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9"
       ".eyJpc3MiOiJzdXBhYmFzZS1kZW1vIiwicm9sZSI6InNlcnZpY2Vfcm9sZSIsImV4cCI6"
       "MTk4MzgxMjk5Nn0.EGIM96RAZx35lJzdJsyH-qQwv8Hdp7fsn3W0YpN81IU"
)

EMBED_URL      = os.environ.get("LMSTUDIO_EMBED_URL", "http://127.0.0.1:7002/v1/embeddings")
EMBED_MODEL    = os.environ.get("LMSTUDIO_EMBED_MODEL", "nomic-embed-text-v1.5")
EMBED_API_KEY  = os.environ.get("LMSTUDIO_API_KEY", "sk-lm-okYEQixt:xqzKrlXmre2LhMNHZJsn")
EMBED_DIM      = 768

DEFAULT_NPC_KEY = "NPC_Andre"

# Directories and file patterns to skip
EXCLUDE_DIRS     = {"Tests", "Editor", "ParticlePack", "StarterAssets", "Library", "Packages"}
EXCLUDE_SUFFIXES = {".Designer.cs", ".g.cs", "AssemblyInfo.cs"}

MAX_CONTENT_CHARS = 1000  # truncate long chunks to keep tokens manageable
MIN_CONTENT_CHARS = 80    # skip trivially short chunks
EMBED_DELAY_SEC   = 0.05  # polite delay between embedding calls

# ── Chunking ──────────────────────────────────────────────────────────────────

# Matches class/struct/interface/enum declarations (with optional modifiers and XML docs)
_TYPE_RE = re.compile(
    r'(?:(?:///[^\n]*\n\s*)+)?'                             # optional XML doc block
    r'\s*(?:(?:public|internal|private|protected|'
    r'partial|abstract|sealed|static|readonly)\s+)+'       # modifiers
    r'(?:class|struct|interface|enum|record)\s+'
    r'(\w+)',                                               # type name → group 1
    re.MULTILINE,
)

# Matches method/property signatures (public/internal members)
_MEMBER_RE = re.compile(
    r'^\s{4,}(?:public|internal|protected|private|override|virtual|static|async|'
    r'abstract|sealed|readonly|extern)\s+[\w<>\[\], ?]+\s+\w+\s*[({]',
    re.MULTILINE,
)


def _strip_bodies(text: str) -> str:
    """Replace method/property bodies with ';' to keep only signatures."""
    result = []
    depth = 0
    in_body = False
    i = 0
    while i < len(text):
        ch = text[i]
        if ch == '{':
            if depth == 0:
                in_body = True
                result.append('{')
            depth += 1
        elif ch == '}':
            depth -= 1
            if depth == 0:
                in_body = False
                result.append('}')
            # skip inner chars
        elif not in_body or depth == 0:
            result.append(ch)
        i += 1
    return ''.join(result)


def _extract_namespace(text: str) -> str:
    m = re.search(r'\bnamespace\s+([\w.]+)', text)
    return m.group(1) if m else ""


def _create_single_chunk(relative: str, namespace: str, cs_path: Path, raw: str) -> list[dict]:
    """Create a single chunk for files with no type declarations."""
    stripped = raw.strip()
    if len(stripped) < MIN_CONTENT_CHARS:
        return []

    content = stripped[:MAX_CONTENT_CHARS]
    title = f"{namespace}.{cs_path.stem}" if namespace else cs_path.stem
    return [{"source_file": relative, "chunk_index": 0,
             "title": title, "content": content}]


def _process_type_chunk(relative: str, namespace: str, matches: list, idx: int, raw: str) -> dict | None:
    """Process a single type declaration chunk."""
    m = matches[idx]
    type_name = m.group(1)
    start = m.start()
    end = matches[idx + 1].start() if idx + 1 < len(matches) else len(raw)
    section = raw[start:end]

    # Keep signatures only (strip method bodies)
    section = _strip_bodies(section)

    # Keep member signatures for readability
    lines = [line.rstrip() for line in section.splitlines() if line.rstrip()]

    content = "\n".join(lines).strip()
    if not content or len(content) < MIN_CONTENT_CHARS:
        return None

    content = content[:MAX_CONTENT_CHARS]
    title = f"{namespace}.{type_name}" if namespace else type_name

    return {
        "source_file": relative,
        "chunk_index": idx,
        "title": title,
        "content": content,
    }


def chunk_file(cs_path: Path) -> list[dict]:
    """
    Return a list of chunk dicts:
        {source_file, chunk_index, title, content}
    """
    try:
        raw = cs_path.read_text(encoding="utf-8", errors="ignore")
    except Exception as e:
        print(f"  [skip] cannot read {cs_path}: {e}", file=sys.stderr)
        return []

    relative = str(cs_path.relative_to(PROJECT_ROOT)).replace("\\", "/")
    namespace = _extract_namespace(raw)
    matches = list(_TYPE_RE.finditer(raw))

    # Handle files with no type declarations
    if not matches:
        return _create_single_chunk(relative, namespace, cs_path, raw)

    # Process files with type declarations
    chunks = []
    for idx in range(len(matches)):
        chunk = _process_type_chunk(relative, namespace, matches, idx, raw)
        if chunk:
            chunks.append(chunk)

    return chunks


def collect_files(root: Path, single_file: Path | None) -> list[Path]:
    if single_file:
        return [single_file]
    files = []
    for cs in root.rglob("*.cs"):
        # Skip excluded directories
        parts = set(cs.parts)
        if parts & EXCLUDE_DIRS:
            continue
        if any(cs.name.endswith(suf) for suf in EXCLUDE_SUFFIXES):
            continue
        files.append(cs)
    return sorted(files)


# ── Embedding ─────────────────────────────────────────────────────────────────

def embed(text: str) -> list[float] | None:
    try:
        resp = requests.post(
            EMBED_URL,
            headers={
                "Authorization": f"Bearer {EMBED_API_KEY}",
                "Content-Type": "application/json",
            },
            json={"model": EMBED_MODEL, "input": text},
            timeout=30,
        )
        resp.raise_for_status()
        data = resp.json()
        vec = data["data"][0]["embedding"]
        if len(vec) != EMBED_DIM:
            print(f"  [warn] embedding dim={len(vec)}, expected {EMBED_DIM}", file=sys.stderr)
            return None
        return vec
    except Exception as e:
        print(f"  [embed error] {e}", file=sys.stderr)
        return None


# ── Supabase upsert ───────────────────────────────────────────────────────────

def upsert_chunk(npc_key: str, chunk: dict, vector: list[float]) -> bool:
    try:
        resp = requests.post(
            f"{SUPABASE_URL}/rest/v1/rpc/authoritative_upsert_npc_knowledge",
            headers={
                "Authorization": f"Bearer {SUPABASE_KEY}",
                "apikey":        SUPABASE_KEY,
                "Content-Type":  "application/json",
            },
            json={
                "p_npc_key":     npc_key,
                "p_source_file": chunk["source_file"],
                "p_chunk_index": chunk["chunk_index"],
                "p_title":       chunk["title"],
                "p_content":     chunk["content"],
                "p_embedding":   vector,
            },
            timeout=15,
        )
        resp.raise_for_status()
        return True
    except Exception as e:
        print(f"  [upsert error] {chunk['source_file']}:{chunk['chunk_index']} — {e}",
              file=sys.stderr)
        return False


# ── Main ──────────────────────────────────────────────────────────────────────

def _process_chunk_no_embed(chunk: dict) -> tuple[int, int, int]:
    """Process a chunk in no-embed mode."""
    print(f"    [{chunk['chunk_index']}] {chunk['title']} ({len(chunk['content'])} chars) [no embed]")
    return 1, 1, 0  # total, upserted, failed


def _process_chunk_dry_run(chunk: dict) -> tuple[int, int, int]:
    """Process a chunk in dry-run mode."""
    vector = embed(chunk["content"])
    if vector is None:
        print(f"    [{chunk['chunk_index']}] {chunk['title']} — embedding failed")
        return 1, 0, 1  # total, upserted, failed

    print(f"    [{chunk['chunk_index']}] {chunk['title']} — embedded ({len(vector)}d) [dry-run]")
    return 1, 1, 0  # total, upserted, failed


def _process_chunk_normal(npc_key: str, chunk: dict) -> tuple[int, int, int]:
    """Process a chunk in normal upsert mode."""
    vector = embed(chunk["content"])
    if vector is None:
        return 1, 0, 1  # total, upserted, failed

    ok = upsert_chunk(npc_key, chunk, vector)
    if ok:
        print(f"    [{chunk['chunk_index']}] {chunk['title']} OK")
        return 1, 1, 0  # total, upserted, failed
    else:
        return 1, 0, 1  # total, upserted, failed


def _process_file_chunks(cs_path: Path, npc_key: str, args) -> tuple[int, int, int]:
    """Process all chunks for a single file."""
    chunks = chunk_file(cs_path)
    if not chunks:
        return 0, 0, 0  # total, upserted, failed

    rel = str(cs_path.relative_to(PROJECT_ROOT)).replace("\\", "/")
    print(f"  {rel} -> {len(chunks)} chunk(s)")

    total_chunks = upserted = failed = 0

    # Choose processing function based on mode
    if args.no_embed:
        process_func = _process_chunk_no_embed
    elif args.dry_run:
        process_func = _process_chunk_dry_run
    else:
        process_func = lambda chunk: _process_chunk_normal(npc_key, chunk)

    for chunk in chunks:
        t, u, f = process_func(chunk)
        total_chunks += t
        upserted += u
        failed += f

        # Rate limiting for API calls
        if not args.no_embed:
            time.sleep(EMBED_DELAY_SEC)

    return total_chunks, upserted, failed


def main():
    parser = argparse.ArgumentParser(description="Index codebase into Supabase NPC knowledge base.")
    parser.add_argument("--npc-key",  default=DEFAULT_NPC_KEY, help="NPC key in Supabase (default: NPC_Andre)")
    parser.add_argument("--file",     default=None,            help="Index a single .cs file instead of all")
    parser.add_argument("--dry-run",  action="store_true",     help="Chunk and embed but do NOT upsert")
    parser.add_argument("--no-embed", action="store_true",     help="Skip embedding (test chunking only)")
    args = parser.parse_args()

    single = Path(args.file) if args.file else None
    files  = collect_files(ASSETS_ROOT, single)

    print(f"Found {len(files)} .cs file(s) to index for npc_key='{args.npc_key}'")

    total_chunks = upserted = failed = 0

    for cs_path in files:
        t, u, f = _process_file_chunks(cs_path, args.npc_key, args)
        total_chunks += t
        upserted += u
        failed += f

    print(f"\nDone. chunks={total_chunks}  upserted={upserted}  failed={failed}")


if __name__ == "__main__":
    main()
