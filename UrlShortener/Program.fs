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
open System.Threading.Tasks

let connectionString = System.Environment.GetEnvironmentVariable "URL_SHORTENER_CONNECTION_STRING"
let urlPrefix = "https://localhost:5001"

// ---------------------------------
// Models
// ---------------------------------

type ShortenedUrl = 
    {
        SubmissionTime : DateTime
        SubmitterIp : string
        FullUrl : string
        ShortCode : string 
    }

type IndexViewModel =
    {
        Recents : ShortenedUrl list
    }

type ThanksViewModel = 
    {
        AlreadyExisted : bool
        FullUrl : string
        ShortCode : string
    }

type NotFoundViewModel = 
    {
        ShortCode : string
    }

[<CLIMutable>]
type SubmissionModel = 
    {
        Url : string
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
        ShortCode = read.text "short_code"
    }

let shortCodeToUrl (shortCode : string) : string = 
    $"{urlPrefix}/{shortCode}"

// ---------------------------------
// View
// ---------------------------------

module Views =
    open Giraffe.ViewEngine

    let layout (content: XmlNode list) =
        html [] [
            head [] [
                title []  [ str "GiraffeViewLearn" ]
                link [ _rel  "stylesheet"
                       _type "text/css"
                       _href "/main.css" ]
            ]
            body [] ([
                nav [] [ a [_href "/"] [str "Home"] ]
            ] @ content)
        ]

    let index (model: IndexViewModel ) : XmlNode =
        [
            h1 [] [str "Shorten your url!"]
            p [] [
                form [_method "post" ; _action "/"] [
                    input [_name "url" ; _type "text"] 
                    input [_value "Shorten!" ; _type "submit"]]]
            h1 [] [str "Recent Shortenings"]
            table [] ([
                tr [] [
                    th [] [str "Submission Time"]
                    th [] [str "Submitter IP"]
                    th [] [str "Shortened URL"]
                    th [] [str "Full Url"]]] 
            @ [ for r in model.Recents -> 
                    let s = shortCodeToUrl r.ShortCode
                    tr [] [ 
                        td [] [str (r.SubmissionTime.ToString("MMMM dd, yyyy HH:mm"))]
                        td [] [str r.SubmitterIp]
                        td [] [a [_href s] [str s]]
                        td [] [a [_href r.FullUrl] [str r.FullUrl]]]])
        ] |> layout

    let thanks (model: ThanksViewModel) : XmlNode =
        let s = shortCodeToUrl model.ShortCode
        [
            h1 [] [str (if model.AlreadyExisted then "Url was previously shortened, here it is!" else "Thanks, Url has been shortened!")]
            p [] [
                str "Full Url: "
                a [_href model.FullUrl] [str model.FullUrl]
            ]
            p [] [
                str "Shortened Url: "
                a [_href s] [str s]
            ]
        ] |> layout

    let notFound (model: NotFoundViewModel) : XmlNode = 
        let s = shortCodeToUrl model.ShortCode
        [
            h1 [] [str "Oops, bad url!"]
            p [] [
                str "No such shortened url"
                str s
            ]
        ] |> layout

let renderNotFound (shortCode : string) = 
    let model = { ShortCode = shortCode }
    htmlView (Views.notFound model)

let renderIndexView (recents: ShortenedUrl list) =
    let model     = { Recents = recents }
    htmlView (Views.index model)

let renderThanksView (alreadyExisted : bool) (shortCode : string) (fullUrl : string) =
    let model = { 
        AlreadyExisted = alreadyExisted
        FullUrl = fullUrl
        ShortCode = shortCode
    }
    htmlView (Views.thanks model)

// ---------------------------------
// Database stuff
// ---------------------------------

let lookupShortCode (code : string) : Task<ShortenedUrl option> =
    task {
        try 
            return!
                connectionString
                |> Sql.connect
                |> Sql.query "select * from shortened_urls where short_code = @short_code"
                |> Sql.parameters [ "short_code", Sql.string code ]
                |> Sql.executeRowAsync (fun read -> read |> readShortenedUrlRow |> Some)
        with
        | :? CommonExtensionsAndTypesForNpgsqlFSharp.NoResultsException 
            -> return! Task.FromResult None
    }

