
# fsharp-giraffe-url-shortener-example

This is an example URL shortener project (like bit.ly or tinyurl), implemented using F#, [Giraffe](https://github.com/giraffe-fsharp/Giraffe), PostgreSQL and [Npgsql.FSharp](https://github.com/Zaid-Ajaj/Npgsql.FSharp).

It includes a Test project as well as github actions configured for running these tests on every push to github.

This project is designed for .NET 5.0.

Assuming you are new to Giraffe, my article [Understanding the ASP.NET Core Boilerplate](https://carpenoctem.dev/blog/giraffe-by-example-understanding-asp-net-core-boilerplate/) may also be helpful.

## Running the project


First, you'll need to initialize a PostgreSQL database with Db/init.sql.
It's beyond the scope of this README to teach you how to set up Pg, but if you want to get started quickly, you can use the [devpg](https://github.com/tnishimura/devpg) like this:

    devpg -i Db/init.sql -i Db/sample-data.sql

Second, you'll need to set `URL_SHORTENER_CONNECTION_STRING` environmental variables to a connection string. 
If using `devpg`, this would look like:

    export URL_SHORTENER_CONNECTION_STRING="Host=localhost; Port=15432; Database=postgres; Username=postgres; Password=password;"

Finally, you can run the application with:

    dotnet restore
    dotnet watch run --project UrlShortener

## Running tests

    dotnet test --no-restore --verbosity normal UrlShortener.Tests

## License

MIT
