module StartupPOCBot.SmsAuthentication

open Twilio
open Twilio.Rest.Api.V2010.Account
    
    let accountSid = "acc_sid"
    let authToken = "auth_token"
    
    let sendSms text number =
        TwilioClient.Init(accountSid, authToken)
        let msg = MessageResource.Create(body = text, from = new Twilio.Types.PhoneNumber("+12092316558"), ``to`` = new Twilio.Types.PhoneNumber(number))
        printfn "%A" msg.Status
        ()
