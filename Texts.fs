module StartupPOCBot.Texts

open StartupPOCBot.Types

let defaultGreeting = "Добро пожаловать в наш сервис!"
let getServiceGreeting = sprintf "Добро пожаловать в наш сервис из %s!"
let getDiscountsText list = sprintf "Получайте 10%% скидку в %s" ( String.concat ", " <| Seq.map (fun (i:Service) -> i.Name) list ) 
let signUpInfo = "Для получения скидки пройдите регистрацию по мобильному номеру: /signup"
let numberFormat = "+380ХХХХХХХХХ"
let enterMobileNumberText = sprintf "Введите мобильный номер. В формате %s" numberFormat
let numberStartFormat = numberFormat.Substring(0,4)
let waitingForCode = "Окей, ждем ваш код :)"

let wrongFormat = sprintf "Извините, номер должен быть именно в формате %s" numberFormat

let successVerification = "Вы успешно верифицировали свой аккаунт!"

let failedVerification = "К сожалению код неверен... попробуйте еще раз"

/// staff part
let enterSum = "Введите сумму заказа в формате ХХХ.YY"
let noRights = "К сожалению нетУ прав."

let enterLastDigits = "Введите последние четыре цифры мобильного номера клиента"

let getInvalidFormat text = sprintf"К сожалению не смогли распознать это число: %s" text

let ready = "Готово!"

let discountNotice newPurchase = sprintf "Вы сэкономили %M грн в %s" (newPurchase.Sum * 0.1M) newPurchase.Service.Name

let chocolateForFirstPurchase = "Получи шоколадку на кассе за первую покупку"

let signUpText = @"
/signup - зарегистрируйтесь по мобильному номеру, обещаем, спамить не будем
"

let getHelpText withSignup = sprintf ( @"Через этот бот Вы можете получить скидку 10%% на всё меню в By The Way, Diaspora Project и Fifities.
%s
Чтобы получить скидку, скажите бариста при оплате последние 4 цифры мобильного номера

/locations - локации наших заведений

После первой покупки мы вас угостим шоколадкой на кассе.

Приятного и вкусного использования)" ) (if withSignup then signUpText else "")

let startCmd = "/start"

[<Literal>]
let helpCmd = "/help"

let signUpCmd = "/signup"

[<Literal>]
let newPurchaseCmd = "/new_purchase"

[<Literal>]
let cancelPurchaseCmd = "/cancel_purchase"

[<Literal>]
let refreshTrigger = "db003815aac56afbbd21ec12f0b8424ddde382a8911938bc64dd6082b8ee8032d075ff78b4c66507"

[<Literal>]
let switchToUser = "/i_am_user"

[<Literal>]
let switchedToUserSuccess = "switched to user mode"

[<Literal>]
let switchToPersonell = "/i_am_personell"

[<Literal>]
let switchedToPersonellSuccess = "switched to personnel mode"

[<Literal>]
let locationsCmd = "/locations"