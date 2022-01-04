
create table shortened_urls (
    shortened_url_id int generated always as identity primary key,
    submission_time timestamptz not null default now(),
    submitter_ip text not null,
    full_url text not null unique,
    short_code text not null
);

