# ZLMKit MCP Tools Description

This document provides a comprehensive overview of all MCP tools available in the ZLMKit library. These tools are designed to extend the capabilities of AI assistants by providing access to various system functions, file operations, memory management, and task coordination. This documentation is intended for AI systems that need to understand and utilize these tools effectively.

## Knowledge Graph Memory Concept

The ZLMKit implements a persistent memory system using a local knowledge graph, allowing the assistant to remember information across different chat sessions.

### Core Concepts

#### Entities
Entities are the primary nodes in the knowledge graph. Each entity has:
- A human readable unique name (identifier)
- An entity type (e.g., "person", "organization", "event")
- A list of observations

#### Relations
Relations define directed connections between entities. They are always stored in active voice and describe how entities interact or relate to each other.

#### Observations
Observations are discrete pieces of information about an entity. They are:
- Stored as strings
- Attached to specific entities
- Can be added or removed independently
- Should be atomic (one fact per observation)

## Agent Tools

### get_current_time
- **Description**: Retrieve current local date/time in the system time zone.
- **Parameters**: None
- **Returns**: Current date/time string in the system's default format.

### expression_calculator
- **Description**: Calculator for evaluating mathematical expressions. Supports arithmetic operations (+,-,*,/,** pow,! not,% mod,& and,| or,^ xor,~ inv), mathematical functions (round,trunc,int,frac,sin,cos,tan,atan,ln,exp,sign), boolean logic (if(condition;then_expr;else_expr)), variables (a=5;b=4;a+b), constants (pi,e), and conditional expressions (<, <=, >, >=, ==, !=).
- **Parameters**: 
  - `expression` (string): Mathematical expression to evaluate.
- **Returns**: Result of the mathematical calculation.

### new_uid
- **Description**: Generates a new unique identifier (UID; string format, 32 chars).
- **Parameters**: None
- **Returns**: A new GUID in string format without dashes.

## Context Tools

### get_context_summary
- **Description**: Retrieve the session context summary containing global history and current session accumulation.
- **Parameters**: 
  - `session_id` (string): The unique identifier of the user session.
- **Returns**: Summary of the session context including historical interactions.

### save_chat_milestone
- **Description**: Force-save an important dialogue milestone to the long-term session log.
- **Parameters**: 
  - `session_id` (string): The unique identifier of the user session.
  - `user_line` (string): The user's message to be recorded.
  - `assistant_line` (string): The assistant's response to be recorded.
- **Returns**: Confirmation of successful recording to the session log.

## File System Tools

### read_text_file
- **Description**: Read the complete contents of a text file from the file system. Returns the file contents as a string (UTF-8).
- **Parameters**: 
  - `path` (string): The path of the text file to read.
- **Returns**: Contents of the specified text file.

### read_image_file
- **Description**: Read an image file from the file system. Returns base64 encoding of the binary contents of a file.
- **Parameters**: 
  - `path` (string): The path of the image file to read (PNG, JPG, JPEG).
- **Returns**: Base64-encoded contents of the image file.

### write_file
- **Description**: Create a new file or overwrite an existing file with the provided content.
- **Parameters**: 
  - `path` (string): The path where the file should be written.
  - `content` (string): The text content to write into the file.
- **Returns**: Confirmation of successful file creation/writing.

### create_directory
- **Description**: Create a new directory at the specified path. If parent directories do not exist, they will be created.
- **Parameters**: 
  - `path` (string): The path of the directory to create.
- **Returns**: Confirmation of successful directory creation.

### list_directory
- **Description**: List the contents of a directory, returning a list of directory and file names.
- **Parameters**: 
  - `path` (string): The path of the directory to list.
- **Returns**: List of files and directories in the specified path.

### move_file
- **Description**: Move or rename a file or directory from a source path to a destination path. If the destination file exists, it will be overwritten.
- **Parameters**: 
  - `source` (string): The current path of the file or directory.
  - `destination` (string): The new path where the file or directory should be moved.
