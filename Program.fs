open System
open Funogram.Telegram.Bot
open Funogram.Api
open Funogram
open Funogram.Telegram
open Funogram.Telegram.Types
open Funogram.Types
open StartupPOCBot
open StartupPOCBot
open StartupPOCBot
open StartupPOCBot
open StartupPOCBot
open StartupPOCBot
open StartupPOCBot
open StartupPOCBot
open StartupPOCBot
open StartupPOCBot
open StartupPOCBot
open System.Collections.Generic
open System.Diagnostics
open StartupPOCBot.Types

module Async =
    let fromResult result = async { return result }

let rnd = Random()

let logPatternFail () = async {
    let func = StackTrace().ToString()
    do! GoogleSheets.addLogToDb "pattern fail" -1L "None" "None" "None" func
    printfn "pattern fail at %s" func
}

let usersList = List<User>()

let mutable (servicesList:Service list) = []

let initUserData (msg:string) (user: Telegram.Types.User) = async {
    do! GoogleSheets.addLogToDb "initUserData"
                            user.Id
                            (Option.defaultValue "None" user.Username)
                            user.FirstName
                            (Option.defaultValue "None" user.LastName)
                            msg
    let service = Seq.tryFind (fun (i:Service) -> msg.EndsWith(i.Id.ToString()) ) servicesList
    return { TelegramUser = user; InvitedFrom = service; Status = UserStatus.None; Code = Option.None; PhoneNumber = Option.None; IsConfirmed = false; IsActive = true; }
}

let getUserGreeting (user:User) =
    match user.InvitedFrom with
    | Some service -> Texts.getServiceGreeting service.Name
    | Option.None -> Texts.defaultGreeting

let addUserToDb user = usersList.Add(user)

let sendMessage ctx userId msg = Api.sendMessage userId msg |> (api ctx.Config)

let sendMap ctx userId (latitude, longitude) = Api.sendLocation userId latitude longitude |> (api ctx.Config)

let handleStart ctx text from = async {
        let! user = initUserData text from
        let greeting = getUserGreeting user
        let discountsText = Texts.getDiscountsText servicesList
        let signupText = Texts.signUpInfo
        
        addUserToDb user |> ignore
        
        for text in [greeting;discountsText;signupText] do
            do! sendMessage ctx user.TelegramUser.Id text |> Async.Ignore
}

let handleStartWithError ctx =
    match ctx.Update.Message with
    | Some ({Text = Some text; From = Some from}) -> handleStart ctx text from
    | _ -> logPatternFail ()

let updateUser (user:User) =
    usersList.RemoveAll(fun i -> i.TelegramUser.Id = user.TelegramUser.Id) |> ignore
    usersList.Add(user)
    
let updateUserStatus userStatus (telegramUser:Telegram.Types.User) =
    let user = Seq.tryFind (fun (i:User) -> i.TelegramUser.Id = telegramUser.Id) usersList
    match user with
    | Option.Some user -> updateUser { user with Status = userStatus }
    | Option.None -> addUserToDb { TelegramUser = telegramUser; InvitedFrom = Option.None; Status = userStatus; Code = Option.None; PhoneNumber = Option.None; IsConfirmed = false; IsActive = true; }
    ()
    
let handleGetDiscount ctx from =
    updateUserStatus NumberRetrieval from |> ignore
    sendMessage ctx from.Id Texts.enterMobileNumberText |> Async.Ignore
   
let handleGetDiscountWithError (ctx:UpdateContext) =
    match ctx.Update.Message with
    | Some ({From = Some from}) -> handleGetDiscount ctx from |> Async.Ignore
    | _ -> logPatternFail ()

let generateCode () = rnd.Next(0, 9999).ToString("D4")

