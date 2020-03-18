namespace PayrollCalculus.PublishedLanguage

open NBB.Application.DataContracts

type AddElemDefinition(elemCode) =
    inherit Command ()     
    member _.ElemCode with get() : string = elemCode        