- **Returns**: Confirmation of successful file/directory movement.

### search_files
- **Description**: Search for a specific regular expression pattern within a file or recursively across all files in a directory. A list of files is returned with the numbers and contents of the lines where a match was found.
- **Parameters**: 
  - `path` (string): The file or directory path to search within.
  - `pattern` (string): The regular expression pattern to search for (case-insensitive).
- **Returns**: List of files with line numbers and content where matches were found.

### get_file_info
- **Description**: Retrieve metadata about a file or directory, including its type (file/directory), size in bytes, creation and modification time in formatted strings.
- **Parameters**: 
  - `path` (string): The path of the file or directory to inspect.
- **Returns**: Metadata information about the specified file or directory.

### append_file
- **Description**: Appends the provided content to the very end of an existing file.
- **Parameters**: 
  - `path` (string): The path of the file to append to.
  - `content` (string): The text content to add (a line or block).
- **Returns**: Confirmation of successful append operation.

### edit_file
- **Description**: Searches for a regex pattern in a file and replaces all occurrences with the provided replacement string.
- **Parameters**: 
  - `path` (string): The path of the file to modify.
  - `pattern` (string): The regular expression pattern to search for.
  - `replacement` (string): The string that will replace every match of the pattern.
- **Returns**: Confirmation of successful update or notification of no matches found.

### read_multiple_files
- **Description**: Read the complete contents of multiple files from the file system. Returns the contents of each file as separate text blocks, with the file name heading.
- **Parameters**: 
  - `paths` (array of strings): An array of file paths to read.
- **Returns**: Contents of each specified file, separated by file name headers.

## Memory Tools

### search_memory
- **Description**: Entities search in the long-term memory using semantic similarity. Returns unique identifiers, entity text (aggregated observations), and similarity.
- **Parameters**: 
  - `query` (string): The search query for finding relevant memories.
  - `top_k` (integer, optional): Maximum number of results to return (default: 5).
- **Returns**: List of entities matching the search query with similarity scores.

### get_knowledge_subgraph
- **Description**: Retrieve the local knowledge subgraph centered around a specific entity. List of entities in radius 1 for incoming and outgoing relations.
- **Parameters**: 
  - `entity_id` (integer): The unique identifier of the entity.
- **Returns**: Local knowledge subgraph information for the specified entity.

### upsert_memory_entity
- **Description**: Add a new entity or update existing in the long-term memory (knowledge graph). Observations of facts are always only added to this function, they are immutable and non-replaceable. Returns unique identifier for the entity.
- **Parameters**: 
  - `entity_id` (integer, optional): Unique identifier for the entity being updated; not required when adding.
  - `name` (string, optional): Human-readable name of the entity.
  - `type` (string, optional): Entity type/category (e.g., 'person', 'location', 'event', 'conversation', 'document', etc.).
  - `content` (string, optional): Human-readable text containing a detailed description or content of the fact of observation of an entity.
- **Returns**: Confirmation of successful entity recording with assigned ID.

### add_memory_relation
- **Description**: Create a semantic relationship between two knowledge entities with contextual notes.
- **Parameters**: 
  - `source_id` (integer): ID of the source entity.
  - `predicate` (string): Relationship type (e.g., 'born_in', 'parent_of', 'worked_at').
  - `target_id` (integer): ID of the target entity.
  - `context_notes` (string): Additional context or evidence for this relationship.
- **Returns**: Confirmation of successful relationship creation.

### get_memory_entity
- **Description**: Retrieve information about a specific entity from the long-term memory (knowledge graph). The name, type, identifier, and full text of observations are returned. No relations.
- **Parameters**: 
  - `entity_id` (integer): Unique identifier for the entity.
- **Returns**: Detailed information about the specified entity.

### delete_memory_entity
- **Description**: Delete an entity from the long-term memory (knowledge graph), with relations to other entities, but without cascade.
- **Parameters**: 
  - `entity_id` (integer): Unique identifier for the entity to delete.