let handleNumberRetrieval ctx (phoneNumber:string) (user:User) = async {
    let validate a = String.length a = Texts.numberFormat.Length && a.StartsWith(Texts.numberStartFormat)
    
    if validate phoneNumber
    then
        let code = generateCode ()
        
        let a1 = GoogleSheets.addLogToDb
                    "code was generated"
                    user.TelegramUser.Id
                    (Option.defaultValue "None" user.TelegramUser.Username)
                    user.TelegramUser.FirstName
                    (Option.defaultValue "None" user.TelegramUser.LastName)
                    code
                    
        let a2 = SmsAuthentication.sendSms code phoneNumber
        
        do! [a1;a2] |> Async.Parallel |> Async.Ignore
        
        updateUser { user with PhoneNumber = Some phoneNumber; Status = NumberConfirmation; Code = Some code }
        return! sendMessage ctx user.TelegramUser.Id Texts.waitingForCode
    else
        return! sendMessage ctx user.TelegramUser.Id Texts.wrongFormat 
}

let handleNumberConfirmation ctx code user = async {
    match user.Code with
    | Option.None -> return ()
    | Some userCode ->
        if code = userCode    
        then
            match user.PhoneNumber with
            | Option.None -> do! logPatternFail()
            | Some a -> updateUser {user with IsConfirmed = true}
                        do! GoogleSheets.addLogToDb "Number-Verification-Success" user.TelegramUser.Id (Option.defaultValue "None" user.TelegramUser.Username) user.TelegramUser.FirstName (Option.defaultValue "None" user.TelegramUser.LastName) a
                        do! sendMessage ctx user.TelegramUser.Id Texts.successVerification |> Async.Ignore
                        do! sendMessage ctx user.TelegramUser.Id (Texts.getHelpText false) |> Async.Ignore                        
        else
            do! GoogleSheets.addLogToDb "Number-Verification-Failure" user.TelegramUser.Id (Option.defaultValue "None" user.TelegramUser.Username) user.TelegramUser.FirstName (Option.defaultValue "None" user.TelegramUser.LastName) (sprintf "Code: %s" code)            
            do! sendMessage ctx user.TelegramUser.Id Texts.failedVerification |> Async.Ignore
}                

    
    
///////////////// staff part
///
///

let staffList = List<StaffUser>()
let mutable (pendingPurchases:Purchase list) = []
let mutable (purchases:Purchase list) = []
    
let finalizePurchase (purchase:Purchase) = async {
    match purchase.Client with
    | Option.None -> ()
    | Some client ->
        let id = client.TelegramUser.Id 
        do! GoogleSheets.addLogToDb
                "finalizePurchase"
                id (Option.defaultValue "None" client.TelegramUser.Username)
                client.TelegramUser.FirstName
                (Option.defaultValue "None" client.TelegramUser.LastName)
                "self-explanatory"
     
    purchases <- purchase::purchases
    pendingPurchases <- List.except [purchase] pendingPurchases
}

let updateStaffUser (staffUser:StaffUser) =
    staffList.RemoveAll(fun (i:StaffUser) -> i.TelegramUser.Id = staffUser.TelegramUser.Id) |> ignore
    staffList.Add(staffUser)

let onNewPurchaseCommand ctx (staffUser:StaffUser) =
    updateStaffUser { staffUser with StaffStatus = StaffStatus.EnteringCost }
    sendMessage ctx staffUser.TelegramUser.Id Texts.enterSum

let initPurchase staff cost =
    let purchase = { Id = Guid.NewGuid(); TimestampUTC = DateTime.UtcNow; Client = Option.None; Sum = cost; Staff = staff; Service = staff.Service }
    updateStaffUser { staff with StaffStatus = StaffStatus.EnteringClientNumber }
    pendingPurchases <- purchase::pendingPurchases

let onPurchaseCostEntered ctx (text:string) (staff:StaffUser) =
    match Decimal.TryParse text with
    | (true, a) ->  
        initPurchase staff a
        sendMessage ctx staff.TelegramUser.Id Texts.enterLastDigits
    | _ -> sendMessage ctx staff.TelegramUser.Id (Texts.getInvalidFormat text)
        
