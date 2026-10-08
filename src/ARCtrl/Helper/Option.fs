module internal ARCtrl.Helper.Option
    
let fromValueWithDefault (defaultValue : 'T) (value : 'T) : 'T option =
    if value = defaultValue then None
    else Some value