-- Replaces: Faces\Names.txt (%-delimited) + Faces\faceN.bmp + the registry
-- key HKCU\SOFTWARE\FRS. A single small SQLite file, safe to back up/copy,
-- with real referential integrity between people and their embeddings.

CREATE TABLE IF NOT EXISTS Users (
    Id             INTEGER PRIMARY KEY AUTOINCREMENT,
    Username       TEXT NOT NULL UNIQUE,
    PasswordHash   TEXT NOT NULL,     -- BCrypt hash, never the raw password
    CreatedAt      TEXT NOT NULL,
    LastLoginAt    TEXT NULL
);

CREATE TABLE IF NOT EXISTS People (
    Id             INTEGER PRIMARY KEY AUTOINCREMENT,
    Name           TEXT NOT NULL,
    Notes          TEXT NULL,
    CreatedAt      TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS FaceEmbeddings (
    Id               INTEGER PRIMARY KEY AUTOINCREMENT,
    PersonId         INTEGER NOT NULL REFERENCES People(Id) ON DELETE CASCADE,
    Vector           BLOB NOT NULL,      -- 128 x 4-byte floats, little-endian
    CreatedAt        TEXT NOT NULL,
    SourceVideoName  TEXT NULL
);

CREATE INDEX IF NOT EXISTS IX_FaceEmbeddings_PersonId ON FaceEmbeddings(PersonId);

-- Optional audit trail of what was recognized, when, in which video —
-- the old app kept no history at all.
CREATE TABLE IF NOT EXISTS Detections (
    Id             INTEGER PRIMARY KEY AUTOINCREMENT,
    PersonId       INTEGER NULL REFERENCES People(Id) ON DELETE SET NULL,
    VideoName      TEXT NOT NULL,
    FrameIndex     INTEGER NOT NULL,
    TimestampMs    INTEGER NOT NULL,
    MatchScore     REAL NULL,
    CreatedAt      TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS IX_Detections_VideoName ON Detections(VideoName);
