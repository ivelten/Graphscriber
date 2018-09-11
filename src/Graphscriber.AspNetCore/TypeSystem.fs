namespace Graphscriber.AspNetCore

open FSharp.Data.GraphQL.Execution
open Newtonsoft.Json
open Newtonsoft.Json.Serialization
open Microsoft.FSharp.Reflection
open System
open Newtonsoft.Json.Linq
open System.Collections.Generic
open System.Linq
open Newtonsoft.Json.Linq

type GQLClientMessage =
    | ConnectionInit of payload : GQLInitOptions
    | ConnectionTerminate
    | Start of id : string * payload : GQLQuery
    | Stop of id : string
    
    static member SerializationSettings =
        JsonSerializerSettings(
            Converters = [| GQLClientMessageConverter(); OptionConverter() |],
            ContractResolver = CamelCasePropertyNamesContractResolver())
    
    member this.ToJsonString() =
        JsonConvert.SerializeObject(this, GQLClientMessage.SerializationSettings)
    
    static member FromJsonString(json : string) =
        JsonConvert.DeserializeObject<GQLClientMessage>(json, GQLClientMessage.SerializationSettings)


and GQLServerMessage =
    | ConnectionAck
    | ConnectionError of err : string
    | Data of id : string * payload : Output
    | Error of id : string option * err : string
    | Complete of id : string

    static member SerializationSettings =
        JsonSerializerSettings(
            Converters = [| GQLServerMessageConverter(); OptionConverter() |],
            ContractResolver = CamelCasePropertyNamesContractResolver())
    
    member this.ToJsonString() =
        JsonConvert.SerializeObject(this, GQLServerMessage.SerializationSettings)
    
    static member FromJsonString(json : string) =
        JsonConvert.DeserializeObject<GQLServerMessage>(json, GQLServerMessage.SerializationSettings)

and GQLQuery =
    { Query : string
      Variables : Map<string, obj> }

    static member SerializationSettings =
        JsonSerializerSettings(
            Converters = [| OptionConverter() |],
            ContractResolver = CamelCasePropertyNamesContractResolver())
    
    member this.ToJsonString() =
        JsonConvert.SerializeObject(this, GQLQuery.SerializationSettings)
    
    static member FromJsonString(json : string) =
        JsonConvert.DeserializeObject<GQLQuery>(json, GQLQuery.SerializationSettings)

and GQLInitOptions =
    { ConnectionParams : Map<string, obj> option }

    static member SerializationSettings =
        JsonSerializerSettings(
            Converters = [| OptionConverter() |],
            ContractResolver = CamelCasePropertyNamesContractResolver())

    member this.ToJsonString() =
        JsonConvert.SerializeObject(this, GQLInitOptions.SerializationSettings)
    
    static member FromJsonString(json : string) =
        JsonConvert.DeserializeObject<GQLInitOptions>(json, GQLInitOptions.SerializationSettings)

and [<Sealed>] OptionConverter() =
    inherit JsonConverter()

    override __.CanConvert(t) = 
        t.IsGenericType && t.GetGenericTypeDefinition() = typedefof<option<_>>

    override __.WriteJson(writer, value, serializer) =
        let value = 
            if value = null then null
            else 
                let _,fields = FSharpValue.GetUnionFields(value, value.GetType())
                fields.[0]  
        serializer.Serialize(writer, value)

    override __.ReadJson(reader, t, _, serializer) =        
        let innerType = t.GetGenericArguments().[0]
        let innerType = 
            if innerType.IsValueType then (typedefof<Nullable<_>>).MakeGenericType([|innerType|])
            else innerType        
        let value = serializer.Deserialize(reader, innerType)
        let cases = FSharpType.GetUnionCases(t)
        if value = null then FSharpValue.MakeUnion(cases.[0], [||])
        else FSharpValue.MakeUnion(cases.[1], [|value|])

and [<Sealed>] GQLQueryConverter() =
    inherit JsonConverter()

    override __.CanConvert(t) = t = typeof<GQLQuery>   
    
    override __.WriteJson(writer, value, _) = 
        let casted = value :?> GQLQuery
        writer.WritePropertyName("query")
        writer.WriteValue(casted.Query)
        if not (Map.isEmpty casted.Variables) then
            writer.WritePropertyName("variables")
            writer.WriteStartObject()
            casted.Variables
            |> Seq.iter (fun var -> 
                writer.WritePropertyName(var.Key)
                writer.WriteValue(var.Value))
            writer.WriteEndObject()
    
    override __.ReadJson(reader, _, _, _) =
        let jobj = JObject.Load reader
        let query = jobj.Property("query").Value.ToString()
        let variables = jobj.Property("variables").Value.ToString() |> JsonConvert.DeserializeObject<Map<string, obj>>
        upcast { Query = query; Variables = variables }

