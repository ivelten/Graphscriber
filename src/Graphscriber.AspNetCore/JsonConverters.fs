namespace Graphscriber.AspNetCore.JsonConverters

open Newtonsoft.Json
open Newtonsoft.Json.Linq
open System.Reflection
open Microsoft.FSharp.Reflection
open System
open Graphscriber.AspNetCore

[<Sealed>]
type OptionConverter() =
    inherit JsonConverter()
    
    override __.CanConvert(t) = 
        t.GetTypeInfo().IsGenericType && t.GetGenericTypeDefinition() = typedefof<option<_>>

    override __.WriteJson(writer, value, serializer) =
        let getFields value =
            let _, fields = FSharpValue.GetUnionFields(value, value.GetType())
            fields.[0]
        let value = 
            match value with
            | null ->null
            | _ -> getFields value
        serializer.Serialize(writer, value)

    override __.ReadJson(_, _, _, _) = raise <| NotSupportedException()

[<Sealed>]
type GQLQueryConverter<'Root>() =
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

[<Sealed>]
type GQLClientMessageConverter<'Root>() =
    inherit JsonConverter()

    override __.CanWrite = false
    
    override __.CanConvert(t) = t = typeof<GQLClientMessage>
    
    override __.WriteJson(writer, obj, _) = 
        let msg = obj :?> GQLClientMessage
        match msg with
        | ConnectionInit ->
            writer.WritePropertyName("type")
            writer.WriteValue("connection_init")
        | ConnectionTerminate ->
            writer.WritePropertyName("type")
            writer.WriteValue("connection_terminate")
        | Start (id, payload) ->
            writer.WritePropertyName("type")
            writer.WriteValue("start")
            writer.WritePropertyName("id")
            writer.WriteValue(id)
            writer.WritePropertyName("payload")
            let settings = JsonSerializerSettings()
            let queryConverter = GQLQueryConverter() :> JsonConverter
            let optionConverter = OptionConverter() :> JsonConverter
            settings.Converters <- [| optionConverter; queryConverter |]
            settings.ContractResolver <- Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver()
            let json = JsonConvert.SerializeObject(payload, settings)
            writer.WriteRaw(json)
        | Stop id ->
            writer.WritePropertyName("type")
            writer.WriteValue("stop")
            writer.WritePropertyName("id")
            writer.WriteValue(id)
        | ParseError _ -> raise <| InvalidOperationException("Can not serialize a parse error message.")
    
    override __.ReadJson(reader, _, _, _) =
        let jobj = JObject.Load reader
        let typ = jobj.Property("type").Value.ToString()
        match typ with
        | "connection_init" -> upcast ConnectionInit
        | "connection_terminate" -> upcast ConnectionTerminate
        | "start" ->
            let id = tryGetJsonProperty jobj "id"
            let payload = tryGetJsonProperty jobj "payload"
            match id, payload with
            | Some id, Some payload ->
                try
                    let settings = JsonSerializerSettings()
                    let queryConverter = GQLQueryConverter() :> JsonConverter
                    let optionConverter = OptionConverter() :> JsonConverter
                    settings.Converters <- [| optionConverter; queryConverter |]
                    settings.ContractResolver <- Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver()
                    let req = JsonConvert.DeserializeObject<GQLQuery>(payload, settings)
                    upcast Start(id, req)
                with e -> upcast ParseError(Some id, "Message parsing failed. " + e.Message)
            | None, _ -> upcast ParseError(None, "Malformed GQL_START message, expected id field but found none.")
            | _, None -> upcast ParseError(None, "Malformed GQL_START message, expected payload field but found none.")
        | "stop" ->
            match tryGetJsonProperty jobj "id" with
            | Some id -> upcast Stop(id)
            | None -> upcast ParseError(None, "Malformed GQL_STOP message, expected id field but found none.")
        | typ -> upcast ParseError(None, "Message Type " + typ + " is not supported.")

[<Sealed>]
type GQLServerMessageConverter() =
    inherit JsonConverter()
    
    override __.CanRead = false
    
    override __.CanConvert(t) = t = typedefof<GQLServerMessage> || t.DeclaringType = typedefof<GQLServerMessage>
    
    override __.WriteJson(writer, value, _) =
        let value = value :?> GQLServerMessage
        let jobj = JObject()
        match value with
        | ConnectionAck ->
            jobj.Add(JProperty("type", "connection_ack"))
        | ConnectionError(err) ->
            let errObj = JObject()
            errObj.Add(JProperty("error", err))
            jobj.Add(JProperty("type", "connection_error"))
            jobj.Add(JProperty("payload", errObj))
        | Error(id, err) ->
            let errObj = JObject()
            errObj.Add(JProperty("error", err))
            jobj.Add(JProperty("type", "error"))
            jobj.Add(JProperty("payload", errObj))
            jobj.Add(JProperty("id", id))
        | Data(id, result) ->
            jobj.Add(JProperty("type", "data"))
            jobj.Add(JProperty("id", id))
            jobj.Add(JProperty("payload", JObject.FromObject(result)))
        | Complete(id) ->
            jobj.Add(JProperty("type", "complete"))
            jobj.Add(JProperty("id", id))
        jobj.WriteTo(writer)
    
    override __.ReadJson(_, _, _, _) = raise <| NotSupportedException()
