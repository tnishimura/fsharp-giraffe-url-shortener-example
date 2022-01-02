module UrlShortener.App

open System
open System.IO
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Cors.Infrastructure
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection
open Giraffe
open Npgsql.FSharp
open FSharp.Control.Tasks // from Ply
open System.Threading
open System.Threading.Tasks

let connectionString = "Host=localhost; Port=15432; Database=postgres; Username=postgres; Password=nyan;"

// Npgsql.PostgresException - Query Problem
// Npgsql.NpgsqlException - connection problem
// CommonExtensionsAndTypesForNpgsqlFSharp.UnknownColumnException as e  
// CommonExtensionsAndTypesForNpgsqlFSharp.MissingQueryException as e  
// CommonExtensionsAndTypesForNpgsqlFSharp.NoResultsException as e  

// ---------------------------------
// Models
// ---------------------------------

// [<CLIMutable>]
type ShortenedUrl = 
    {
        SubmissionTime : DateTime
        SubmitterIp : string
        FullUrl : string
        ShortenedUrl : string 
    }

type NotFoundModel = {
    shortenedUrl : string
}

[<CLIMutable>]
type SubmissionModel = {
    Url : string
}

type IndexViewModel =
    {
        Recents : ShortenedUrl list
    }

type ThanksViewModel = 
    {
        AlreadyExisted : bool
        FullUrl : string
        ShortenedUrl : string
    }

// ---------------------------------
// Utility 
// ---------------------------------

let requestToFullUrl (r: HttpRequest) : string = 
    Microsoft.AspNetCore.Http.Extensions.UriHelper.GetEncodedUrl(r)

let r = System.Random()
let generateShortCode() =
    let alphabet = ['0' .. '9'] @ ['a' .. 'z']
    let N = List.length alphabet 
    Seq.init 7 (fun _ -> alphabet.[r.Next() % N].ToString())
    |> List.ofSeq
    |> String.concat ""

let readShortenedUrlRow (read: RowReader) : ShortenedUrl =
    {
        SubmissionTime = read.dateTime "submission_time"
        SubmitterIp = read.text "submitter_ip"
        FullUrl = read.text "full_url"
        ShortenedUrl = read.text "shortened_url"
    }

// ---------------------------------
// View
// ---------------------------------

module Views =
    open Giraffe.ViewEngine

    let layout (content: XmlNode list) =
        html [] [
            head [] [
                title []  [ encodedText "GiraffeViewLearn" ]
                link [ _rel  "stylesheet"
                       _type "text/css"
                       _href "/main.css" ]
            ]
            body [] ([
                nav [] [ a [_href "/"] [str "Home"] ]
            ] @ content)
        ]

    let notFound (model: NotFoundModel) : XmlNode = 
        [
            h1 [] [str "Oops, bad url!"]
            p [] [
                str "No such shortened url"
                encodedText model.shortenedUrl
            ]
        ] |> layout

    let thanks (model: ThanksViewModel) : XmlNode =
        [
            h1 [] [encodedText (if model.AlreadyExisted then "Url was previously shortened, here it is!" else "Thanks, Url has been shortened!")]
            p [] [
                encodedText "Full Url: "
                a [_href model.FullUrl] [encodedText model.FullUrl]
            ]
            p [] [
                encodedText "Shortened Url: "
                a [_href model.ShortenedUrl] [encodedText model.ShortenedUrl]
            ]
        ] |> layout

    let index (model: IndexViewModel ) : XmlNode =
        [
            h1 [] [encodedText "Shorten your url!"]
            p [] [
                form [_method "post" ; _action "/"] [
                    input [_name "url" ; _type "text"] 
                    input [_value "Shorten!" ; _type "submit"] 
                ]
            ]
            h1 [] [encodedText "Recent Shortenings"]
            table [] ([
                tr [] [
                    th [] [encodedText "Submission Time"]
                    th [] [encodedText "Submitter IP"]
                    th [] [encodedText "Shortened URL"]
                    th [] [encodedText "Full Url"]
                ]
            ] @ List.map (fun r -> 
                tr [] [ 
                    td [] [encodedText (r.SubmissionTime.ToString("MMMM dd, yyyy HH:mm"))]
                    td [] [encodedText r.SubmitterIp]
                    td [] [a [_href r.ShortenedUrl] [encodedText r.ShortenedUrl]]
                    td [] [encodedText r.FullUrl]
                ]) model.Recents)
        ] |> layout

let renderNotFound (shortenedUrl : string) = 
    let model = { shortenedUrl = shortenedUrl }
    htmlView (Views.notFound model)

let renderIndexView (recents: ShortenedUrl list) =
    let model     = { Recents = recents }
    htmlView (Views.index model)

let renderThanksView (alreadyExisted : bool) (shortenedUrl : string) (fullUrl : string) =
    let model = { 
        AlreadyExisted = alreadyExisted
        FullUrl = fullUrl
        ShortenedUrl = shortenedUrl
    }
    htmlView (Views.thanks model)

// ---------------------------------
// Database stuff
// ---------------------------------

let lookupShortenedUrl (url : string) : Task<ShortenedUrl option> =
    task {
        try 
            return!
                connectionString
                |> Sql.connect
                |> Sql.query "select * from shortened_urls where shortened_url = @shortened_url"
                |> Sql.parameters [ "shortened_url", Sql.string url ]
                |> Sql.executeRowAsync (fun read -> read |> readShortenedUrlRow |> Some)
        with
        | :? CommonExtensionsAndTypesForNpgsqlFSharp.NoResultsException 
            -> return! Task.FromResult None
    }