let handlePurchase ctx client staff = async {
    let pendingPurchase = Seq.tryFind (fun i -> i.Staff.TelegramUser.Id = staff.TelegramUser.Id) pendingPurchases
    
    match pendingPurchase with
    | Some purchase ->
        let newPurchase = { purchase with Client = Some client }
        updateStaffUser { staff with StaffStatus = StaffStatus.None; }
        
        let finalization = finalizePurchase newPurchase
        let msgToStaff = sendMessage ctx staff.TelegramUser.Id Texts.ready |> Async.Ignore
        let msgToUser = sendMessage ctx client.TelegramUser.Id (Texts.discountNotice purchase) |> Async.Ignore
        
        let! isFirstPurchase = GoogleSheets.isFirstPurchase client.TelegramUser.Id        
        
        let msgToUserFirstPurchase =            
            if isFirstPurchase then
                (sendMessage ctx client.TelegramUser.Id Texts.chocolateForFirstPurchase) |> Async.Ignore
                else Async.fromResult ()
        
        let addPurchase = GoogleSheets.addPurchaseToDB ( client.TelegramUser.Id.ToString() )
                             (Option.defaultValue "None" client.TelegramUser.Username)
                             client.TelegramUser.FirstName
                             (Option.defaultValue "None" client.TelegramUser.LastName)
                             DateTime.UtcNow
                             staff.Service.Name
                             staff.Service.Id
                             purchase.Sum
                             |> Async.Ignore
        
        do! [msgToStaff;msgToUser;msgToUserFirstPurchase;addPurchase;finalization] |> Async.Parallel |> Async.Ignore
               
    | Option.None -> return! logPatternFail () 
}

let onClientNumberEntered ctx (text:string) (staff:StaffUser) =
    let client = Seq.tryPick (fun i ->
        match i.PhoneNumber with
        | Option.None -> Option.None
        | Option.Some a -> if a.EndsWith(text) then Some i else Option.None ) usersList
    
    match client with
    | Option.None -> sendMessage ctx staff.TelegramUser.Id "user was not found" |> Async.Ignore
    | Option.Some client -> handlePurchase ctx client staff

let onCancelPurchaseCommand ctx (staff:StaffUser) =
    updateStaffUser {staff with StaffStatus = StaffStatus.None}
    sendMessage ctx staff.TelegramUser.Id "cancelled current purchase..."
       
let defaultMessageUpdate ctx (text:string) =
        if text.StartsWith(Texts.startCmd) then handleStartWithError ctx
        elif text = Texts.signUpCmd then handleGetDiscountWithError ctx
        else logPatternFail ()

        
let statefulMessageUpdate ctx (text:string) (user:User) =
    match user.Status with
    | NumberRetrieval -> handleNumberRetrieval ctx text user |> Async.Ignore
    | NumberConfirmation -> handleNumberConfirmation ctx text user        
    | UserStatus.None -> defaultMessageUpdate ctx text
    
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
            | StaffStatus.EnteringClientNumber -> onClientNumberEntered ctx text staffUser
 
let handleHelp ctx (user:Telegram.Types.User) = 
    sendMessage ctx 
                user.Id 
                (Texts.getHelpText 
                (Seq.exists (fun (i:User) -> i.Status = UserStatus.None && i.IsConfirmed = false && i.TelegramUser.Id = user.Id) usersList))

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
            | Option.Some a -> staffList.Add({ Service = a; TelegramUser = user; StaffStatus = StaffStatus.None; IsActive = true; })        
    else ()    
}

let ensureUserAlreadyAuthorized (user:Telegram.Types.User) = async {
    if (Seq.forall (fun (i:User) -> i.TelegramUser.Id <> user.Id) usersList)
    then
        let! phoneNumber = GoogleSheets.getInfo user.Id
        
        match phoneNumber with
        | Option.None -> ()
        | Option.Some a -> addUserToDb { TelegramUser = user; InvitedFrom = Option.None; Status = UserStatus.None; IsConfirmed = true; Code = Option.None; PhoneNumber = Some a; IsActive = true;} 
}

