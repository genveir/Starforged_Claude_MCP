namespace StarForged_Claude_MCP.DirectUpload;

public enum SinkType { None, Embedded, Document }

public enum UploadMode { None, Folder, Continuous }

public record UploadOptions(UploadMode Mode, SinkType Sink, string? FolderPath, string? SourceDocument, bool BeatLogging = false);