and [<Sealed>] GQLClientMessageConverter() =
    inherit JsonConverter()

    override __.CanConvert(t) = t = typedefof<GQLClientMessage> || t.DeclaringType = typedefof<GQLClientMessage>
    
    override __.WriteJson(writer, obj, _) = 
        let msg = obj :?> GQLClientMessage
        let jobj = JObject()
        match msg with
        | ConnectionInit payload ->
            jobj.Add(JProperty("type", "connection_init"))
            jobj.Add(JProperty("payload", payload.ToJsonString()))
        | ConnectionTerminate ->
            jobj.Add(JProperty("type", "connection_terminate"))
        | Start (id, payload) ->
            jobj.Add(JProperty("type", "start"))
            jobj.Add(JProperty("id", id))
            jobj.Add(JProperty("payload", payload.ToJsonString()))
        | Stop id ->
            jobj.Add(JProperty("type", "stop"))
            jobj.Add(JProperty("id", id))
        jobj.WriteTo(writer)
    
    override __.ReadJson(reader, _, _, _) =
        let jobj = JObject.Load reader
        let typ = jobj.Property("type").Value.ToString()
        match typ with
        | "connection_init" -> 
            let payload = jobj.Property("payload").Value.ToString()
            upcast ConnectionInit (GQLInitOptions.FromJsonString(payload))
        | "connection_terminate" -> upcast ConnectionTerminate
        | "start" ->
            let id = jobj.Property("id").Value.ToString()
            let payload = jobj.Property("payload").Value.ToString()
            upcast Start (id, GQLQuery.FromJsonString(payload))
        | "stop" ->
            let id = jobj.Property("id").Value.ToString()
            upcast Stop (id)
        | t -> 
            raise <| InvalidOperationException(sprintf "Message Type %s is not supported." t)

and [<Sealed>] GQLServerMessageConverter() =
    inherit JsonConverter()
    
    override __.CanConvert(t) = t = typedefof<GQLServerMessage> || t.DeclaringType = typedefof<GQLServerMessage>
    
    override __.WriteJson(writer, value, _) =
        let value = value :?> GQLServerMessage
        let jobj = JObject()
        match value with
        | ConnectionAck ->
            jobj.Add(JProperty("type", "connection_ack"))
        | ConnectionError err ->
            let errObj = JObject()
            errObj.Add(JProperty("error", err))
            jobj.Add(JProperty("type", "connection_error"))
            jobj.Add(JProperty("payload", errObj))
        | Error (id, err) ->
            let errObj = JObject()
            errObj.Add(JProperty("error", err))
            jobj.Add(JProperty("type", "error"))
            jobj.Add(JProperty("payload", errObj))
            jobj.Add(JProperty("id", id))
        | Data (id, result) ->
            jobj.Add(JProperty("type", "data"))
            jobj.Add(JProperty("id", id))
            jobj.Add(JProperty("payload", JObject.FromObject(result)))
        | Complete (id) ->
            jobj.Add(JProperty("type", "complete"))
            jobj.Add(JProperty("id", id))
        jobj.WriteTo(writer)
    
    override __.ReadJson(reader, _, _, serializer) =
        let format (payload : Output) : Output =
            let rec helper (data : obj) : obj =
                match data with
                | :? JObject as jobj ->
                    upcast (
                        jobj.ToObject<Dictionary<string, obj>>(serializer)
                        |> Seq.map (fun kval -> KeyValuePair<string, obj>(kval.Key, helper kval.Value))
                        |> Array.ofSeq
                        |> NameValueLookup
                    )
                | :? JArray as jarr ->
                    upcast (
                        jarr.ToObject<obj list>(serializer)
                        |> List.map helper
                    )
                | _ -> data
            let toOutput (seq : KeyValuePair<string, obj> seq) : Output =
                upcast Enumerable.ToDictionary(seq, (fun x -> x.Key), fun x -> x.Value)
            payload
            |> Seq.map (fun kval -> KeyValuePair<string, obj>(kval.Key, helper kval.Value))
            |> toOutput
        let jobj = JObject.Load reader
        let typ = jobj.Property("type").Value.ToString()
        match typ with
        | "connection_ack" -> upcast ConnectionAck
        | "connection_error" ->
            let payload = jobj.Property("payload").Value.ToString()
            let errObj = JObject.Parse(payload)
            let errMsg = errObj.Property("error").Value.ToString()
            upcast ConnectionError errMsg
        | "error" ->
            let id = tryGetJsonProperty jobj "id"
            let payload = jobj.Property("payload").Value.ToString()
            let errObj = JObject.Parse(payload)
            let errMsg = errObj.Property("error").Value.ToString()
            upcast Error (id, errMsg)
        | "data" ->
            let id = jobj.Property("id").Value.ToString()
            let payload = jobj.Property("payload").Value.ToObject<Dictionary<string, obj>>(serializer)
            upcast Data (id, format payload)
        | "complete" ->
            let id = jobj.Property("id").Value.ToString()
            upcast Complete id
        | t -> 
            raise <| InvalidOperationException(sprintf "Message Type %s is not supported." t)