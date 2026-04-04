create database Embedders;
go
use Embedders;
go

create table Embeddings
(
    Id int identity(1,1) primary key,
    Text nvarchar(max) not null,
    Vector varbinary(8000) not null,
    SourceDocument nvarchar(500) not null,
    TokenCount int not null
);