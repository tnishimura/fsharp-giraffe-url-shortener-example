module Tests

open System
open Xunit
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.TestHost
open Microsoft.AspNetCore.Hosting
open System.Net.Http
open System.Collections.Generic
open FSharp.Control.Tasks // from Ply

let getTestHost() =
    if String.IsNullOrEmpty (System.Environment.GetEnvironmentVariable "URL_SHORTENER_CONNECTION_STRING") then
        failwith "URL_SHORTENER_CONNECTION_STRING environment variable is not set"

    WebHostBuilder()
        .UseTestServer()
        .Configure(Action<IApplicationBuilder> UrlShortener.App.configureApp)
        .ConfigureServices(UrlShortener.App.configureServices)
        .ConfigureLogging(UrlShortener.App.configureLogging)
        .UseUrls([|"http://localhost:5000" ; "https://localhost:5001"|])

let testRequest (request : HttpRequestMessage) =
    let resp = task {
        use server = new TestServer(getTestHost())
        use client = server.CreateClient()
        let! response = request |> client.SendAsync
        return response
    }
    resp.Result

let newSubmissionRequest (fullUrl: string) = 
    let req = new HttpRequestMessage(HttpMethod.Post, "/");
    let postData = new FormUrlEncodedContent( [ new KeyValuePair<string, string>("url", fullUrl) ] )
    req.Content <- postData
    req

[<Fact>]
let ``GET /`` () =
    let response = testRequest (new HttpRequestMessage(HttpMethod.Get, "/"))
    Assert.Equal(response.StatusCode, System.Net.HttpStatusCode.OK)

[<Fact>]
let ``Submit Url 1`` () =
    let r = Random()
    let dummyUrl = "https://example.com/?stuff=" + r.Next(1000000, 9999999).ToString()
    let response = newSubmissionRequest dummyUrl |> testRequest 
    Assert.Equal(response.StatusCode, System.Net.HttpStatusCode.OK)
    Assert.True(response.Headers.Contains("X-Short-Code"))
    try 
        match response.Headers.GetValues "X-Short-Code" |> Seq.toList with
        | shortCode :: _ -> 
            let response = testRequest (new HttpRequestMessage(HttpMethod.Get, $"/{shortCode}"))
            Assert.Equal(response.StatusCode, System.Net.HttpStatusCode.Redirect)
            Assert.Equal(response.Headers.Location.ToString(), dummyUrl)
        | _ -> 
            Assert.True(false, "response from postman-echo.com contains expected dummy header")
    with
    | :? InvalidOperationException -> 
        Assert.True(false, "X-Short-Code not found?")
