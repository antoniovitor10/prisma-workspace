---
id: ADR-0008
titulo: "Armazenamento local de anexos com metadados no SQL Server"
status: aceita
data: 2026-06-30
origem: "DECISIONS.md D8"
substitui: ""
---

# ADR-0008 · Armazenamento local de anexos com metadados no SQL Server

## Decisão

- **D8** - Storage de anexos na Fase 3: arquivos ficam em disco local controlado pela API (`App_Data/attachments`) e metadados ficam no SQL Server via entidade `Attachment`. Evita configurar FILESTREAM agora e mantem caminho simples para trocar por blob storage futuramente. (2026-06-30)
