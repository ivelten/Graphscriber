namespace Graphscriber.AspNetCore

open System
open System.Net.WebSockets
open System.Collections.Concurrent
open System.Collections.Generic
open System.Threading
open System.Threading.Tasks
open Newtonsoft.Json
open Graphscriber.AspNetCore.JsonConverters

type IGQLWebSocket<'Root> =
    inherit IDisposable
    abstract member Subscribe : string * IDisposable -> unit
    abstract member Unsubscribe : string -> unit
    abstract member UnsubscribeAll : unit -> unit
    abstract member Id : Guid
    abstract member SendAsync : GQLServerMessage -> Task
    abstract member ReceiveAsync : unit -> Task<GQLClientMessage option>
    abstract member State : WebSocketState
    abstract member CloseAsync : unit -> Task

type GQLWebSocket<'Root> (inner : WebSocket) =
    let subscriptions : IDictionary<string, IDisposable> = 
        upcast ConcurrentDictionary<string, IDisposable>()

    let id = System.Guid.NewGuid()

    member __.Subscribe(id : string, unsubscriber : IDisposable) =
        subscriptions.Add(id, unsubscriber)

    member __.Unsubscribe(id : string) =
        match subscriptions.ContainsKey(id) with
        | true ->
            subscriptions.[id].Dispose()
            subscriptions.Remove(id) |> ignore
        | false -> ()

    member __.UnsubscribeAll() =
        subscriptions
        |> Seq.iter (fun x -> x.Value.Dispose())
        subscriptions.Clear()
        
    member __.Id = id

    member __.SendAsync(message: GQLServerMessage) =
        async {
            let settings =
                GQLServerMessageConverter() :> JsonConverter
                |> Seq.singleton
                |> jsonSerializerSettings
            let json = JsonConvert.SerializeObject(message, settings)
            let buffer = utf8Bytes json
            let segment = new ArraySegment<byte>(buffer)
            do! inner.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None) |> Async.AwaitTask
        } |> Async.StartAsTask :> Task

    member __.ReceiveAsync() =
        async {
            let buffer = Array.zeroCreate 4096
            let segment = ArraySegment<byte>(buffer)
            do! inner.ReceiveAsync(segment, CancellationToken.None)
                |> Async.AwaitTask
                |> Async.Ignore
            let message = utf8String buffer
            if isNullOrWhiteSpace message
            then
                return None
            else
                let settings =
                    GQLClientMessageConverter() :> JsonConverter
                    |> Seq.singleton
                    |> jsonSerializerSettings
                return JsonConvert.DeserializeObject<GQLClientMessage>(message, settings) |> Some
        } |> Async.StartAsTask

    member __.Dispose = inner.Dispose

    member __.CloseAsync() = 
        inner.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None)

    member __.State = inner.State

    interface IDisposable with
        member this.Dispose() = this.Dispose()

    interface IGQLWebSocket<'Root> with
        member this.Subscribe(id, unsubscriber) = this.Subscribe(id, unsubscriber)
        member this.Unsubscribe(id) = this.Unsubscribe(id)
        member this.UnsubscribeAll() = this.UnsubscribeAll()
        member this.Id = this.Id
        member this.SendAsync(message) = this.SendAsync(message)
        member this.ReceiveAsync() = this.ReceiveAsync()
        member this.State = this.State
        member this.CloseAsync() = this.CloseAsync()