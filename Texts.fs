module StartupPOCBot.Texts

open StartupPOCBot.Types

let defaultGreeting = "Добро пожаловать в наш сервис!"
let getServiceGreeting = sprintf "Добро пожаловать в наш сервис из %s!"
let getDiscountsText list = sprintf "Получайте 10%% скидку в %s" ( String.concat ", " <| Seq.map (fun (i:Service) -> i.Name) list )
let signUpInfo = "Для получения скидки назовите свой код: /get_discount"

/// staff part
let enterSum = "Введите сумму заказа в формате ХХХ.YY"
let noRights = "К сожалению нетУ прав."

let enterCode = "Введите код клиента"

let getInvalidFormat text = sprintf"К сожалению не смогли распознать это число: %s /cancel_purchase - отменить покупку" text

let ready = sprintf "Готово! Сэкономлено %M грн"

let ready2 = "Для новой транзакции нажмите /new_purchase"

let discountNotice discount service = sprintf "Вы сэкономили %M грн в %s" discount service.Name

[<Literal>]
let afterCodeText = @"
Чтобы получить скидку нажми /get_discount.

Количество скидок не ограничено и действует на всё меню. 
Чтобы получить скидку снова нажимай /get_discount"

let chocolateForFirstPurchase = "Получи шоколадку на кассе за первую покупку"

let staffFirstPurchaseNotification = "Это была первая покупка у клиента - не забудьте подарить шоколадку !)"

let helpText = @"Через этот бот Вы можете получить скидку 10% на всё меню в By The Way, Diaspora Project и Fifities.

/get_discount - получите код

Чтобы получить скидку, скажите баристе при оплате ваш код

/locations - локации наших заведений

После первой покупки мы вас угостим шоколадкой на кассе.

Приятного и вкусного использования)"

let notFoundUserText = "user was not found /cancel_purchase - отменить покупку"

[<Literal>]
let startCmd = "/start"

[<Literal>]
let helpCmd = "/help"

[<Literal>]
let signUpCmd = "/get_discount"

[<Literal>]
let newPurchaseCmd = "/new_purchase"

[<Literal>]
let cancelPurchaseCmd = "/cancel_purchase"

[<Literal>]
let refreshTrigger = "db003815aac56afbbd21ec12f0b8424ddde382a8911938bc64dd6082b8ee8032d075ff78b4c66507"

[<Literal>]
let locationsCmd = "/locations"

let getCodeText = sprintf "%s - это ваш персональный промо-код на скидку. Назовите его при оплате"