- **Returns**: Confirmation of successful entity deletion with count of associated relations removed.

### merge_memory_entities
- **Description**: Merge two entities in the knowledge graph - only moving all relations to the target entity and deleting the source entity (if the target entity exists). To merge data of entity itself, read from source and write to target.
- **Parameters**: 
  - `source_id` (integer): ID of the source entity to be merged and deleted.
  - `target_id` (integer): ID of the target entity to which all data will be merged.
- **Returns**: Confirmation of successful entity merge with counts of updated relations.

### list_memory_entities
- **Description**: List all entities in the long-term memory (knowledge graph) in a paginated table format (20 rows per page). Returns name, type, identifier.
- **Parameters**: 
  - `type` (string, optional): Optional entity type/category (e.g., 'person', 'location', 'event', 'conversation', 'document', etc.).
  - `page` (integer, optional): Optional page number for pagination (default: 1).
- **Returns**: Paginated table of entities in the knowledge graph.

### get_memory_relation
- **Description**: Retrieve information about a specific relation between entities in the knowledge graph.
- **Parameters**: 
  - `relation_id` (integer): Unique identifier for the relation.
- **Returns**: Detailed information about the specified relation.

### update_memory_relation
- **Description**: Update an existing relation between entities in the knowledge graph.
- **Parameters**: 
  - `relation_id` (integer): ID of the relation to update.
  - `predicate` (string, optional): New predicate for the relation.
  - `context_notes` (string, optional): New context notes for the relation.
- **Returns**: Confirmation of successful relation update.

### delete_memory_relation
- **Description**: Delete a relation between entities in the knowledge graph.
- **Parameters**: 
  - `relation_id` (integer): ID of the relation to delete.
- **Returns**: Confirmation of successful relation deletion.

## Meta Tools

### search_tool
- **Description**: Search available tools by keywords. Don't mix keywords for multiple tools at the same time.
- **Parameters**: 
  - `query` (string): Query keywords string must be strictly in English.
- **Returns**: List of tools matching the search query.

### use_tool
- **Description**: Calling a tool by name.
- **Parameters**: 
  - `tool_name` (string): Tool name.
  - `arguments` (object): Tool call arguments.
- **Returns**: Results of executing the specified tool with provided arguments.

## Profile Tools

### get_user_profile
- **Description**: Read the complete user profile with all stored preferences.
- **Parameters**: None
- **Returns**: Complete user profile with all stored preferences in JSON format.

### update_user_profile
- **Description**: Write or update a fact about the user in their profile. Keys are strictly controlled via instructions.
- **Parameters**: 
  - `key` (string): The preference key (must be from the allowed list).
  - `value` (string): The value to assign to the preference key.
- **Returns**: Confirmation of successful user profile update.

### remove_user_preference
- **Description**: Remove an outdated or incorrect preference from the user profile.
- **Parameters**: 
  - `key` (string): The preference key to delete.
- **Returns**: Confirmation of successful removal of the preference.

## Task Board Tools

### create_assistant_task
- **Description**: Create a new assistant task with a specific objective.
- **Parameters**: 
  - `objective` (string): The objective or focus of the task.
  - `description` (string): Detailed description of what needs to be accomplished.
- **Returns**: Confirmation of new task creation with assigned task ID.

### update_assistant_task
- **Description**: Update progress on an existing task: add completed steps and define next steps.
- **Parameters**: 
  - `task_id` (integer): The unique ID of the task to update.
  - `add_completed_step` (string, optional): Description of a newly completed step or finding.
  - `set_next_steps` (array of strings, optional): List of next action steps for the task.
- **Returns**: Confirmation of successful task progress update.

### change_task_status
- **Description**: Change the status of a research task (COMPLETED, PAUSED, or ACTIVE).
- **Parameters**: 
  - `task_id` (integer): The unique ID of the task.
  - `status` (string): New status value: COMPLETED, PAUSED, or ACTIVE.
- **Returns**: Confirmation of successful task status change.
