module StartupPOCBot.Types

open System

type Service = {
    Id : Guid
    Name : string
    Location : (double * double)
}

type UserStatus = None | NumberRetrieval | NumberConfirmation

type StaffStatus = None | EnteringCost | EnteringClientNumber

type User = {
    TelegramUser : Funogram.Telegram.Types.User
    InvitedFrom : Option<Service>
    Status : UserStatus
    Code : Option<string>
    PhoneNumber : Option<string>
    IsConfirmed : bool
    IsActive : bool
}

type StaffUser = {
    TelegramUser : Funogram.Telegram.Types.User
    StaffStatus : StaffStatus
    Service : Service
    IsActive : bool
}

type Purchase = {
    Id: Guid
    TimestampUTC : DateTime
    Sum : decimal
    Client : Option<User>
    Service : Service
    Staff : StaffUser
}