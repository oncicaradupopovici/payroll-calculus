namespace PayrollCalculus.Infra

open DynamicExpresso
open PayrollCalculus.Domain
open PayrollCalculus.Domain.Parser

module FormulaParser =

    let parse ({Formula = formula; ElemDefinitions = definitions}: ParseFormulaSideEffect) : ParseFormulaResult =
    
        let interpreter = DynamicExpresso.Interpreter();
        let parameters = 
            interpreter.DetectIdentifiers(formula).UnknownIdentifiers 
            |> Seq.map (fun param -> Parameter(param, definitions.[param |> ElemCode].DataType)) 
            |> Seq.toArray

        let parseResult = interpreter.Parse(formula, parameters)
   
        {   
            Func= parseResult.Invoke; 
            Parameters= parameters |> Array.map (fun param -> param.Name) |> Array.toList
        }

   