let lookupFullUrl (url : string) : Task<ShortenedUrl option> =
    task {
        try 
            return!
                connectionString
                |> Sql.connect
                |> Sql.query "select * from shortened_urls where full_url = @full_url"
                |> Sql.parameters [ "full_url", Sql.string url ]
                |> Sql.executeRowAsync (fun read -> read |> readShortenedUrlRow |> Some)
        with
        | :? CommonExtensionsAndTypesForNpgsqlFSharp.NoResultsException 
            -> return! Task.FromResult None
    }

let getRecentShortenedUrls (count: int) : Task<ShortenedUrl list> =
    printfn "getRecentShortenedUrls"
    connectionString
    |> Sql.connect
    |> Sql.query "select * from shortened_urls order by submission_time desc limit @limit"
    |> Sql.parameters [ "limit", Sql.int count ]
    |> Sql.executeAsync (readShortenedUrlRow)

let createNewShortener (submitterIp : string) (fullUrl: string) : Task<string> =
    let shortCode = generateShortCode ()
    let shortUrl = "https://localhost:5001/" + shortCode

    task {
        let! rows =
            connectionString
            |> Sql.connect
            |> Sql.query "insert into shortened_urls (full_url, shortened_url, submitter_ip) values ( @full_url, @shortened_url, @submitter_ip)"
            |> Sql.parameters [ 
                ("full_url", Sql.string fullUrl) 
                ("shortened_url", Sql.string shortUrl)
                ("submitter_ip", Sql.string submitterIp)]
            |> Sql.executeNonQueryAsync
        rows |> ignore
        return shortUrl
    }

// ---------------------------------
// Web App
// ---------------------------------

let handleIndex : HttpHandler =
    fun next ctx -> 
        task {
            let logger = ctx.GetLogger("submission")

            let! recents = getRecentShortenedUrls 10
            recents |> Seq.iter (fun r -> 
                logger.LogDebug(r.FullUrl)
                logger.LogDebug(r.ShortenedUrl)
                logger.LogDebug(r.SubmissionTime.ToIsoString())
                logger.LogDebug(r.SubmitterIp)
            )

            return! (renderIndexView recents) next ctx
        }

let handleRedirect (p : seq<string>) : HttpHandler = 
    fun next ctx ->
        task {
            let logger = ctx.GetLogger("submission")

            let url = requestToFullUrl ctx.Request
            logger.LogInformation(url)

            let! res = lookupShortenedUrl url
            let h = 
                match res with
                | Some s -> redirectTo false s.FullUrl
                | None -> renderNotFound url

            return! h next ctx
        }

let handleSubmission : HttpHandler =
    fun next ctx ->
        task {
            let logger = ctx.GetLogger("submission")

            let! s = ctx.BindFormAsync<SubmissionModel>()
            let fullUrl = s.Url
            logger.LogInformation("got a submission for")
            logger.LogInformation(fullUrl)

            let! existing = lookupFullUrl fullUrl
            match existing with 
            | Some s -> 
                logger.LogInformation("Already exists")
                return! (renderThanksView true (s.ShortenedUrl) fullUrl) next ctx
            | None -> 
                logger.LogInformation("Not yet!!!")
                let remoteIp = 
                    match ctx.Connection.RemoteIpAddress with
                    | null -> "0.0.0.0"
                    | ip -> ip.ToString()
                
                let! short = createNewShortener remoteIp fullUrl

                return! (renderThanksView false short fullUrl) next ctx
        }

let webApp =
    choose [
        GET >=>
            choose [
                route "/" >=> handleIndex
                routexp "/([0-9a-zA-Z]+)" handleRedirect
            ]
        POST >=> route "/" >=> handleSubmission
        setStatusCode 404 >=> text "Not Found" ]

// ---------------------------------
// Error handler
// ---------------------------------

let errorHandler (ex : Exception) (logger : ILogger) =
    logger.LogError(ex, "An unhandled exception has occurred while executing the request.")
    clearResponse >=> setStatusCode 500 >=> text ex.Message

// ---------------------------------
// Config and Main
// ---------------------------------

let configureCors (builder : CorsPolicyBuilder) =
    builder
        .WithOrigins(
            "http://localhost:5000",
            "https://localhost:5001")
       .AllowAnyMethod()
       .AllowAnyHeader()
       |> ignore

let configureApp (app : IApplicationBuilder) =
    let env = app.ApplicationServices.GetService<IWebHostEnvironment>()
    (match env.IsDevelopment() with
    | true  ->
        app.UseDeveloperExceptionPage()
    | false ->
        app .UseGiraffeErrorHandler(errorHandler)
            .UseHttpsRedirection())
        .UseCors(configureCors)
        .UseStaticFiles()
        .UseGiraffe(webApp)

let configureServices (services : IServiceCollection) =
    services.AddCors()    |> ignore
    services.AddGiraffe() |> ignore

let configureLogging (builder : ILoggingBuilder) =
    builder.AddConsole()
           .AddDebug() |> ignore

[<EntryPoint>]
let main args =
    let contentRoot = Directory.GetCurrentDirectory()
    let webRoot     = Path.Combine(contentRoot, "WebRoot")
    Host.CreateDefaultBuilder(args)
        .ConfigureWebHostDefaults(
            fun webHostBuilder ->
                webHostBuilder
                    .UseContentRoot(contentRoot)
                    .UseWebRoot(webRoot)
                    .Configure(Action<IApplicationBuilder> configureApp)
                    .ConfigureServices(configureServices)
                    .ConfigureLogging(configureLogging)
                    |> ignore)
        .Build()
        .Run()
    0
