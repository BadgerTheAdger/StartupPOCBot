module StartupPOCBot.GoogleSheets

open System
open System.Collections.Generic
open System.IO
open System.Threading
open Google.Apis.Auth.OAuth2;
open Google.Apis.Sheets.v4;
open Google.Apis.Sheets.v4.Data;
open Google.Apis.Services;
open Google.Apis.Sheets.v4
open Google.Apis.Util.Store
open Twilio.Types
open System.Linq
open StartupPOCBot.Types

let scopes:(string list) = [ SheetsService.Scope.Spreadsheets ]
let applicationName = "Google Sheets API .NET Quickstart"

let credential =
    let credPath = "token.json"
    
    use stream = new FileStream("credentials.json", FileMode.Open, FileAccess.Read)
    
    let res = GoogleWebAuthorizationBroker
                         .AuthorizeAsync(GoogleClientSecrets.Load(stream).Secrets, scopes, "user", CancellationToken.None, FileDataStore(credPath, true))
                         .GetAwaiter()
                         .GetResult()
    res

let initializer =
    let initializer = BaseClientService.Initializer()
    initializer.HttpClientInitializer <- credential
    initializer.ApplicationName <- applicationName    
    
    initializer
    
let service = new SheetsService(initializer)
    
let purchasesId = "1OHsPgML7zY7mClH1zwAhnqsqTGjhXOre7NLBVvHlj_I" 
let logsId = "1u6zc4qZUmkIf8B27qgurVcURRWJCMbd_0CaYsvYE1So"
let servicesPersonelId = "1voLzQiaQlF1WlwL1GhYNGboHqQ1LSqL1fYrKK-0r32o"

let alphabet = ['A';'B';'C';'D';'E';'F';'G';'H';'I';'J';'K';'L';'M';'N';'O'; 'P'; 'Q'; 'R'; 'S'; 'T'; 'U'; 'V'; 'W'; 'X'; 'Y'; 'Z']
     
let appendToSheet sheetId values = async {
    let range = sprintf "A:%c" (List.item (List.length values - 1) alphabet)
    let vr = ValueRange()
    
    let list = List<obj>()
    list.AddRange( values )
    let listlist = List<IList<obj>>()
    listlist.Add(list)    
    
    vr.Values <- listlist
    
    let request = SpreadsheetsResource.ValuesResource.AppendRequest(service, vr, sheetId, range)
    
    let appe = SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.RAW    
    let inse = SpreadsheetsResource.ValuesResource.AppendRequest.InsertDataOptionEnum.INSERTROWS
    
    request.ValueInputOption <- System.Nullable appe
    request.InsertDataOption <- System.Nullable inse
    
    let! response = request.ExecuteAsync() |> Async.AwaitTask
    printfn "insert to db id: %s; response: %s" sheetId response.TableRange
}

let addPurchaseToDB (userId:string) (userName:string) (firstName:string) (lastName:string) (timeStampUTC:DateTime) (serviceName:string) (serviceGuid:Guid) (uahSpent:decimal) =                                    
    appendToSheet purchasesId
                  [box userId; box userName; box firstName; box lastName; box timeStampUTC; box serviceName; box serviceGuid; box uahSpent]           
    
    
let addLogToDb (logType:string) (userId:int64) (username:string) (firstName:string) (lastName:string) (info:string)  =
    let timeStamp = DateTime.UtcNow
    appendToSheet logsId
                  [box logType; box <| string userId; box username; box firstName; box lastName; box info; box timeStamp]
                  

let getRows spreadsheetId range =
    let request = service.Spreadsheets.Values.Get(spreadsheetId, range)
    let response = request.ExecuteAsync() |> Async.AwaitTask
    response

let isFirstPurchase (userId:int64) = async {
    let! response = getRows purchasesId "A:A"
    let values = [for i in response.Values do yield! i] |> Seq.map (fun i -> (i.ToString()) )
    
    return (not <| Seq.exists (fun i -> i = userId.ToString()) values)
}

let tryGetStaff (userId:int64) = async {
    let! response = getRows servicesPersonelId "A:B"
    
    let staffUser = Seq.tryFind (fun i -> (Seq.item 0 i).ToString() = userId.ToString()) response.Values
    
    return Option.bind (fun i -> Some <| Guid ((Seq.item 1 i).ToString()) ) staffUser
}
let getServices () = async {
    let! response = getRows servicesPersonelId "F:H"
    let values = [for i in (Seq.tail response.Values) do
                      let el1 = i.Item 0 |> string |> Guid
                      let el2 = i.Item 1 |> string
                      match (i.Item 2 |> string).Split "," |> Seq.map (fun i -> double i) |> Seq.toList
                        with
                        | [latitude;longitude] -> yield ( el1, el2, (latitude, longitude) )
                        | _ -> raise (FormatException(sprintf "could not parse langitude and longitude: %s" <| string (i.Item 2)) )
                  ]
                          
    return List.map (fun (guid, name, location) -> { Id=guid; Name=name; Location=location }) values
}
let getInfo (userid:int64) = async {
    let! response = getRows logsId "A:F"
    let values = Seq.tryFind (fun (i) ->
        let logType = Seq.item 0 i
        let logUserId = Seq.item 1 i
        printfn "%A" logType
        printfn "%A" logUserId
        logType.ToString() = "Number-Verification-Success" && logUserId.ToString() = userid.ToString()) response.Values
    
    return Option.bind (fun i -> Some <| (Seq.last i).ToString() ) values
}