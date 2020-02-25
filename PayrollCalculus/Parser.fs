namespace PayrollCalculus.Application

module FormulaParser =
    open DynamicExpresso
    open PayrollCalculus.Domain
    open PayrollCalculus.Domain.Parser


    let handle ({formula=formula; definitions=definitions}: ParseFormulaSideEffect) : ParseFormulaResult =
    
        let interpreter = DynamicExpresso.Interpreter();
        let parameters = 
            interpreter.DetectIdentifiers(formula).UnknownIdentifiers 
            |> Seq.map (fun param -> Parameter(param, definitions.[(ElemCode param)].DataType)) 
            |> Seq.toArray

        let parseResult = interpreter.Parse(formula, parameters)
   
        {   
            func= parseResult.Invoke; 
            parameters= parameters |> Array.map (fun param -> param.Name) |> Array.toList
        }