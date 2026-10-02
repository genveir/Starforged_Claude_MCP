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
