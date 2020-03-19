namespace PayrollCalculus.PublishedLanguage

open NBB.Application.DataContracts
open System

type AddDbElemDefinition(elemCode, table, column, dataType) =
    inherit Command ()     
    member _.ElemCode with get() : string = elemCode
    member _.Table with get() : string = table
    member _.Column with get() : string = column
    member _.DataType with get() : Type = dataType
