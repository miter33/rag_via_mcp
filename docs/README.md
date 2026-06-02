# RAG Demo — Local Retrieval-Augmented Generation Pipeline

A fully local, zero-cost RAG system built on .NET 8. Drop documents into a folder, ingest them, and ask questions in a chat UI — all powered by free-tier cloud APIs and a self-hosted vector store.

---

## What Is RAG?

Large Language Models (LLMs) have a fundamental limitation: they hallucinate. When asked about facts outside their training data — your internal docs, recent events, proprietary knowledge — they confidently invent plausible-sounding answers.

**Retrieval-Augmented Generation (RAG)** solves this by grounding the model in real source material at query time. The pipeline has three phases:

### 1. Index (offline, run once)

Documents are chunked into passages, each passage is converted to a dense vector (embedding) by a model that understands semantic meaning, and those vectors are stored in a vector database alongside the original text.

*Why it matters:* The embedding model maps meaning, not keywords. "automobile" and "car" land near each other in vector space, so search finds relevant passages even when wording differs.

### 2. Retrieve (at query time)

The user's question is embedded with the same model. The vector database finds the top-N passages whose vectors are closest to the question vector (cosine similarity).

*Why it matters:* Only the relevant context is sent to the LLM — not the entire document corpus. This keeps the prompt small and the answer grounded.

### 3. Generate (at query time)

The retrieved passages are injected into the LLM prompt as context. The model synthesises an answer that is anchored to those passages rather than to its training weights.

*Why it matters:* The model can now say "according to the provided document…" and cite its source. Hallucination drops dramatically because the answer space is constrained.

---

## Component Map

| Component | Role | Why this one |
|---|---|---|
| **Qdrant** | Vector store — persists embeddings, runs nearest-neighbour search | Self-hosted Docker image, no cloud account needed, gRPC API on port 6334 |
| **Cohere `embed-english-v3.0`** | Embedding model — converts text to 1024-dimensional vectors | Free tier (1 000 calls/min), state-of-the-art retrieval quality |
| **Groq `llama-3.1-8b-instant`** | LLM — synthesises answers from retrieved context | Free tier, extremely fast inference (~500 tok/s), no GPU required |
| **Semantic Kernel 1.77.0** | .NET orchestration layer — wraps OpenAI-compatible endpoint for Groq | First-class .NET support, integrates naturally with the existing stack |
| **PdfPig 0.1.14** | Pure .NET PDF text extraction | No native dependencies, works cross-platform without Poppler or Ghostscript |

---

## Prerequisites

| Requirement | Notes |
|---|---|
| **.NET 8 SDK** | [dotnet.microsoft.com/download](https://dotnet.microsoft.com/download) — version 8.0 or later |
| **Docker Desktop** | [docker.com/products/docker-desktop](https://www.docker.com/products/docker-desktop) — used to run Qdrant locally |

---

## Setup

### Step 1 — Get a free Groq API key

1. Go to [console.groq.com](https://console.groq.com) and create a free account.
2. In the left sidebar, click **API Keys**.
3. Click **Create API Key**, give it a name, and copy the key value.

### Step 2 — Get a free Cohere API key

1. Go to [dashboard.cohere.com](https://dashboard.cohere.com) and create a free account.
2. In the left sidebar, click **API Keys**.
3. Copy the **Trial key** (or create a production key if you have one).

### Step 3 — Create the `.env` file

In the project root (same directory as `RagDemo.sln`) create a file named `.env`. You can use the provided template as a starting point:

```powershell
# From the project root
Copy-Item .env.example .env
```

Then edit `.env` and fill in your keys:

```
GROQ_API_KEY=your_groq_key_here
COHERE_API_KEY=your_cohere_key_here
```

The `.env` file is gitignored — your keys will never be committed.

### Step 4 — Start Qdrant

```powershell
docker compose up -d
```

Qdrant will be available on HTTP port 6333 (dashboard: [http://localhost:6333/dashboard](http://localhost:6333/dashboard)) and gRPC port 6334.

Verify it is running:

```powershell
docker ps
```

### Step 5 — Add documents

Drop `.md`, `.txt`, or `.pdf` files into the `docs/` folder. A `sample.md` file explaining RAG is already there to test with.

PDF files must have selectable text (not scanned images). See Troubleshooting below if text appears garbled.

### Step 6 — Ingest documents

```powershell
dotnet run --project RagDemo.Ingest
```

The ingest tool reads all documents in `docs/`, chunks them, embeds each chunk via Cohere, and upserts the vectors into Qdrant. Ingestion is **idempotent** — re-running it will not create duplicate entries.

To ingest from a different folder, pass `--docs`:

```powershell
dotnet run --project RagDemo.Ingest -- --docs C:\path\to\your\documents
```

### Step 7 — Start the chat

```powershell
dotnet run --project RagDemo.Chat
```

An interactive Spectre.Console UI opens. Type your question and press Enter. The pipeline retrieves the most relevant passages and displays the LLM's answer once it is fully generated.

---

## RAM Profile

This pipeline is designed to run comfortably on a developer laptop. No model weights are downloaded locally — all inference happens via free-tier cloud APIs.

| Component | Approximate RAM |
|---|---|
| Qdrant (Docker container) | ~50 MB |
| .NET application (ingest + chat) | ~80 MB |
| **Total** | **~130 MB** |

---

## Troubleshooting

### "Cannot connect to Qdrant" / connection refused on port 6334

Qdrant is not running. Start it with:

```powershell
docker compose up -d
```

Then confirm the container is healthy:

```powershell
docker ps
```

The status column should show `Up` for the `qdrant` container. If the container appears but is restarting, check logs with `docker compose logs qdrant`.

### Missing API key / authentication error

The application reads keys from `.env` at startup. Check:

1. The `.env` file exists in the project root — the same directory that contains `RagDemo.sln`.
2. Both `GROQ_API_KEY=` and `COHERE_API_KEY=` lines are present and non-empty.
3. There are no extra spaces around the `=` sign.

If you just created the file, restart the ingest or chat process — the file is read once at startup.

### PDF text is garbled or empty

PdfPig extracts text from the PDF's internal text layer. Scanned PDFs (image-based) have no text layer, so extraction produces garbage or nothing.

Fix: use a PDF converter or OCR tool (e.g. Adobe Acrobat, `ocrmypdf`) to produce a text-based PDF before ingesting. Plain `.txt` and `.md` files always work reliably.

### Rate limit errors (429)

Both Groq and Cohere free tiers impose requests-per-minute (RPM) limits:

- **Cohere** — up to 1 000 API calls per minute on the trial key.
- **Groq** — varies by model; `llama-3.1-8b-instant` allows a generous free quota but throttles under sustained load.

If you hit limits during bulk ingest, wait 60 seconds and re-run. Ingestion is idempotent, so already-processed chunks are skipped. During chat, rate limits are rare because each question triggers only one LLM call.
