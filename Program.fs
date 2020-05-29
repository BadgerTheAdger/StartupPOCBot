open System
open Funogram.Telegram.Bot
open Funogram.Api
open Funogram
open Funogram.Telegram
open Funogram.Telegram.Types
open Funogram.Types
open StartupPOCBot
open System.Collections.Generic
open System.Diagnostics

module Async =
    let fromResult result = async { return result }

let rnd = Random()

let logPatternFail () = printfn "pattern fail at %s" <| StackTrace().GetFrame(1).GetMethod().Name

type Service = {
    Id : Guid
    Name : string
}

type UserStatus = None | NumberRetrieval | NumberConfirmation

type User = {
    TelegramUser : Funogram.Telegram.Types.User
    InvitedFrom : Option<Service>
    Status : UserStatus
    Code : Option<string>
    PhoneNumber : Option<string>
    IsConfirmed : bool
}

let usersList = List<User>()

let servicesList = [
    { Id = Guid "16d53958-e235-4cc5-b2b3-e5c9d995798e"; Name = "TestService1" }
    { Id = Guid "e98d6255-2284-46c4-892e-62dc9ef75f93"; Name = "People" }
    { Id = Guid "a88def9e-e918-45c3-a147-3eefe5dd9a9c"; Name = "ByTheWay" }
]

let texts = [
    sprintf "Получайте 10%% скидку в %s" ( String.concat ", " <| Seq.map (fun i -> i.Name) servicesList  )
    "Для получения скидки пройдите регистрацию по мобильному номеру: /getDiscount"
]

let initUserData (msg:string) (user: Telegram.Types.User) =
    let service = Seq.tryFind (fun i -> msg.EndsWith(i.Id.ToString()) ) servicesList
    { TelegramUser = user; InvitedFrom = service; Status = None; Code = Option.None; PhoneNumber = Option.None; IsConfirmed = false }

let getUserGreeting (user:User) =
    match user.InvitedFrom with
    | Some service -> sprintf "Hello to our app from service %s" service.Name
    | Option.None -> "Hello to our app!"

let addUserToDb user = usersList.Add(user)

let sendMessage ctx userId msg = Api.sendMessage userId msg |> (api ctx.Config)

let handleStart ctx text from = async {
        let user = initUserData text from
        let greeting = getUserGreeting user
        
        addUserToDb user |> ignore
        
        for text in greeting::texts do
            do! sendMessage ctx user.TelegramUser.Id text |> Async.Ignore
}

let handleStartWithError ctx =
    match ctx.Update.Message with
    | Some ({Text = Some text; From = Some from}) -> handleStart ctx text from
    | _ -> Async.fromResult <| logPatternFail ()

let updateUser user =
    usersList.RemoveAll(fun i -> i.TelegramUser.Id = user.TelegramUser.Id) |> ignore
    usersList.Add(user)

let updateUserStatus userStatus (telegramUser:Telegram.Types.User) =
    let user = Seq.tryFind (fun i -> i.TelegramUser.Id = telegramUser.Id) usersList
    match user with
    | Option.Some user -> updateUser { user with Status = userStatus }
    | Option.None -> addUserToDb { TelegramUser = telegramUser; InvitedFrom = Option.None; Status = userStatus; Code = Option.None; PhoneNumber = Option.None; IsConfirmed = false }
    ()
    
let handleGetDiscount ctx from =
    updateUserStatus NumberRetrieval from |> ignore
    sendMessage ctx from.Id "Введите мобильный номер. В формате +380ХХХХХХХХХ" |> Async.Ignore
   
let handleGetDiscountWithError (ctx:UpdateContext) =
    match ctx.Update.Message with
    | Some ({From = Some from}) -> handleGetDiscount ctx from |> Async.Ignore
    | _ -> Async.fromResult <| logPatternFail ()

let generateCode () = rnd.Next(0, 9999).ToString("D4")

let handleNumberRetrieval ctx (phoneNumber:string) (user:User) =
    let code = generateCode ()
    SmsAuthentication.sendSms code phoneNumber
    updateUser { user with Status = NumberConfirmation; Code = Some code }
    sendMessage ctx user.TelegramUser.Id "Ok, we're waiting for your code!"
    
let handleNumberConfirmation ctx code user =
    Option.bind (fun userCode ->     
        if code = userCode
        then
            updateUser {user with IsConfirmed = true}
            Some <| sendMessage ctx user.TelegramUser.Id "You've successfully verified your number"
        else
            Some <| sendMessage ctx user.TelegramUser.Id "Sorry, the code is incorrect. Please try again"
    ) user.Code
    
let defaultMessageUpdate ctx (text:string) =
        if text.StartsWith("/start") then handleStartWithError ctx
        elif text = "/getDiscount" then handleGetDiscountWithError ctx
        else Async.fromResult <| logPatternFail ()
        
let statefulMessageUpdate ctx (text:string) (user:User) =
    match user.Status with
    | NumberRetrieval -> handleNumberRetrieval ctx text user |> Async.Ignore
    | NumberConfirmation ->
        match handleNumberConfirmation ctx text user with
        | Option.None -> Async.fromResult ()
        | Option.Some a -> Async.Ignore a
    | None -> defaultMessageUpdate ctx text

let onMessageUpdate ctx msg =
    match msg.Text, msg.From with
    | Some text, Some user ->
        let userinfo = Seq.tryFind (fun i -> i.TelegramUser.Id = user.Id) usersList
        match userinfo with
        | Option.Some userinfo -> statefulMessageUpdate ctx text userinfo
        | Option.None -> defaultMessageUpdate ctx text
    | _ -> Async.fromResult <| logPatternFail ()

let onUpdate (context: UpdateContext) =
    match context.Update with
    | { Message = Some msg } -> onMessageUpdate context msg
    | _ -> Async.fromResult <| logPatternFail ()

[<EntryPoint>]
let main argv =
    startBot { defaultConfig with Token = "bot_token" } (onUpdate >> Async.RunSynchronously) Option.None 
    |> Async.RunSynchronously
    Console.ReadLine () |> ignore
    0 // return an integer exit code