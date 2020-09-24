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
open StartupPOCBot.Types

module Async =
    let fromResult result = async { return result }

let logPatternFail () = async {
    let func = StackTrace().ToString()
    do! GoogleSheets.addLogToDb "pattern fail" -1L "None" "None" "None" func
    printfn "pattern fail at %s" func
}

let calculateDiscount m = if m < 10m then 0m else ceil (m * 0.1M)

let mutable (servicesList:Service list) = []

let getUserGreeting service =
    match service with
    | Some service -> Texts.getServiceGreeting service.Name
    | Option.None -> Texts.defaultGreeting

let sendMessage ctx userId msg = async {
    try 
        do! Api.sendMessage userId msg |> (api ctx.Config) |> Async.Ignore
        do! GoogleSheets.addLogToDb "sendMessage-success" userId "None" "None" "None" msg
    with _ -> do! GoogleSheets.addLogToDb "sendMessage-fail" userId "None" "None" "None" msg
}
let sendMap ctx userId (latitude, longitude) = Api.sendLocation userId latitude longitude |> (api ctx.Config)

let handleStart ctx (text:string) (from:Telegram.Types.User) = async {
        let service = Seq.tryFind (fun (i:Service) -> text.EndsWith(i.Id.ToString()) ) servicesList
        let greeting = getUserGreeting service
        let discountsText = Texts.getDiscountsText servicesList
        let signupText = Texts.signUpInfo
               
        for text in [greeting;discountsText;signupText] do
            do! sendMessage ctx from.Id text |> Async.Ignore
}

let staffList = List<StaffUser>()
let mutable (pendingPurchases:Purchase list) = []
let mutable (purchases:Purchase list) = []
    
let finalizePurchase (purchase:Purchase) =     
    purchases <- purchase::purchases
    pendingPurchases <- List.except [purchase] pendingPurchases

let updateStaffUser (staffUser:StaffUser) =
    staffList.RemoveAll(fun (i:StaffUser) -> i.TelegramUser.Id = staffUser.TelegramUser.Id) |> ignore
    staffList.Add(staffUser)

let onNewPurchaseCommand ctx (staffUser:StaffUser) =
    updateStaffUser { staffUser with StaffStatus = StaffStatus.EnteringCost }
    sendMessage ctx staffUser.TelegramUser.Id Texts.enterSum

let initPurchase staff cost =
    let purchase = { Id = Guid.NewGuid(); TimestampUTC = DateTime.UtcNow; ClientId = Option.None; Sum = cost; Staff = staff; Service = staff.Service }
    updateStaffUser { staff with StaffStatus = StaffStatus.EnteringClientCode }
    pendingPurchases <- purchase::pendingPurchases

let onPurchaseCostEntered ctx (text:string) (staff:StaffUser) =
    match Decimal.TryParse text with
    | (true, a) ->
        initPurchase staff a
        sendMessage ctx staff.TelegramUser.Id Texts.enterCode
    | _ -> sendMessage ctx staff.TelegramUser.Id (Texts.getInvalidFormat text)
        
let handlePurchase ctx clientId staff = async {
    let pendingPurchase = Seq.tryFind (fun i -> i.Staff.TelegramUser.Id = staff.TelegramUser.Id) pendingPurchases
    
    match pendingPurchase with
    | Some purchase ->
        let newPurchase = { purchase with ClientId = Some clientId }
        
        do finalizePurchase newPurchase
        
        updateStaffUser { staff with StaffStatus = StaffStatus.None; }       
        
        let discount = calculateDiscount purchase.Sum
        let msgToStaff = sendMessage ctx staff.TelegramUser.Id (Texts.ready discount) |> Async.Ignore
        let msgToStaff2 = sendMessage ctx staff.TelegramUser.Id Texts.ready2 |> Async.Ignore
        let msgToUser = sendMessage ctx clientId (Texts.discountNotice discount purchase.Service) |> Async.Ignore
        
        let! isFirstPurchase = GoogleSheets.isFirstPurchase clientId
        
        let firstPurchaseNotification =            
            if isFirstPurchase then
                [
                    (sendMessage ctx clientId Texts.chocolateForFirstPurchase);
                    (sendMessage ctx staff.TelegramUser.Id Texts.staffFirstPurchaseNotification)
                ] |> Async.Parallel |> Async.Ignore
                else Async.fromResult ()
        
        let addPurchase = GoogleSheets.addPurchaseToDB ( string clientId )
                             "None"                             
                             "None"
                             "None"
                             DateTime.UtcNow
                             staff.Service.Name
                             staff.Service.Id
                             purchase.Sum
                             ( string staff.TelegramUser.Id )
                             |> Async.Ignore
        
        do! msgToUser
        
        do! msgToStaff
        do! msgToStaff2
        
        do! [firstPurchaseNotification;addPurchase] |> Async.Parallel |> Async.Ignore
               
    | Option.None -> return! logPatternFail () 
}

let onClientCodeEntered ctx (text:string) (staff:StaffUser) = async {
    let! clientId = GoogleSheets.getIdFromCode text
    
    match clientId with
    | Option.None -> return! sendMessage ctx staff.TelegramUser.Id Texts.notFoundUserText |> Async.Ignore
    | Option.Some clientId -> return! handlePurchase ctx clientId staff
}

let onCancelPurchaseCommand ctx (staff:StaffUser) =
    updateStaffUser {staff with StaffStatus = StaffStatus.None}
    sendMessage ctx staff.TelegramUser.Id "cancelled current purchase..."
       
let provideUserCode ctx (user:Telegram.Types.User) = async {
    let! userCode = GoogleSheets.getCodeFromId <| string user.Id
    do! sendMessage ctx user.Id <| Texts.getCodeText userCode
    do! sendMessage ctx user.Id (Texts.afterCodeText) |> Async.Ignore
}
    
