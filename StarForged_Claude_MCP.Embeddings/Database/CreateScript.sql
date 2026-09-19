create database Embedders;
go
use Embedders;
go

create table Documents
(
    Id int identity(1,1) primary key,
    Category nvarchar(200) not null,
    Filename nvarchar(500) not null,
    Content nvarchar(max) not null,
    Summary nvarchar(512) null,
    Indexed bit not null,
    constraint UQ_Documents_Category_Filename unique (Category, Filename)
);

create table Embeddings
(
    Id int identity(1,1) primary key,
    DocumentId int not null,
    Text nvarchar(max) not null,
    Vector varbinary(8000) not null,
    TokenCount int not null,
    constraint FK_Embeddings_Documents foreign key (DocumentId) references Documents (Id) on delete cascade
);

create index IX_Embeddings_DocumentId on Embeddings (DocumentId);

create table Beats
(
    Id int identity(1,1) primary key,
    Category nvarchar(200) not null,
    SessionNumber int not null,
    BeatNumber int null,
    Version int null,
    Content nvarchar(max) not null,
    constraint CK_Beats_NumberedBeatsAreVersioned
        check ((BeatNumber is null and Version is null) or (BeatNumber is not null and Version is not null))
);

create index IX_Beats_Category_Session on Beats (Category, SessionNumber);

-- An unnumbered beat may occur many times per session, so this cannot be a unique
-- constraint: SQL Server treats nulls as equal for those. Filtered to numbered beats,
-- it stops the same version of a beat being written twice.
create unique index UX_Beats_Numbered on Beats (Category, SessionNumber, BeatNumber, Version)
    where BeatNumber is not null;
