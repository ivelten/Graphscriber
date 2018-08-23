module Graphscriber.AspNetCore.Tests.Helpers

open System.Net.Http
open Expecto
open Graphscriber.AspNetCore
open Graphscriber.AspNetCore.JsonConverters
open Newtonsoft.Json

let serializeMessage<'Root> (msg : GQLClientMessage) =
    let settings = JsonSerializerSettings()
    let messageConverter = GQLClientMessageConverter<'Root>() :> JsonConverter
    let optionConverter = OptionConverter() :> JsonConverter
    settings.Converters <- [| optionConverter; messageConverter |]
    settings.ContractResolver <- Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver()
    JsonConvert.SerializeObject(msg, settings)

let get (client : HttpClient) (uri : string) =
    client.GetAsync(uri) |> Async.AwaitTask |> Async.RunSynchronously

let check checks item =
    checks |> Seq.iter (fun check -> check item)

let statusCodeEquals expected (response : HttpResponseMessage) =
    let actual = response.StatusCode
    Expect.equal actual expected "Unexpected HTTP status code"

let contentEquals expected (response : HttpResponseMessage) =
    let actual = response.Content.ReadAsStringAsync() |> Async.AwaitTask |> Async.RunSynchronously
    Expect.equal actual expected "Unexpected HTTP response content"