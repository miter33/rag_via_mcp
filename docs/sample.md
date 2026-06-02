# What is RAG?

Retrieval-Augmented Generation (RAG) is an AI technique that improves LLM answers
by retrieving relevant documents at query time and including them as context.
Instead of relying solely on what the model memorised during training, RAG grounds
the model's response in specific, up-to-date, and citable source material.

## Key Components

- **Vector Store**: A database optimised for similarity search over dense float vectors.
  Qdrant is a high-performance open-source vector store.
- **Embedding Model**: Converts text into vectors. Semantically similar texts land
  near each other in vector space. Cohere's embed-english-v3.0 produces 1024-dimensional vectors.
- **LLM**: Reads the retrieved chunks and generates a fluent, cited answer.
  Groq runs llama-3.1-8b-instant at very high speed on free-tier hardware.
- **Chunking**: Long documents are split into smaller pieces (chunks) before embedding,
  because embedding models have input length limits and shorter, focused chunks
  retrieve more precisely than entire documents.

## Why RAG Reduces Hallucination

LLMs are trained on static data and confidently invent facts about topics they
don't know well (hallucination). RAG forces the model to answer from a specific
context window containing only your documents. If the answer isn't there, a
well-prompted model says "I don't know" rather than making something up.
