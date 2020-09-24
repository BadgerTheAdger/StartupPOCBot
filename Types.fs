module StartupPOCBot.Types

open System

type Service = {
    Id : Guid
    Name : string
    Location : (double * double)
}

type StaffStatus = None | EnteringCost | EnteringClientCode

type StaffUser = {
    TelegramUser : Funogram.Telegram.Types.User
    StaffStatus : StaffStatus
    Service : Service
}

type Purchase = {
    Id: Guid
    TimestampUTC : DateTime
    Sum : decimal
    ClientId : Option<int64>
    Service : Service
    Staff : StaffUser
}