let ensureOptionSwitched ctx text (user:Telegram.Types.User) = async {
        let logger = GoogleSheets.addLogToDb "ensureOptionSwitched" user.Id (Option.defaultValue "None" user.Username) user.FirstName (Option.defaultValue "None" user.LastName)
        let msgToUser = (sendMessage ctx user.Id) >> Async.Ignore
        
        let userInfo = Seq.tryFind (fun (i:User) -> i.TelegramUser.Id = user.Id) usersList
        let staffInfo = Seq.tryFind (fun (i:StaffUser) -> i.TelegramUser.Id = user.Id) staffList        
        
        match userInfo, staffInfo with
        | Some user, Some staff ->          
            match text with 
            | Texts.switchToUser ->
                updateUser { user with IsActive = true; }
                updateStaffUser { staff with IsActive = false; }
                let a1 = logger Texts.switchedToUserSuccess
                let a2 = msgToUser Texts.switchedToUserSuccess
                do! Async.Parallel [a1;a2] |> Async.Ignore
            | Texts.switchToPersonell ->
                updateUser { user with IsActive = false; }
                updateStaffUser { staff with IsActive = true; }
                let a1 = logger Texts.switchedToPersonellSuccess
                let a2 = msgToUser Texts.switchedToPersonellSuccess
                do! Async.Parallel [a1;a2] |> Async.Ignore
            | _ -> return ()
        | _ -> return ()
}
    

let handleDefaultText ctx text (user:Telegram.Types.User) = async { 
    do! ensureOptionSwitched ctx text user 
    
    let userinfo = Seq.tryFind (fun (i:User) -> i.IsActive && i.TelegramUser.Id = user.Id) usersList
    let staffInfo = Seq.tryFind (fun (i:StaffUser) -> i.IsActive && i.TelegramUser.Id = user.Id) staffList
    
    match userinfo, staffInfo with
    | Option.Some userinfo, Option.None -> return! statefulMessageUpdate ctx text userinfo
    | Option.None, Option.Some staffInfo -> return! statefulStaffMessageUpdate ctx text staffInfo
    | Option.None, Option.None -> return! defaultMessageUpdate ctx text
    | Option.Some _, Option.Some _ -> return! logPatternFail ()
}

let refreshServices () = async {
    let! a = GoogleSheets.getServices ()
    servicesList <- a
}

let onMessageUpdate ctx msg = async {
    match msg.Text, msg.From with
    | Some text, Some user ->
        let logger = GoogleSheets.addLogToDb "onMessageUpdateStart" user.Id (Option.defaultValue "None" user.Username) user.FirstName (Option.defaultValue "None" user.LastName)
        do! logger (sprintf "user wrote message: %s" text)
        
        if text = Texts.startCmd
            then usersList.RemoveAll(fun i -> i.TelegramUser.Id = user.Id) |> ignore
            else    
                let a1 = ensureInStaff user logger
                let a2 = ensureUserAlreadyAuthorized user

                do! [a1;a2] |> Async.Parallel |> Async.Ignore

        match text with
        | Texts.locationsCmd -> do! handleLocations ctx user |> Async.Ignore
        | Texts.helpCmd -> do! handleHelp ctx user |> Async.Ignore
        | Texts.refreshTrigger ->
            let a = refreshServices ()
            let b = sendMessage ctx user.Id "Successfuly refreshed services" |> Async.Ignore
            return! Async.Parallel [a;b] |> Async.Ignore
        | _ -> return! handleDefaultText ctx text user |> Async.Ignore                
    | _ -> return! logPatternFail ()
}

let onUpdate (context: UpdateContext) =
    try 
        match context.Update with
        | { Message = Some msg } -> onMessageUpdate context msg
        | _ -> logPatternFail ()
    with ex ->
        let logger = GoogleSheets.addLogToDb "Unhandled exception"
        match context.Update.Message with
        | Option.Some { From = Option.Some from } ->
            logger from.Id
                    (Option.defaultValue "None" from.Username)
                    from.FirstName
                    (Option.defaultValue "None" from.LastName)
                    ( ex.ToString() )
        | _ -> logger -1L "None" "None" "None" <| ex.ToString()
          

[<EntryPoint>]
let main argv =
    refreshServices () |> Async.RunSynchronously

    startBot { defaultConfig with Token = "1213715602:AAHCYDlN9JjFF0TA5s6ILm2kVFP7sd8sWBs"; } (onUpdate >> Async.Start) Option.None 
    |> Async.RunSynchronously
    Console.ReadLine () |> ignore
    0 // return an integer exit code