namespace Graphscriber.AspNetCore

open System
open System.Text
open Newtonsoft.Json.Linq

[<AutoOpen>]
module internal Helpers =
    let tee f x =
        f x
        x

[<AutoOpen>]
module internal StringHelpers =
    let utf8String (bytes : byte seq) =
        bytes
        |> Seq.filter (fun i -> i > 0uy)
        |> Array.ofSeq
        |> Encoding.UTF8.GetString

    let utf8Bytes (str : string) =
        str |> Encoding.UTF8.GetBytes

    let isNullOrWhiteSpace (str : string) =
        String.IsNullOrWhiteSpace(str)

[<AutoOpen>]
module internal JsonHelpers =
    let tryGetJsonProperty (jobj: JObject) prop =
        match jobj.Property(prop) with
        | null -> None
        | p -> Some(p.Value.ToString())