let defaultStaffMessageUpdate ctx (text:string) (staffUser:StaffUser) =
    match text with 
    | Texts.newPurchaseCmd -> onNewPurchaseCommand ctx staffUser |> Async.Ignore
    | _ -> logPatternFail ()
    
let statefulStaffMessageUpdate ctx (text:string) (staffUser:StaffUser) =
    if (text = Texts.cancelPurchaseCmd)
        then onCancelPurchaseCommand ctx staffUser |> Async.Ignore
        else         
            match staffUser.StaffStatus with
            | StaffStatus.None -> defaultStaffMessageUpdate ctx text staffUser
            | StaffStatus.EnteringCost -> onPurchaseCostEntered ctx text staffUser |> Async.Ignore
            | StaffStatus.EnteringClientCode -> onClientCodeEntered ctx text staffUser
 
let handleHelp ctx (user:Telegram.Types.User) = 
    sendMessage ctx user.Id Texts.helpText

let handleLocations ctx (user:Telegram.Types.User) = async { 
    for service in servicesList do
        do! sendMap ctx user.Id service.Location |> Async.Ignore
}
    
let ensureInStaff (user:Telegram.Types.User) logger = async {
    if (not <| Seq.exists(fun i -> i.TelegramUser.Id = user.Id) staffList)
    then
        let! staffUserServiceId = GoogleSheets.tryGetStaff user.Id
        
        match staffUserServiceId with
        | Option.None -> () 
        | Some serviceId ->
            do! logger "found user in staff, adding them to staff list"
            
            let service = Seq.tryFind (fun (i:Service) -> i.Id = serviceId) servicesList
                 
            match service with
            | Option.None -> return! logPatternFail ()
            | Option.Some a -> staffList.Add({ Service = a; TelegramUser = user; StaffStatus = StaffStatus.None; })        
    else ()    
}
    
let handleDefaultText ctx text (user:Telegram.Types.User) = async {     
    let staffInfo = Seq.tryFind (fun (i:StaffUser) -> i.TelegramUser.Id = user.Id) staffList
    
    match staffInfo with
    | Option.None -> return! logPatternFail ()
    | Option.Some staffInfo -> return! statefulStaffMessageUpdate ctx text staffInfo
}

let refreshServices () = async {
    let! a = GoogleSheets.getServices ()
    servicesList <- a
}

let handleTextPrivate ctx (text:string) = 
    let receiverAndText = text.Substring("textPrivate ".Length).Split(";")
    let receiverId = receiverAndText.[0]
    let text = receiverAndText.[1]

    sendMessage ctx (Int64.Parse receiverId) text

let removeStaffState (user:Telegram.Types.User) =
    let staffInfo = Seq.tryFind (fun (i:StaffUser) -> i.TelegramUser.Id = user.Id) staffList
    
    match staffInfo with
    | Option.None -> ()
    | Option.Some staff ->
        pendingPurchases <- List.where (fun i -> i.Staff <> staff) pendingPurchases
        updateStaffUser { staff with StaffStatus=StaffStatus.None; }

let sendToAllUsers ctx = async {
    let! ids = GoogleSheets.getAllUserIds ()
    
    for id in ids do
        do! sendMessage ctx (int64 id) "К сожалению, действие скидок через бот прекращается с 1 августа. Скоро мы приготовим для вас нечто очень крутое и выгодное"
}

let onMessageUpdate ctx msg = async {
    match msg.Text, msg.From with
    | Some text, Some user ->
        let logger = GoogleSheets.addLogToDb "onMessageUpdateStart" user.Id (Option.defaultValue "None" user.Username) user.FirstName (Option.defaultValue "None" user.LastName)
        do! logger (sprintf "user wrote message: %s" text)
        
        do! ensureInStaff user logger

        if text.StartsWith("textPrivate ") then
            return! handleTextPrivate ctx text
        
        if text.StartsWith(Texts.startCmd) then
            do! handleStart ctx text user
            do removeStaffState user
            return ()
        
        match text with
        | "qwertyu" -> do! sendToAllUsers ctx
        | Texts.signUpCmd -> do! provideUserCode ctx user |> Async.Ignore
        | Texts.locationsCmd -> do! handleLocations ctx user |> Async.Ignore
        | Texts.helpCmd -> do! handleHelp ctx user |> Async.Ignore
        | Texts.refreshTrigger ->
            let a = refreshServices ()
            let b = sendMessage ctx user.Id "Successfully refreshed services" |> Async.Ignore
            return! Async.Parallel [a;b] |> Async.Ignore
        | _ -> return! handleDefaultText ctx text user |> Async.Ignore                
    | _ -> return! logPatternFail ()
}

let onUpdate (context: UpdateContext) = async {    
    try 
        match context.Update with
        | { Message = Some msg } -> do! onMessageUpdate context msg
        | _ -> do! logPatternFail ()
    with ex ->
        let logger = GoogleSheets.addLogToDb "Unhandled exception"
        match context.Update.Message with
        | Option.Some { From = Option.Some from } ->
            do! logger from.Id
                    (Option.defaultValue "None" from.Username)
                    from.FirstName
                    (Option.defaultValue "None" from.LastName)
                    ( ex.ToString() )
        | _ -> do! logger -1L "None" "None" "None" <| ex.ToString()
}
[<EntryPoint>]
let main _ =
    refreshServices () |> Async.RunSynchronously

    startBot { defaultConfig with Token = "1213715602:AAHCYDlN9JjFF0TA5s6ILm2kVFP7sd8sWBs"; } (onUpdate >> Async.Start) Option.None 
    |> Async.RunSynchronously

    Console.ReadLine () |> ignore
    0 // return an integer exit code