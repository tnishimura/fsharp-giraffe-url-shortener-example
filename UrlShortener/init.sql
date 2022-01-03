
create table shortened_urls (
    shortened_url_id int generated always as identity primary key,
    submission_time timestamptz not null default now(),
    submitter_ip text not null,
    full_url text not null unique,
    short_code text not null
);

insert into shortened_urls (
    submitter_ip, full_url, short_code
) 
values 
( '127.0.0.1' , 'https://www.google.com'    , '1000001')  ,
( '127.0.0.1' , 'https://www.panasonic.com' , '1000001')  ,
( '127.0.0.1' , 'https://www.yahoo.com'     , '1000002')  ,
( '127.0.0.1' , 'https://myanimelist.net'   , '1000003')  ,
( '127.0.0.1' , 'https://www.github.com'    , '1000004')  ,
( '127.0.0.1' , 'https://www.microsoft.com' , '1000005');

