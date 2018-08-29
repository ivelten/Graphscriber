namespace Graphscriber.AspNetCore.Tests.WebApp

open System
open FSharp.Data.GraphQL.Types
open System.Collections.Concurrent
open FSharp.Data.GraphQL

type Reminder =
    { Id : Guid
      Subject : string
      Time : DateTime }

type Appointment =
    { Id : Guid
      Subject : string
      Location : string
      StartTime : DateTime
      EndTime : DateTime option
      Reminder : Reminder option }

type Entry =
    | Appointment of Appointment
    | Reminder of Reminder

type Root =
    { RequestId : Guid }

module Storage =
    let entries = ConcurrentBag<Entry>()

    let private normalize (date : DateTime) =
        DateTime(date.Year, date.Month, date.Day, date.Hour, date.Minute, date.Second)

    let private filterByReminderTime filter (entries : Entry seq) =
        entries
        |> Seq.filter (fun x ->
            match x with
            | Appointment a ->
                match a.Reminder with
                | Some r -> filter r
                | None -> false
            | Reminder r -> filter r)
        |> Seq.sortBy (fun x ->
            match x with
            | Appointment a -> a.Reminder.Value.Time
            | Reminder r -> r.Time)

    let getNextReminders limit =
        entries
        |> filterByReminderTime (fun r -> normalize r.Time > normalize DateTime.Now)
        |> Seq.truncate limit

    let alarmReminders () =
        entries
        |> filterByReminderTime (fun r -> normalize r.Time = normalize DateTime.Now)

    let addReminder subject time =
        let r = { Id = System.Guid.NewGuid(); Subject = subject; Time = time }
        entries.Add(Reminder r); r

    let addAppointment subject location startTime endTime (reminder : DateTime option) =
        let r = 
            reminder
            |> Option.map (fun x -> 
                { Id = System.Guid.NewGuid()
                  Subject = subject
                  Time = x })
        let a = 
            { Id = System.Guid.NewGuid()
              Subject = subject
              Location = location
              StartTime = startTime
              EndTime = endTime
              Reminder = r }
        entries.Add(Appointment a); a

module Schema =
    open System.Threading.Tasks

    let ReminderType =
        Define.Object<Reminder>(
            name = "Reminder",
            description = "A reminder in the calendar.",
            isTypeOf = (fun x -> x :? Reminder),
            fieldsFn = fun () ->
                [ Define.Field("id", Guid, "The ID of the reminder.", fun _ (x : Reminder) -> x.Id)
                  Define.Field("subject", String, "The subject that the reminder refers to.", fun _ (x : Reminder) -> x.Subject)
                  Define.Field("time", Date, "The date and time that the reminder should be fired.", fun _ (x : Reminder) -> x.Time) ])

    let AppointmentType =
        Define.Object<Appointment>(
            name = "Appointment",
            description = "An appointment in the calendar.",
            isTypeOf = (fun x -> x :? Appointment),
            fieldsFn = fun () ->
                [ Define.Field("id", Guid, "The ID of the appointment.", fun _ (x : Appointment) -> x.Id)
                  Define.Field("subject", String, "A description of the appointment.", fun _ (x : Appointment) -> x.Subject)
                  Define.Field("location", String, "The location where the appointment happens.", fun _ (x : Appointment) -> x.Location)
                  Define.Field("startTime", Date, "The date and time when the appointment starts.", fun _ (x : Appointment) -> x.StartTime)
                  Define.Field("endTime", Nullable Date, "The date and time when the appointment ends.", fun _ (x : Appointment) -> x.EndTime)
                  Define.Field("reminder", Nullable ReminderType, "An optional reminder for the event.", fun _ (x : Appointment) -> x.Reminder) ])

    let EntryType =
        Define.Union(
            name = "Entry",
            description = "An entry in the calendar.",
            options = [ ReminderType; AppointmentType ],
            resolveValue = (fun x ->
                match x with
                | Reminder x -> box x
                | Appointment x -> upcast x),
            resolveType = (fun x ->
                match x with
                | Reminder _ -> upcast ReminderType
                | Appointment _ -> upcast AppointmentType))

    let RootType =
        Define.Object<Root>(
            name = "Root",
            description = "The root object for all operations.",
            isTypeOf = (fun x -> x :? Root),
            fieldsFn = fun () ->
                [ Define.Field("requestId", Guid, "The ID of the requisition made by the client.", fun _ (x : Root) -> x.RequestId) ])

    let QueryType =
        Define.Object<Root>(
            name = "Query",
            fields =
                [ Define.Field("incomingReminders", ListOf EntryType, "Gets next reminders (including appointments with reminders).", 
                    [ Define.Input("limit", Int, 5, "The maximum amount of reminders to be retrieved.") ],
                    fun ctx _ -> Storage.getNextReminders (ctx.Arg("limit"))) ])

    let MutationType =
        Define.Object<Root>(
            name = "Mutation",
            fields = 
                [ Define.Field("addReminder", ReminderType, "Adds a reminder to the calendar.",
                    [ Define.Input("subject", String)
                      Define.Input("time", Date) ],
                    fun ctx _ -> 
                        let subject = ctx.Arg("subject")
                        let time = ctx.Arg("time")
                        Storage.addReminder subject time)
                  Define.Field("addAppointment", AppointmentType, "Adds an appointment to the calendar.",
                    [ Define.Input("subject", String)
                      Define.Input("location", String)
                      Define.Input("startTime", Date)
                      Define.Input("endTime", Nullable Date)
                      Define.Input("reminder", Nullable Date) ],
                    fun ctx _ -> 
                        let subject = ctx.Arg("subject")
                        let location = ctx.Arg("location")
                        let startTime = ctx.Arg("startTime")
                        let endTime = ctx.Arg("endTime")
                        let reminder = ctx.Arg("reminder")
                        Storage.addAppointment subject location startTime endTime reminder) ])

    let SubscriptionType =
        Define.SubscriptionObject<Root>(
            name = "Subscription",
            fields =
                [ Define.SubscriptionField(
                    "incomingReminders", RootType, EntryType, "Subscribes to future reminders (including appointments with reminders).",
                    fun _ _ x -> Some x) ])

    let private config = SchemaConfig.Default

    let instance = Schema(QueryType, MutationType, SubscriptionType, config)

    let executor = Executor(instance)

    do 
        async {
            while true do
                Storage.alarmReminders ()
                |> Seq.iter (fun r -> config.SubscriptionProvider.Publish "incomingReminders" r)
                do! Task.Delay(1000) |> Async.AwaitTask
        } |> Async.StartAsTask |> ignore