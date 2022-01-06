.EXPORT_ALL_VARIABLES:

# Adjust the connection string if not using devpg!
URL_SHORTENER_CONNECTION_STRING=Host=localhost; Port=15432; Database=postgres; Username=postgres; Password=password;

debug: 
	env | grep URL_SHOR

run:
	dotnet watch run --project UrlShortener

test: 
	dotnet test --no-restore --verbosity normal UrlShortener.Tests

db:
	devpg -i Db/init.sql -i Db/sample-data.sql
