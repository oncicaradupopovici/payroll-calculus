namespace PayrollCalculus.Infra

open PayrollCalculus.Domain.Exception

module Common = 

    let handleException (ExceptionSideEffect msg) = failwith msg |> ignore
        