let lookupFullUrl (full_url : string) : Task<ShortenedUrl option> =
    task {
        try 
            return!
                connectionString
                |> Sql.connect
                |> Sql.query "select * from shortened_urls where full_url = @full_url"
                |> Sql.parameters [ "full_url", Sql.string full_url ]
                |> Sql.executeRowAsync (fun read -> read |> readShortenedUrlRow |> Some)
        with
        | :? CommonExtensionsAndTypesForNpgsqlFSharp.NoResultsException 
            -> return! Task.FromResult None
    }

let getRecentShortenedUrls (count: int) : Task<ShortenedUrl list> =
    connectionString
    |> Sql.connect
    |> Sql.query "select * from shortened_urls order by submission_time desc limit @limit"
    |> Sql.parameters [ "limit", Sql.int count ]
    |> Sql.executeAsync (readShortenedUrlRow)

let createNewShortener (submitterIp : string) (fullUrl: string) : Task<string> =
    let shortCode = generateShortCode ()

    task {
        let! rows =
            connectionString
            |> Sql.connect
            |> Sql.query "insert into shortened_urls (full_url, short_code, submitter_ip) values ( @full_url, @short_code, @submitter_ip)"
            |> Sql.parameters [ 
                ("full_url", Sql.string fullUrl) 
                ("short_code", Sql.string shortCode)
                ("submitter_ip", Sql.string submitterIp)]
            |> Sql.executeNonQueryAsync
        rows |> ignore
        return shortCode
    }

// ---------------------------------
// Handlers
// ---------------------------------

let handleIndex : HttpHandler =
    fun next ctx -> 
        task {
            let! recents = getRecentShortenedUrls 10
            return! (renderIndexView recents) next ctx
        }

let handleRedirect (shortCode : string ) : HttpHandler = 
    fun next ctx ->
        task {
            let! res = lookupShortCode shortCode
            let h = 
                match res with
                | Some s -> redirectTo false s.FullUrl
                | None -> setStatusCode 404 >=> renderNotFound shortCode

            return! h next ctx
        }

let handleSubmission : HttpHandler =
    fun next ctx ->
        task {
            let! s = ctx.BindFormAsync<SubmissionModel>()
            let fullUrl = s.Url

            let! existing = lookupFullUrl fullUrl
            match existing with 
            | Some s -> 
                ctx.SetHttpHeader("X-Short-Code", s.ShortCode)
                return! (renderThanksView true (s.ShortCode) fullUrl) next ctx
            | None -> 
                let remoteIp = 
                    match ctx.Connection.RemoteIpAddress with
                    | null -> "0.0.0.0"
                    | ip -> ip.ToString()
                
                let! shortCode = createNewShortener remoteIp fullUrl
                ctx.SetHttpHeader("X-Short-Code", shortCode)

                return! (renderThanksView false shortCode fullUrl) next ctx
        }

let webApp =
    choose [
        GET >=>
            choose [
                route "/" >=> handleIndex
                // routexp "/([0-9a-zA-Z]+)" handleRedirect
                routef "/%s" handleRedirect
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
    if String.IsNullOrEmpty connectionString then
        failwith "URL_SHORTENER_CONNECTION_STRING environment variable is not set"

    let contentRoot = Directory.GetCurrentDirectory()
    let webRoot     = Path.Combine(contentRoot, "WebRoot")
    Host.CreateDefaultBuilder(args)
        .ConfigureWebHostDefaults(
            fun webHostBuilder ->
                webHostBuilder
                    .UseContentRoot(contentRoot)
                    .UseWebRoot(webRoot)
                    .ConfigureLogging(configureLogging)
                    .ConfigureServices(configureServices)
                    .Configure(Action<IApplicationBuilder> configureApp)
                    |> ignore)
        .Build()
        .Run()
    0
