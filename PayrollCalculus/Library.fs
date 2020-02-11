namespace PayrollCalculus

open System

module Domain = 
    type ElemCode = ElemCode of string

    type ElemType = 
        | DataAccess of table:string * column:string
        | Formula of formula:string * deps: string list

    type ElemDefinition = {
        Code: ElemCode
        Type: ElemType
    }

    type IEffect<'T> = 'T list
    
    type PersonId = PersonId of Guid

    type YearMonth = {
        Year: int
        Month: int
    }

    type ComputationCtx = {
        PersonId: PersonId
        YearMonth: YearMonth
    }

    type Elem<'T> = 
        | Pure of 'T
        | Expression of (ComputationCtx -> IEffect<Elem<'T>>)

    type ElemDefinitionCache = Map<ElemCode, Elem<obj>>

    type ElemCache = Map<ElemCode, Elem<obj>>

    type ElemValuesCache = Map<ElemCode, obj>

    type ParseElem<'T>  = ElemDefinitionCache -> ElemCode -> ElemCache -> Elem<'T> * ElemCache

    type ComputeElem<'T> = ElemCache -> ElemCode -> ComputationCtx -> ElemValuesCache -> 'T * ElemValuesCache


