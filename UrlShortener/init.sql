
create table shortened_urls (
    shortened_url_id int generated always as identity primary key,
    submission_time timestamptz not null default now(),
    submitter_ip text not null,
    full_url text not null unique,
    shortened_url text not null
);

insert into shortened_urls (
    submitter_ip, full_url, shortened_url
) 
values 
( '127.0.0.1' , 'https://www.google.com'    , 'https://localhost:5001/1000001')  ,
( '127.0.0.1' , 'https://www.panasonic.com' , 'https://localhost:5001/1000001')  ,
( '127.0.0.1' , 'https://www.yahoo.com'     , 'https://localhost:5001/1000002')  ,
( '127.0.0.1' , 'https://myanimelist.net'   , 'https://localhost:5001/1000003')  ,
( '127.0.0.1' , 'https://www.github.com'    , 'https://localhost:5001/1000004')  ,
( '127.0.0.1' , 'https://www.microsoft.com' , 'https://localhost:5001/1000005');

