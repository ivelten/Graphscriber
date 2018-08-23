namespace Graphscriber.AspNetCore

open FSharp.Data.GraphQL.Execution

type GQLQuery =
    { Query : string
      Variables : Map<string, obj> }

type GQLClientMessage =
    | ConnectionInit
    | ConnectionTerminate
    | Start of id : string * payload : GQLQuery
    | Stop of id : string
    | ParseError of id : string option * err : string

type GQLServerMessage =
    | ConnectionAck
    | ConnectionError of err : string
    | Data of id : string * payload : Output
    | Error of id : string option * err : string
    | Complete of id : string