---
name: localmcp-usage
description: How to read, search and write the localMCP document store, and how to keep a campaign's game state (meters, progress tracks, impacts, checkpoints) with the localMCP state tools. Use before calling any localMCP tool (list_documents, get_document, find_text, search_index, add_document, replace_section_text, request_write_permission, get_meters, update_track, set_impact and the rest), or whenever looking up, recording or updating material or game state kept in localMCP.
---

# The localMCP Campaign Store

localMCP keeps two things for a campaign: documents in categories, and game state (meters, progress tracks and impacts). Whatever a particular campaign keeps, and how it is laid out, is described by that campaign's own instructions. This skill covers how the document store and the state tools themselves work.

## Document store

### Categories and leaves

Categories are dotted paths, such as `Campaign.People` or `Campaign.Oracles.Core`. A category with no subcategories under it is a leaf; any other category is a parent.

Documents live only in leaves. Every tool that reads, lists or writes a specific document needs the full leaf path, for example `category: "Campaign.People"`, `filename: "harbourmaster.md"`.

The two search tools are the exception: they take a category at any level, and a parent covers everything under it. Search a parent when you do not know where something lives, and a leaf when you do.

Filenames include the `.md` extension and are only unique within their leaf.

### Reading

- `list_documents` lists a leaf's documents with their summaries and whether each is indexed. Given a parent it is refused with the list of leaves under it, which is a quick way to see them.
- `get_document` reads one document in full.
- `get_document_summary` reads only a document's summary, which is useful after a search returns several chunks from one file.

### Searching

There are two search tools, for two different jobs.

`find_text` finds an exact word or phrase, ignoring case, in every document, indexed or not. Use it for anything with a name: a person, a ship, a place, an item, a phrase someone said. It returns matching files with short snippets labelled by section, which often answers the question without fetching anything. Pass `wholeWord: true` when a short name could sit inside a longer word. Pass `filename`, with a leaf category, to see where a term appears in one document.

`search_index` matches on meaning, over indexed documents only. Use it for questions like "when was the hydroponics bay last worked on" or "what has anyone said about the guild". It returns chunk IDs with their leaf and filename. Fetch the text with `retrieve_search_results`, or the whole file with `get_document`. It returns 3 results by default; ask for up to 10 with `topK`.

Semantic search is poor at rare proper nouns and invented names. When the question is about a named thing, use `find_text` first.

### Writing

Every write tool refuses to run until `request_write_permission` has been called for that leaf. Permission covers one leaf only, so request it for each leaf you are about to write, and release it with `release_write_permission` when the write-up is done.

- `add_document` creates a document. Always set `indexed: true` unless there is a reason not to. Give it a one-line `summary`, since that is what `list_documents` shows.
- `replace_section_text` finds an exact snippet of text within one section and replaces every occurrence of it, leaving the rest of the section alone. Prefer this for a small change: only the changed snippet has to be written out, not the whole section.
- `replace_document_section`, `append_to_document` and `delete_document_section` change one part of a document and leave the rest alone. Reach for `replace_document_section` when more of a section is changing than a snippet-level find-and-replace can cover.
- `update_document` replaces the whole document. Use it only when most of the document changes, and then write the full replacement with nothing dropped. It has an option to index or deindex a file; that is rarely needed.
- `archive_document` removes a document from view entirely: it no longer appears in listings, searches or reads, and cannot be brought back except by tooling not provided as part of the localMCP.
- Editing an indexed document re-indexes it automatically.

### Document structure

Every store document opens with a single `#` title, and everything else sits under `##` headers. Sections are what search chunks on, what results are labelled with, and what the section tools address, so anything that will be edited on its own gets its own `##` section: each entry in a list of people, each room of a building, each item in a tracker.

Where a header name is ambiguous, qualify it with the headers above it, separated by `>`, for example `Lower Deck > Galley`.

## Campaign state

The state tools keep a campaign's meters, progress tracks and impacts, and save checkpoints of them. They are separate from the documents: no category, and no `request_write_permission`.

- Every state tool takes a `campaign`: the name of the active campaign, as the campaign's own instructions give it. Campaigns are set up by the player outside localMCP; if a campaign is not found, check the name and ask the player rather than looking for a way to create one.
- Meters are named like `health` or `jorran-hasfer.integrity`, tracks like `vow.handle-the-plantation` (kind, a dot, then a name), and impacts by what carries them (`character`, a vehicle, a module) and their name. Names are matched ignoring case.
- The tools store values and keep them in range; they apply no game rules. When a change does not fit a meter's or track's range, the result carries `clamped`, the amount that did not fit, and what follows from that is yours to apply.
- Write a change when it happens, and read values from the tools when you need them rather than from memory. Which meters and tracks a campaign has, and when to make a checkpoint, is in the campaign's own instructions.

## Tool tags

Every localMCP tool description ends with `tag::<tag>`, which groups the tools by job. If you are unsure which tool to use, run a tool search on the tag and it will return the tools that carry it, for example a search for `tag::section-editing`. Searching for multiple tags at once works as well; set the result limit to the number of tools in the tags you are searching for. The Ironsworn tags nest, so a search for `tag::ironsworn` returns all thirteen Ironsworn tools.

- **tag::core-access**: `list_documents`, `get_document`, `find_text`
- **tag::semantic-search**: `search_index`, `retrieve_search_results`, `get_document_summary`
- **tag::document-lifecycle**: `add_document`, `update_document`, `archive_document`
- **tag::section-editing**: `append_to_document`, `replace_document_section`, `replace_section_text`, `delete_document_section`
- **tag::authorization**: `request_write_permission`, `release_write_permission`
- **tag::ironsworn::dice**: `roll_dice`
- **tag::ironsworn::meters**: `get_meters`, `create_meter`, `update_meter`, `remove_meter`
- **tag::ironsworn::tracks**: `get_tracks`, `create_track`, `update_track`, `edit_track`, `remove_track`
- **tag::ironsworn::impacts**: `get_impacts`, `set_impact`
- **tag::ironsworn::checkpoints**: `create_checkpoint`
